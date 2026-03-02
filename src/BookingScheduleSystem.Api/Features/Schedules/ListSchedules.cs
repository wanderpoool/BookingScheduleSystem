using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Schedules;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Schedules;

public sealed class ListSchedulesRequest
{
    public Guid? ProviderId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool? IsActive { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ListSchedules : Endpoint<ListSchedulesRequest, ListSchedulesResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Get("/api/schedules");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Schedules")
            .WithSummary("List provider availability")
            .WithDescription("Returns per-provider availability blocks for the authenticated tenant with optional filters."));
    }

    public override async Task HandleAsync(ListSchedulesRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (tenantId is null)
        {
            ThrowError("Tenant context is required", 400);
        }

        await using var session = DocumentStore.LightweightSession();

        var pageNumber = Math.Max(1, req.PageNumber);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        var now = DateTimeOffset.UtcNow;
        // Marten uses 'timestamp without time zone' — must use DateTime with Kind=Unspecified
        var startDate = req.StartDate.HasValue
            ? DateTime.SpecifyKind(req.StartDate.Value, DateTimeKind.Unspecified)
            : DateTime.SpecifyKind(now.UtcDateTime, DateTimeKind.Unspecified);
        var endDate = req.EndDate.HasValue
            ? DateTime.SpecifyKind(req.EndDate.Value, DateTimeKind.Unspecified)
            : DateTime.SpecifyKind(now.AddDays(30).UtcDateTime, DateTimeKind.Unspecified);

        // Load tenant for fallback operating hours
        var tenant = await session.LoadAsync<Tenant>(tenantId, ct);

        // Query providers for this tenant via UserTenant membership
        var providerMemberships = await session.Query<UserTenant>()
            .Where(ut => ut.TenantId == tenantId && ut.Role == "Provider" && ut.IsActive)
            .ToListAsync(ct);
        var providerUserIds = providerMemberships.Select(ut => ut.UserId).ToHashSet();

        var allProviders = (await session.Query<User>()
            .Where(u => u.IsProvider && u.IsActive)
            .ToListAsync(ct))
            .Where(u => providerUserIds.Contains(u.Id));

        if (req.ProviderId.HasValue)
        {
            var providerId = new UserId(req.ProviderId.Value);
            allProviders = allProviders.Where(u => u.Id == providerId);
        }

        var providersList = allProviders.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList();
        var totalProviders = providersList.Count;

        var providers = providersList
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Load schedules for this tenant in the date range
        IQueryable<Schedule> schedulesQuery = session.Query<Schedule>()
            .Where(s => s.TenantId == tenantId
                && s.StartTime < endDate
                && s.EndTime > startDate);

        if (req.IsActive.HasValue)
        {
            schedulesQuery = schedulesQuery.Where(s => s.IsActive == req.IsActive.Value);
        }

        var allSchedules = await schedulesQuery.ToListAsync(ct);

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

        // Load customer names for the bookings (users may belong to multiple tenants)
        var bookingUserIdSet = allBookings.Select(b => b.UserId).ToHashSet();
        var missingUserIds = bookingUserIdSet.Except(providers.Select(p => p.Id)).ToHashSet();
        var additionalUsers = missingUserIds.Count > 0
            ? (await session.Query<User>()
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

        Response = new ListSchedulesResponse
        {
            Providers = availability,
            TotalCount = (int)totalProviders,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
