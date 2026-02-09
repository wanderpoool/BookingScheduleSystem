using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Bookings;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Bookings;

public sealed class GetBookingRequest
{
    public Guid BookingId { get; set; }
}

public sealed class GetBooking : Endpoint<GetBookingRequest, BookingResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Get("/api/bookings/{BookingId}");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Bookings")
            .WithSummary("Get booking by ID")
            .WithDescription("Retrieves a specific booking by ID. Users can only view their own bookings."));
    }

    public override async Task HandleAsync(GetBookingRequest req, CancellationToken ct)
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

        var userId = UserId.Parse(userIdClaim);
        var isAdmin = User.IsInRole("GlobalAdmin");

        await using var session = DocumentStore.LightweightSession();

        var bookingId = new BookingId(req.BookingId);
        var booking = await session.LoadAsync<Booking>(bookingId, ct);

        if (booking is null)
        {
            ThrowError("Booking not found", 404);
        }

        if (booking.TenantId != tenantId)
        {
            ThrowError("Booking not found", 404);
        }

        // Users can only view their own bookings unless they're an admin
        if (!isAdmin && booking.UserId != userId)
        {
            ThrowError("Booking not found", 404);
        }

        Response = new BookingResponse
        {
            Id = booking.Id,
            ScheduleId = booking.ScheduleId,
            UserId = booking.UserId,
            TenantId = booking.TenantId,
            Status = booking.Status,
            Notes = booking.Notes,
            BookedAt = booking.BookedAt,
            CancelledAt = booking.CancelledAt,
            CancellationReason = booking.CancellationReason
        };
    }
}
