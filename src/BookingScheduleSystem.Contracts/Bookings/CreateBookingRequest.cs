namespace BookingScheduleSystem.Contracts.Bookings;

public sealed record CreateBookingRequest
{
    public required Guid ScheduleId { get; init; }
    public string? Notes { get; init; }
}
