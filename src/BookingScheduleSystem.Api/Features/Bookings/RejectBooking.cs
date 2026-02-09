using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Bookings;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Bookings;

public sealed class RejectBookingRequestWithReason
{
    public Guid BookingId { get; set; }
    public string? Reason { get; set; }
}

public sealed class RejectBooking : Endpoint<RejectBookingRequestWithReason, BookingResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Post("/api/bookings/{BookingId}/reject");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Bookings")
            .WithSummary("Reject a pending booking")
            .WithDescription("Rejects a pending booking. Only the schedule's provider or admin can reject."));
    }

    public override async Task HandleAsync(RejectBookingRequestWithReason req, CancellationToken ct)
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

        if (booking.Status != BookingStatus.Pending)
        {
            ThrowError($"Booking is not pending. Current status: {booking.Status}", 400);
        }

        // Load the schedule to check if user is the provider
        var schedule = await session.LoadAsync<Schedule>(booking.ScheduleId, ct);
        if (schedule is null)
        {
            ThrowError("Associated schedule not found", 500);
        }

        // Only the provider or admin can reject
        if (!isAdmin && schedule.ProviderId != currentUserId)
        {
            ThrowError("Only the schedule provider or admin can reject this booking", 403);
        }

        booking.Status = BookingStatus.Cancelled;
        booking.CancelledAt = DateTime.UtcNow;
        booking.CancellationReason = req.Reason ?? "Rejected by provider";

        // Decrement the schedule's current bookings since the booking is rejected
        schedule.CurrentBookings--;

        session.Update(booking);
        session.Update(schedule);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Rejected booking {BookingId} for schedule {ScheduleId} by user {UserId}",
            booking.Id, booking.ScheduleId, currentUserId);

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
