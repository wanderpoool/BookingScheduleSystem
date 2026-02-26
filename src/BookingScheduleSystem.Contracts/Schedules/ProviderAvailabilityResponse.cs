using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Schedules;
public sealed record ProviderAvailabilityResponse
{
    public required UserId ProviderId { get; init; }
    public required string ProviderName { get; init; }
    public required List<DayAvailability> Days { get; init; }
}

public sealed record DayAvailability
{
    public required DateOnly Date { get; init; }
    public required string WorkingHoursStart { get; init; }
    public required string WorkingHoursEnd { get; init; }
    public required List<TimeBlock> TimeBlocks { get; init; }
}

public sealed record TimeBlock
{
    public required DateTime StartTime { get; init; }
    public required DateTime EndTime { get; init; }
    public required bool IsAvailable { get; init; }
    public bool IsActive { get; init; }
    public int MaxCapacity { get; init; }
    public int CurrentBookings { get; init; }
    public ScheduleId? ScheduleId { get; init; }
    public string? ScheduleTitle { get; init; }
    public string? CustomerName { get; init; }
    public string? BookingNotes { get; init; }
    public BookingId? BookingId { get; init; }
    public BookingStatus? BookingStatus { get; init; }
}
