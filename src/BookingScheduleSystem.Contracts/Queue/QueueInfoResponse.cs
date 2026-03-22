namespace BookingScheduleSystem.Contracts.Queue;

public sealed record QueueInfoResponse
{
    public required string TenantName { get; init; }
    public required DateTime QueueDate { get; init; }
    public required int CurrentWaitingCount { get; init; }
    public required int AverageServiceTimeMinutes { get; init; }
}
