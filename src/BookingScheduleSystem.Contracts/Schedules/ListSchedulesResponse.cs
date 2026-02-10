namespace BookingScheduleSystem.Contracts.Schedules;

public sealed record ListSchedulesResponse
{
    public required List<ProviderAvailabilityResponse> Providers { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}
