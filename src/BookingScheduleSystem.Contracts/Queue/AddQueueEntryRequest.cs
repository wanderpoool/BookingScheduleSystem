namespace BookingScheduleSystem.Contracts.Queue;

public sealed record AddQueueEntryRequest
{
    public required string PhoneNumber { get; init; }
    public required string CustomerName { get; init; }
    public bool IsPriority { get; init; }
}
