using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.Subscriptions;

/// <summary>
/// Links a tenant to a subscription plan with billing information
/// </summary>
public sealed class TenantSubscription
{
    public TenantSubscriptionId Id { get; set; }
    public required TenantId TenantId { get; set; }
    public required SubscriptionPlanId PlanId { get; set; }

    public required SubscriptionStatus Status { get; set; }
    public required BillingCycle BillingCycle { get; set; }

    public required DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime? TrialEndDate { get; set; }
    public bool IsTrialConverted { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Usage tracking for the current billing period
    public required UsageStats CurrentUsage { get; set; }
    public DateTime UsageResetDate { get; set; }
}

public enum SubscriptionStatus
{
    Active,
    Expired,
    Cancelled,
    PastDue,
    Suspended
}

public enum BillingCycle
{
    Monthly,
    Yearly
}

/// <summary>
/// Tracks current usage against plan limits
/// </summary>
public sealed class UsageStats
{
    public int BookingsThisMonth { get; set; }
    public int BookingsToday { get; set; }
    public int SchedulesThisMonth { get; set; }
    public int SchedulesToday { get; set; }
    public int ActiveUsers { get; set; }
    public int ActiveProviders { get; set; }
    public int ActiveBranches { get; set; }
    public DateTime LastUpdated { get; set; }
}
