namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record CreateSubscriptionPlanRequest
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public decimal PricePerMonth { get; init; }
    public decimal PricePerYear { get; init; }
    public required PlanLimitsDto Limits { get; init; }
    public bool IsCustomPlan { get; init; } = false;
}

public sealed record PlanLimitsDto
{
    public int MaxBookingsPerDay { get; init; }
    public int MaxBookingsPerMonth { get; init; }
    public int MaxConcurrentBookings { get; init; }
    public int MaxSchedulesPerDay { get; init; }
    public int MaxSchedulesPerMonth { get; init; }
    public int MaxUsers { get; init; }
    public int MaxProviders { get; init; }
    public int MaxBranches { get; init; }
    public bool AllowMultipleBranches { get; init; }
    public bool AllowCustomBranding { get; init; }
    public bool AllowApiAccess { get; init; }
    public bool AllowAdvancedReporting { get; init; }
    public bool AllowPrioritySupport { get; init; }
}
