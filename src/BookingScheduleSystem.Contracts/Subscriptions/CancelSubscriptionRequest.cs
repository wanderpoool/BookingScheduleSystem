namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record CancelSubscriptionRequest
{
    public string? CancellationReason { get; init; }
}
