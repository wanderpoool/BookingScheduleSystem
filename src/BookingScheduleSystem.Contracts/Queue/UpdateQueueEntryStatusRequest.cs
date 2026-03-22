namespace BookingScheduleSystem.Contracts.Queue;

public sealed record UpdateQueueEntryStatusRequest
{
    public required QueueEntryStatus Status { get; init; }
}
