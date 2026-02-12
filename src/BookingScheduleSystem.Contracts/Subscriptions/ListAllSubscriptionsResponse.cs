namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record ListAllSubscriptionsResponse
{
    public required List<TenantSubscriptionResponse> Subscriptions { get; init; }
    public int TotalCount { get; init; }
    public int PageNumber { get; init; }
    public int PageSize { get; init; }
}
