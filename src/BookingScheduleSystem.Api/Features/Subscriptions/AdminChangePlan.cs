using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Subscriptions;

public sealed class AdminChangePlan : Endpoint<AdminChangePlanRequest, TenantSubscriptionResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/admin/subscriptions/change-plan");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("Admin Subscriptions")
            .WithSummary("Admin: Change a tenant's subscription plan")
            .WithDescription("Allows a GlobalAdmin to change any tenant's subscription plan."));
    }

    public override async Task HandleAsync(AdminChangePlanRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var subscription = await session.Query<TenantSubscription>()
            .FirstOrDefaultAsync(s => s.TenantId == req.TenantId
                && (s.Status == SubscriptionStatus.Active
                    || s.Status == SubscriptionStatus.PastDue
                    || s.Status == SubscriptionStatus.Suspended), token: ct);

        if (subscription is null)
        {
            ThrowError("No active subscription found for this tenant", 404);
        }

        var newPlan = await session.LoadAsync<SubscriptionPlan>(req.NewPlanId, ct);
        if (newPlan is null)
        {
            ThrowError("Subscription plan not found", 404);
        }

        if (!newPlan.IsActive)
        {
            ThrowError("Subscription plan is not available", 400);
        }

        if (subscription.PlanId == req.NewPlanId)
        {
            ThrowError("Tenant is already on this plan", 400);
        }

        var now = DateTime.UtcNow;
        var newBillingCycle = req.NewBillingCycle ?? (subscription.BillingCycle == BillingCycle.Monthly
            ? BillingCycleDto.Monthly
            : BillingCycleDto.Yearly);

        subscription.PlanId = req.NewPlanId;
        subscription.BillingCycle = newBillingCycle == BillingCycleDto.Monthly
            ? BillingCycle.Monthly
            : BillingCycle.Yearly;
        subscription.UpdatedAt = now;
        subscription.EndDate = newBillingCycle == BillingCycleDto.Monthly
            ? now.AddMonths(1)
            : now.AddYears(1);
        subscription.UsageResetDate = subscription.EndDate.Value;

        session.Update(subscription);
        await session.SaveChangesAsync(ct);

        var tenant = await session.Query<Tenant>()
            .FirstOrDefaultAsync(t => t.Id == req.TenantId, token: ct);

        Logger.LogInformation(
            "GlobalAdmin changed plan for tenant {TenantId} to {NewPlanId}. Reason: {Reason}",
            req.TenantId, req.NewPlanId, req.Reason ?? "N/A");

        Response = MapToResponse(subscription, newPlan, tenant?.Name);
    }

    private static TenantSubscriptionResponse MapToResponse(
        TenantSubscription subscription, SubscriptionPlan plan, string? tenantName)
    {
        return new TenantSubscriptionResponse
        {
            Id = subscription.Id,
            TenantId = subscription.TenantId,
            TenantName = tenantName,
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
