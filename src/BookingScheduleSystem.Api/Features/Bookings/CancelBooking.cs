using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Bookings;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Bookings;

public sealed class CancelBookingRequestWithId
{
    public Guid BookingId { get; set; }
    public required CancelBookingRequest Cancellation { get; set; }
}

public sealed class CancelBooking : Endpoint<CancelBookingRequestWithId, BookingResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }
    public required BookingNotificationService NotificationService { get; init; }

    public override void Configure()
    {
        Post("/api/bookings/{BookingId}/cancel");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Bookings")
            .WithSummary("Cancel a booking")
            .WithDescription("Cancels an existing booking. Users can only cancel their own bookings."));
    }

    public override async Task HandleAsync(CancelBookingRequestWithId req, CancellationToken ct)
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

        // Users can only cancel their own bookings unless they're an admin
        if (!isAdmin && booking.UserId != userId)
        {
            ThrowError("Booking not found", 404);
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            ThrowError("Booking is already cancelled", 400);
        }

        // Load the schedule to check timing and decrement bookings
        var schedule = await session.LoadAsync<Schedule>(booking.ScheduleId, ct);
        if (schedule is null)
        {
            ThrowError("Associated schedule not found", 500);
        }

        // Optional: Prevent cancellation after schedule has started
        if (schedule.StartTime <= DateTime.UtcNow)
        {
            ThrowError("Cannot cancel a booking for a schedule that has already started", 400);
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        booking.CancellationReason = req.Cancellation.CancellationReason;

        schedule.CurrentBookings--;

        session.Update(booking);
        session.Update(schedule);

        NotificationService.NotifyBookingCancelled(session, booking, schedule);

        await session.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Cancelled booking {BookingId} for schedule {ScheduleId} by user {UserId}",
            booking.Id, booking.ScheduleId, userId);

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
