using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Queue;

public sealed record QueueEntryResponse
{
    public required QueueEntryId Id { get; init; }
    public required int QueueNumber { get; init; }
    public string? CustomerName { get; init; }
    public required string PhoneNumber { get; init; }
    public required QueueEntryStatus Status { get; init; }
    public required bool IsPriority { get; init; }
    public required int EstimatedWaitMinutes { get; init; }
    public required DateTime JoinedAt { get; init; }
    public DateTime? CalledAt { get; init; }
    public DateTime? CompletedAt { get; init; }
}
