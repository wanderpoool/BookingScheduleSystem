using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Schedules;

public sealed record CreateScheduleRequest
{
    public Guid? ProviderId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int MaxCapacity { get; init; } = 1;
}
