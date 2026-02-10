using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Schedules;

public sealed record PublicScheduleResponse
{
    public required ScheduleId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public UserId? ProviderId { get; init; }
    public string? ProviderName { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public DateTime StartTime { get; init; }
    public DateTime EndTime { get; init; }
    public int MaxCapacity { get; init; }
    public int CurrentBookings { get; init; }
    public int AvailableCapacity => MaxCapacity - CurrentBookings;
    public bool IsAvailabilityBlock { get; init; }
}

public sealed record ListPublicSchedulesResponse
{
    public required List<ProviderAvailabilityResponse> Providers { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}
