using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record TenantSubscriptionResponse
{
    public required TenantSubscriptionId Id { get; init; }
    public required TenantId TenantId { get; init; }
    public string? TenantName { get; init; }
    public required SubscriptionPlanId PlanId { get; init; }
    public required string PlanName { get; init; }
    public required SubscriptionStatusDto Status { get; init; }
    public required BillingCycleDto BillingCycle { get; init; }
    public required DateTime StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancellationReason { get; init; }
    public DateTime? TrialEndDate { get; init; }
    public bool IsTrialConverted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public required UsageStatsDto CurrentUsage { get; init; }
    public required PlanLimitsDto PlanLimits { get; init; }
}

public enum SubscriptionStatusDto
{
    Active,
    Expired,
    Cancelled,
    PastDue,
    Suspended,
    Trial
}

public sealed record UsageStatsDto
{
    public int BookingsThisMonth { get; init; }
    public int BookingsToday { get; init; }
    public int SchedulesThisMonth { get; init; }
    public int SchedulesToday { get; init; }
    public int ActiveUsers { get; init; }
    public int ActiveProviders { get; init; }
    public int ActiveBranches { get; init; }
    public DateTime LastUpdated { get; init; }
}
