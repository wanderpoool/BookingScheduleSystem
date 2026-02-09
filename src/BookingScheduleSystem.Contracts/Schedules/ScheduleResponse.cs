using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Schedules;

public sealed record ScheduleResponse
{
    public required ScheduleId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public UserId? ProviderId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int MaxCapacity { get; init; }
    public int CurrentBookings { get; init; }
    public int AvailableCapacity => MaxCapacity - CurrentBookings;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
}
