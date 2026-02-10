using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Bookings;

public sealed record BookingResponse
{
    public required BookingId Id { get; init; }
    public required ScheduleId ScheduleId { get; init; }
    public required UserId UserId { get; init; }
    public required TenantId TenantId { get; init; }
    public BookingStatus Status { get; init; }
    public string? Notes { get; init; }
    public DateTime BookedAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
    public string? ScheduleTitle { get; init; }
    public DateTime? ScheduleStartTime { get; init; }
    public DateTime? ScheduleEndTime { get; init; }
}
