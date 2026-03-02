using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Bookings;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Bookings;

public sealed class UpdateBookingNotesEndpointRequest
{
    public Guid BookingId { get; set; }
    public string? Notes { get; set; }
}

public sealed class UpdateBookingNotes : Endpoint<UpdateBookingNotesEndpointRequest, BookingResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Patch("/api/bookings/{BookingId}/notes");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Bookings")
            .WithSummary("Update booking notes")
            .WithDescription("Updates the notes on a booking. Only the booking owner or admin can update."));
    }

    public override async Task HandleAsync(UpdateBookingNotesEndpointRequest req, CancellationToken ct)
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

        if (!isAdmin && booking.UserId != currentUserId)
        {
            ThrowError("You can only update notes on your own bookings", 403);
        }

        booking.Notes = req.Notes;
        session.Update(booking);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Updated notes on booking {BookingId} by user {UserId}",
            booking.Id, currentUserId);

        Response = new BookingResponse
        {
            Id = booking.Id,
            ScheduleId = booking.ScheduleId,
            UserId = booking.UserId,
            TenantId = booking.TenantId,
            Status = booking.Status,
            ReferenceNumber = booking.ReferenceNumber,
            Notes = booking.Notes,
            BookedAt = booking.BookedAt,
            CancelledAt = booking.CancelledAt,
            CancellationReason = booking.CancellationReason
        };
    }
}
