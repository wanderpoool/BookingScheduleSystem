using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Subscriptions;

public sealed class CancelSubscription : Endpoint<CancelSubscriptionRequest, TenantSubscriptionResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/subscriptions/cancel");
        Description(d => d
            .WithTags("Subscriptions")
            .WithSummary("Cancel subscription")
            .WithDescription("Cancels the current active subscription. Access continues until the end of the current billing period."));
    }

    public override async Task HandleAsync(CancelSubscriptionRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        // Get current user's tenant
        var tenantIdClaim = User.FindFirst("TenantId")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim))
        {
            ThrowError("Tenant context is required", 400);
        }

        var tenantId = TenantId.Parse(tenantIdClaim);

        // Get active subscription
        var subscription = await session.Query<TenantSubscription>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId
                && s.Status == SubscriptionStatus.Active, token: ct);

        if (subscription is null)
        {
            ThrowError("No active subscription found", 404);
        }

        // Load plan details
        var plan = await session.LoadAsync<SubscriptionPlan>(subscription.PlanId, ct);
        if (plan is null)
        {
            ThrowError("Subscription plan not found", 404);
        }

        // Mark as cancelled but keep active until end date
        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.CancellationReason = req.CancellationReason;
        subscription.UpdatedAt = DateTime.UtcNow;

        session.Update(subscription);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Subscription {SubscriptionId} cancelled for tenant {TenantId}. Access continues until {EndDate}",
            subscription.Id, tenantId, subscription.EndDate);

        Response = new TenantSubscriptionResponse
        {
            Id = subscription.Id,
            TenantId = subscription.TenantId,
            PlanId = subscription.PlanId,
            PlanName = plan.Name,
            Status = SubscriptionStatusDto.Cancelled,
            BillingCycle = subscription.BillingCycle == BillingCycle.Monthly
                ? BillingCycleDto.Monthly
                : BillingCycleDto.Yearly,
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            CancelledAt = subscription.CancelledAt,
            CancellationReason = subscription.CancellationReason,
            TrialEndDate = subscription.TrialEndDate,
            IsTrialConverted = subscription.IsTrialConverted,
            CreatedAt = subscription.CreatedAt,
            UpdatedAt = subscription.UpdatedAt,
            CurrentUsage = new UsageStatsDto
            {
                BookingsThisMonth = subscription.CurrentUsage.BookingsThisMonth,
                BookingsToday = subscription.CurrentUsage.BookingsToday,
                SchedulesThisMonth = subscription.CurrentUsage.SchedulesThisMonth,
                SchedulesToday = subscription.CurrentUsage.SchedulesToday,
                ActiveUsers = subscription.CurrentUsage.ActiveUsers,
                ActiveProviders = subscription.CurrentUsage.ActiveProviders,
                ActiveBranches = subscription.CurrentUsage.ActiveBranches,
                LastUpdated = subscription.CurrentUsage.LastUpdated
            },
            PlanLimits = new PlanLimitsDto
            {
                MaxBookingsPerDay = plan.Limits.MaxBookingsPerDay,
                MaxBookingsPerMonth = plan.Limits.MaxBookingsPerMonth,
                MaxConcurrentBookings = plan.Limits.MaxConcurrentBookings,
                MaxSchedulesPerDay = plan.Limits.MaxSchedulesPerDay,
                MaxSchedulesPerMonth = plan.Limits.MaxSchedulesPerMonth,
                MaxUsers = plan.Limits.MaxUsers,
                MaxProviders = plan.Limits.MaxProviders,
                MaxBranches = plan.Limits.MaxBranches,
                AllowMultipleBranches = plan.Limits.AllowMultipleBranches,
                AllowCustomBranding = plan.Limits.AllowCustomBranding,
                AllowApiAccess = plan.Limits.AllowApiAccess,
                AllowAdvancedReporting = plan.Limits.AllowAdvancedReporting,
                AllowPrioritySupport = plan.Limits.AllowPrioritySupport
            }
        };
    }
}
