using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record SubscribeToPlanRequest
{
    public required SubscriptionPlanId PlanId { get; init; }
    public BillingCycleDto BillingCycle { get; init; } = BillingCycleDto.Monthly;
}

public enum BillingCycleDto
{
    Monthly,
    Yearly
}
