using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
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

public sealed class ListBookingsResponse
{
    public required List<BookingResponse> Bookings { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
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

        await using var session = DocumentStore.LightweightSession();

        var pageNumber = Math.Max(1, req.PageNumber);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        IQueryable<Booking> query = session.Query<Booking>()
            .Where(b => b.TenantId == tenantId);

        // Regular users can only see their own bookings
        if (!isAdmin)
        {
            query = query.Where(b => b.UserId == currentUserId);
        }
        else if (req.UserId.HasValue)
        {
            // Admins can filter by specific user
            var filterUserId = new UserId(req.UserId.Value);
            query = query.Where(b => b.UserId == filterUserId);
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

        var totalCount = await query.CountAsync(ct);

        var bookings = await query
            .OrderByDescending(b => b.BookedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        Response = new ListBookingsResponse
        {
            Bookings = bookings.Select(b => new BookingResponse
            {
                Id = b.Id,
                ScheduleId = b.ScheduleId,
                UserId = b.UserId,
                TenantId = b.TenantId,
                Status = b.Status,
                Notes = b.Notes,
                BookedAt = b.BookedAt,
                CancelledAt = b.CancelledAt,
                CancellationReason = b.CancellationReason
            }).ToList(),
            TotalCount = (int)totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
