using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Subscriptions;

public sealed class AdminReactivateSubscription : Endpoint<AdminReactivateSubscriptionRequest, TenantSubscriptionResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/admin/subscriptions/reactivate");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("Admin Subscriptions")
            .WithSummary("Admin: Reactivate a suspended subscription")
            .WithDescription("Allows a GlobalAdmin to reactivate a suspended tenant subscription."));
    }

    public override async Task HandleAsync(AdminReactivateSubscriptionRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var subscription = await session.Query<TenantSubscription>()
            .FirstOrDefaultAsync(s => s.TenantId == req.TenantId
                && s.Status == SubscriptionStatus.Suspended, token: ct);

        if (subscription is null)
        {
            ThrowError("No suspended subscription found for this tenant", 404);
        }

        var now = DateTime.UtcNow;
        subscription.Status = SubscriptionStatus.Active;
        subscription.CancellationReason = null;
        subscription.UpdatedAt = now;

        // Reset end date from now
        subscription.EndDate = subscription.BillingCycle == BillingCycle.Monthly
            ? now.AddMonths(1)
            : now.AddYears(1);
        subscription.UsageResetDate = subscription.EndDate.Value;

        session.Update(subscription);
        await session.SaveChangesAsync(ct);

        var plan = await session.LoadAsync<SubscriptionPlan>(subscription.PlanId, ct);
        var tenant = await session.Query<Tenant>()
            .FirstOrDefaultAsync(t => t.Id == req.TenantId, token: ct);

        Logger.LogInformation(
            "GlobalAdmin reactivated subscription for tenant {TenantId}. Reason: {Reason}",
            req.TenantId, req.Reason ?? "N/A");

        Response = MapToResponse(subscription, plan, tenant?.Name);
    }

    private static TenantSubscriptionResponse MapToResponse(
        TenantSubscription subscription, SubscriptionPlan? plan, string? tenantName)
    {
        return new TenantSubscriptionResponse
        {
            Id = subscription.Id,
            TenantId = subscription.TenantId,
            TenantName = tenantName,
            PlanId = subscription.PlanId,
            PlanName = plan?.Name ?? "Unknown",
            Status = SubscriptionStatusDto.Active,
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
            PlanLimits = plan is not null ? new PlanLimitsDto
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
            } : new PlanLimitsDto()
        };
    }
}
