using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record AdminChangePlanRequest
{
    public required TenantId TenantId { get; init; }
    public required SubscriptionPlanId NewPlanId { get; init; }
    public BillingCycleDto? NewBillingCycle { get; init; }
    public string? Reason { get; init; }
}
