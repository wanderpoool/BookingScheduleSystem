namespace BookingScheduleSystem.Contracts.Queue;

public sealed record ReorderQueueEntryRequest
{
    public required int NewPosition { get; init; }
}
