using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Queue;

public sealed record GetTodayQueueResponse
{
    public required QueueId Id { get; init; }
    public required string DailyToken { get; init; }
    public required DateTime QueueDate { get; init; }
    public required List<QueueEntryResponse> Entries { get; init; }
    public required int TotalWaiting { get; init; }
    public required int TotalCalled { get; init; }
    public required int TotalCompleted { get; init; }
}
