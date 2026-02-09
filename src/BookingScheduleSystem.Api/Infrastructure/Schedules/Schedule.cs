using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.Schedules;

public sealed class Schedule
{
    public ScheduleId Id { get; set; }
    public required TenantId TenantId { get; set; }
    public UserId? ProviderId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int MaxCapacity { get; set; }
    public int CurrentBookings { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public UserId CreatedBy { get; set; }
}
