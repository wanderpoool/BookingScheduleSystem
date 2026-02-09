using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.Subscriptions;

/// <summary>
/// Subscription plan template defining features and limits
/// </summary>
public sealed class SubscriptionPlan
{
    public SubscriptionPlanId Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required decimal PricePerMonth { get; set; }
    public required decimal PricePerYear { get; set; }
    public required PlanLimits Limits { get; set; }
    public bool IsActive { get; set; }
    public bool IsCustomPlan { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Defines the limits and features for a subscription plan
/// </summary>
public sealed class PlanLimits
{
    // Booking limits
    public int MaxBookingsPerDay { get; set; }
    public int MaxBookingsPerMonth { get; set; }
    public int MaxConcurrentBookings { get; set; }

    // Schedule limits
    public int MaxSchedulesPerDay { get; set; }
    public int MaxSchedulesPerMonth { get; set; }

    // User limits
    public int MaxUsers { get; set; }
    public int MaxProviders { get; set; }

    // Organization limits
    public int MaxBranches { get; set; }

    // Feature flags
    public bool AllowMultipleBranches { get; set; }
    public bool AllowCustomBranding { get; set; }
    public bool AllowApiAccess { get; set; }
    public bool AllowAdvancedReporting { get; set; }
    public bool AllowPrioritySupport { get; set; }
}
