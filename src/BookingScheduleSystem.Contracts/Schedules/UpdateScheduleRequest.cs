namespace BookingScheduleSystem.Contracts.Schedules;

public sealed record UpdateScheduleRequest
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public DateTime? StartTime { get; init; }
    public DateTime? EndTime { get; init; }
    public int? MaxCapacity { get; init; }
    public bool? IsActive { get; init; }
}
