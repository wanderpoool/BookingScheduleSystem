using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Bookings;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Bookings;

public sealed class ListBookingsRequest
{
    public Guid? ScheduleId { get; set; }
    public Guid? UserId { get; set; }
    public BookingStatus? Status { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class ListBookings : Endpoint<ListBookingsRequest, ListBookingsResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Get("/api/bookings");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Bookings")
            .WithSummary("List bookings")
            .WithDescription("Retrieves paginated list of bookings. Regular users see only their bookings; admins see all bookings for the tenant."));
    }

    public override async Task HandleAsync(ListBookingsRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (tenantId is null)
        {
            ThrowError("Tenant context is required", 400);
        }

        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
        {
            ThrowError("User not authenticated", 401);
        }

        var currentUserId = UserId.Parse(userIdClaim);
        var isAdmin = User.IsInRole("GlobalAdmin");
        var isProvider = User.IsInRole("Provider");

        await using var session = DocumentStore.LightweightSession();

        var pageNumber = Math.Max(1, req.PageNumber);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        IQueryable<Booking> query = session.Query<Booking>()
            .Where(b => b.TenantId == tenantId);

        // Determine which schedule IDs this provider owns (for provider-level access)
        var providerScheduleIds = new HashSet<ScheduleId>();
        if (isProvider && !isAdmin)
        {
            var ownedSchedules = await session.Query<Schedule>()
                .Where(s => s.TenantId == tenantId && s.ProviderId == currentUserId)
                .Select(s => s.Id)
                .ToListAsync(ct);
            providerScheduleIds = ownedSchedules.ToHashSet();
        }

        if (isAdmin)
        {
            // Admins see all tenant bookings
            if (req.UserId.HasValue)
            {
                var filterUserId = new UserId(req.UserId.Value);
                query = query.Where(b => b.UserId == filterUserId);
            }
        }
        else if (isProvider)
        {
            // Providers see their own bookings + bookings on their schedules
            // Marten LINQ can't handle complex OR with HashSet, so fetch all tenant bookings and filter in-memory
            // Apply other filters in DB first, then filter access in-memory after fetch
        }
        else
        {
            // Regular users only see their own bookings
            query = query.Where(b => b.UserId == currentUserId);
        }

        if (req.ScheduleId.HasValue)
        {
            var scheduleId = new ScheduleId(req.ScheduleId.Value);
            query = query.Where(b => b.ScheduleId == scheduleId);
        }

        if (req.Status.HasValue)
        {
            query = query.Where(b => b.Status == req.Status.Value);
        }

        IReadOnlyList<Booking> bookings;
        int totalCount;

        if (isProvider && !isAdmin)
        {
            // For providers, fetch all matching bookings then filter in-memory
            var allMatchingBookings = await query
                .OrderByDescending(b => b.BookedAt)
                .ToListAsync(ct);

            var accessibleBookings = allMatchingBookings
                .Where(b => b.UserId == currentUserId || providerScheduleIds.Contains(b.ScheduleId))
                .ToList();

            totalCount = accessibleBookings.Count;
            bookings = accessibleBookings
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }
        else
        {
            totalCount = (int)await query.CountAsync(ct);
            bookings = await query
                .OrderByDescending(b => b.BookedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        // Load related schedules for enrichment
        var scheduleIds = bookings.Select(b => b.ScheduleId).Distinct().ToList();
        var schedules = new Dictionary<ScheduleId, Schedule>();
        foreach (var sid in scheduleIds)
        {
            var schedule = await session.LoadAsync<Schedule>(sid, ct);
            if (schedule != null)
                schedules[sid] = schedule;
        }

        // Load customer names for enrichment
        var userIds = bookings.Select(b => b.UserId).Distinct().ToList();
        var userNames = new Dictionary<UserId, string>();
        foreach (var uid in userIds)
        {
            var user = await session.LoadAsync<User>(uid, ct);
            if (user != null)
                userNames[uid] = $"{user.FirstName} {user.LastName}".Trim();
        }

        Response = new ListBookingsResponse
        {
            Bookings = bookings.Select(b =>
            {
                schedules.TryGetValue(b.ScheduleId, out var schedule);
                userNames.TryGetValue(b.UserId, out var customerName);
                return new BookingResponse
                {
                    Id = b.Id,
                    ScheduleId = b.ScheduleId,
                    UserId = b.UserId,
                    TenantId = b.TenantId,
                    Status = b.Status,
                    ReferenceNumber = b.ReferenceNumber,
                    Notes = b.Notes,
                    BookedAt = b.BookedAt,
                    CancelledAt = b.CancelledAt,
                    CancellationReason = b.CancellationReason,
                    ScheduleTitle = schedule?.Title,
                    ScheduleStartTime = schedule?.StartTime,
                    ScheduleEndTime = schedule?.EndTime,
                    CustomerName = customerName
                };
            }).ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
