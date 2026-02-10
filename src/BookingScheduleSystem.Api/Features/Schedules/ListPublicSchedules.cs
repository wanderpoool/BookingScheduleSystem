using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Schedules;

public sealed class ListPublicSchedulesRequest
{
    public Guid TenantId { get; set; }
    public Guid? ProviderId { get; set; }
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ListPublicSchedules : Endpoint<ListPublicSchedulesRequest, ListPublicSchedulesResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/tenants/{TenantId}/schedules");
        AllowAnonymous();
        Description(d => d
            .WithTags("Schedules")
            .WithSummary("Browse provider availability for a tenant")
            .WithDescription("Returns per-provider availability blocks showing open and booked time slots."));
    }

    public override async Task HandleAsync(ListPublicSchedulesRequest req, CancellationToken ct)
    {
        var tenantId = new TenantId(req.TenantId);
        var pageNumber = Math.Max(1, req.PageNumber);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        await using var session = DocumentStore.LightweightSession();

        var now = DateTimeOffset.UtcNow;
        var startDateOffset = req.StartDate ?? now;
        var endDateOffset = req.EndDate ?? now.AddDays(30);

        // Marten uses 'timestamp without time zone' — must use DateTime with Kind=Unspecified
        var startDate = DateTime.SpecifyKind(startDateOffset.UtcDateTime, DateTimeKind.Unspecified);
        var endDate = DateTime.SpecifyKind(endDateOffset.UtcDateTime, DateTimeKind.Unspecified);

        // Load tenant for fallback operating hours
        var tenant = await session.LoadAsync<Tenant>(tenantId, ct);

        // Query providers for this tenant
        var providersQuery = session.Query<User>()
            .Where(u => u.TenantId == tenantId && u.IsProvider && u.IsActive);

        if (req.ProviderId.HasValue)
        {
            var providerId = new UserId(req.ProviderId.Value);
            providersQuery = providersQuery.Where(u => u.Id == providerId);
        }

        var totalProviders = await providersQuery.CountAsync(ct);

        var providers = await providersQuery
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // Load all active schedules for this tenant in the date range
        var allSchedules = await session.Query<Schedule>()
            .Where(s => s.TenantId == tenantId
                && s.IsActive
                && s.StartTime < endDate
                && s.EndTime > startDate)
            .ToListAsync(ct);

        // Filter to only schedules belonging to our page of providers
        var providerIdSet = providers.Select(p => p.Id).ToHashSet();
        var relevantSchedules = allSchedules
            .Where(s => s.ProviderId.HasValue && providerIdSet.Contains(s.ProviderId.Value))
            .ToList();

        // Load bookings for the relevant schedules to enrich time blocks
        // Note: Marten can't serialize List<ScheduleId> for SQL arrays, so filter in-memory
        var scheduleIdSet = relevantSchedules.Select(s => s.Id).ToHashSet();
        var tenantBookings = await session.Query<Booking>()
            .Where(b => b.TenantId == tenantId
                && b.Status != BookingStatus.Cancelled)
            .ToListAsync(ct);
        var allBookings = tenantBookings
            .Where(b => scheduleIdSet.Contains(b.ScheduleId))
            .ToList();

        // Load customer names for the bookings
        var bookingUserIdSet = allBookings.Select(b => b.UserId).ToHashSet();
        var allTenantUsers = providers.ToList<User>(); // reuse already-loaded providers
        // Load any additional users not in the providers list
        var missingUserIds = bookingUserIdSet.Except(providers.Select(p => p.Id)).ToHashSet();
        var additionalUsers = missingUserIds.Count > 0
            ? (await session.Query<User>()
                .Where(u => u.TenantId == tenantId)
                .ToListAsync(ct))
                .Where(u => missingUserIds.Contains(u.Id))
                .ToList()
            : new List<User>();
        var userNamesById = providers.Concat(additionalUsers)
            .Where(u => bookingUserIdSet.Contains(u.Id))
            .ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}");

        var availability = AvailabilityCalculator.BuildProviderAvailability(
            providers,
            relevantSchedules,
            tenant?.OperatingHours,
            DateOnly.FromDateTime(startDate),
            DateOnly.FromDateTime(endDate),
            allBookings,
            userNamesById);

        Response = new ListPublicSchedulesResponse
        {
            Providers = availability,
            TotalCount = (int)totalProviders,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
