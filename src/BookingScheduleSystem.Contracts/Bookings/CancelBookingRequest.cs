namespace BookingScheduleSystem.Contracts.Bookings;

public sealed record CancelBookingRequest
{
    public string? CancellationReason { get; init; }
}
