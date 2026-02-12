namespace BookingScheduleSystem.Contracts.Bookings;

public sealed record UpdateBookingNotesRequest
{
    public string? Notes { get; init; }
}
