namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record UpdateSubscriptionPlanRequest
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public decimal PricePerMonth { get; init; }
    public decimal PricePerYear { get; init; }
    public required PlanLimitsDto Limits { get; init; }
    public bool IsActive { get; init; }
}
