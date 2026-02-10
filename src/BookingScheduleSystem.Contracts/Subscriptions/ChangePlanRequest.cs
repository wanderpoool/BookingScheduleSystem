using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record ChangePlanRequest
{
    public required SubscriptionPlanId NewPlanId { get; init; }
    public BillingCycleDto? NewBillingCycle { get; init; }
}
