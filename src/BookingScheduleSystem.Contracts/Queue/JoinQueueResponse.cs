using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Queue;

public sealed record JoinQueueResponse
{
    public required QueueEntryId EntryId { get; init; }
    public required int QueueNumber { get; init; }
    public required int PositionInQueue { get; init; }
    public required int EstimatedWaitMinutes { get; init; }
}
