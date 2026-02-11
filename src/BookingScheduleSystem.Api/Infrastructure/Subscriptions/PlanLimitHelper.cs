using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using Marten;

namespace BookingScheduleSystem.Api.Infrastructure.Subscriptions;

/// <summary>
/// Shared helper for resolving plan limits from trial status or active subscription.
/// </summary>
public static class PlanLimitHelper
{
    public static readonly PlanLimits TrialLimits = new()
    {
        MaxBookingsPerDay = 2,
        MaxBookingsPerMonth = 60,
        MaxConcurrentBookings = 5,
        MaxSchedulesPerDay = 5,
        MaxSchedulesPerMonth = 150,
        MaxUsers = 5,
        MaxProviders = 1,
        MaxBranches = 1,
        AllowMultipleBranches = false,
        AllowCustomBranding = false,
        AllowApiAccess = false,
        AllowAdvancedReporting = false,
        AllowPrioritySupport = false
    };

    /// <summary>
    /// Resolves the current plan limits for a tenant, either from trial defaults or active subscription.
    /// </summary>
    public static async Task<(PlanLimits? Limits, TenantSubscription? Subscription)> GetCurrentLimitsAsync(
        IQuerySession session,
        Tenant tenant,
        CancellationToken ct)
    {
        if (tenant.IsInTrial)
        {
            return (TrialLimits, null);
        }

        var subscription = await session.Query<TenantSubscription>()
            .FirstOrDefaultAsync(s => s.TenantId == tenant.Id && s.Status == SubscriptionStatus.Active, token: ct);

        if (subscription is null)
        {
            return (null, null);
        }

        var plan = await session.LoadAsync<SubscriptionPlan>(subscription.PlanId, ct);
        return (plan?.Limits, subscription);
    }
}
