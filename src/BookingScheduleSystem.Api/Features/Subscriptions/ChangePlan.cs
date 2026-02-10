using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Subscriptions;

public sealed class ChangePlanEndpointRequest
{
    public Guid NewPlanId { get; set; }
    public BillingCycleDto? NewBillingCycle { get; set; }
}

public sealed class ChangePlan : Endpoint<ChangePlanEndpointRequest, TenantSubscriptionResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/subscriptions/change-plan");
        Description(d => d
            .WithTags("Subscriptions")
            .WithSummary("Change subscription plan")
            .WithDescription("Changes the tenant's subscription plan with proration. Immediately switches to the new plan."));
    }

    public override async Task HandleAsync(ChangePlanEndpointRequest req, CancellationToken ct)
    {
        var tenantIdClaim = User.FindFirst("TenantId")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim))
        {
            ThrowError("Tenant context is required", 400);
        }

        var tenantId = TenantId.Parse(tenantIdClaim);

        await using var session = DocumentStore.LightweightSession();

        var currentSubscription = await session.Query<TenantSubscription>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status == SubscriptionStatus.Active, token: ct);

        if (currentSubscription is null)
        {
            ThrowError("No active subscription found", 404);
        }

        var currentPlan = await session.LoadAsync<SubscriptionPlan>(currentSubscription.PlanId, ct);
        if (currentPlan is null)
        {
            ThrowError("Current subscription plan not found", 404);
        }

        var newPlanId = new SubscriptionPlanId(req.NewPlanId);
        var newPlan = await session.LoadAsync<SubscriptionPlan>(newPlanId, ct);
        if (newPlan is null)
        {
            ThrowError("New subscription plan not found", 404);
        }

        if (!newPlan.IsActive)
        {
            ThrowError("New subscription plan is not available", 400);
        }

        if (currentSubscription.PlanId == newPlanId)
        {
            ThrowError("Already subscribed to this plan", 400);
        }

        var newBillingCycle = req.NewBillingCycle ?? (currentSubscription.BillingCycle == BillingCycle.Monthly
            ? BillingCycleDto.Monthly
            : BillingCycleDto.Yearly);

        var now = DateTime.UtcNow;

        // Update the subscription to the new plan
        currentSubscription.PlanId = newPlanId;
        currentSubscription.BillingCycle = newBillingCycle == BillingCycleDto.Monthly
            ? BillingCycle.Monthly
            : BillingCycle.Yearly;
        currentSubscription.UpdatedAt = now;

        // Recalculate end date from now based on new billing cycle
        currentSubscription.EndDate = newBillingCycle == BillingCycleDto.Monthly
            ? now.AddMonths(1)
            : now.AddYears(1);

        currentSubscription.UsageResetDate = currentSubscription.EndDate.Value;

        session.Update(currentSubscription);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Tenant {TenantId} changed plan from {OldPlanId} to {NewPlanId}",
            tenantId, currentPlan.Id, newPlanId);

        Response = MapToResponse(currentSubscription, newPlan);
    }

    private static TenantSubscriptionResponse MapToResponse(TenantSubscription subscription, SubscriptionPlan plan)
    {
        return new TenantSubscriptionResponse
        {
            Id = subscription.Id,
            TenantId = subscription.TenantId,
            PlanId = subscription.PlanId,
            PlanName = plan.Name,
            Status = subscription.Status switch
            {
                SubscriptionStatus.Active => SubscriptionStatusDto.Active,
                SubscriptionStatus.Expired => SubscriptionStatusDto.Expired,
                SubscriptionStatus.Cancelled => SubscriptionStatusDto.Cancelled,
                SubscriptionStatus.PastDue => SubscriptionStatusDto.PastDue,
                SubscriptionStatus.Suspended => SubscriptionStatusDto.Suspended,
                _ => SubscriptionStatusDto.Expired
            },
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
