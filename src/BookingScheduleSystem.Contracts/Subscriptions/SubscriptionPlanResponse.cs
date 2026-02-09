using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record SubscriptionPlanResponse
{
    public required SubscriptionPlanId Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public decimal PricePerMonth { get; init; }
    public decimal PricePerYear { get; init; }
    public required PlanLimitsDto Limits { get; init; }
    public bool IsActive { get; init; }
    public bool IsCustomPlan { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
