using System.Security.Claims;
using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Subscriptions;

public sealed class SubscribeToPlan : Endpoint<SubscribeToPlanRequest, TenantSubscriptionResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/subscriptions/subscribe");
        Description(d => d
            .WithTags("Subscriptions")
            .WithSummary("Subscribe to a plan")
            .WithDescription("Subscribes the tenant to a subscription plan. Converts from trial if applicable."));
    }

    public override async Task HandleAsync(SubscribeToPlanRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        // Get current user's tenant
        var tenantIdClaim = User.FindFirst("TenantId")?.Value;
        if (string.IsNullOrEmpty(tenantIdClaim))
        {
            ThrowError("Tenant context is required", 400);
        }

        var tenantId = TenantId.Parse(tenantIdClaim);
        var tenant = await session.LoadAsync<Tenant>(tenantId, ct);

        if (tenant is null)
        {
            ThrowError("Tenant not found", 404);
        }

        // Verify plan exists and is active
        var plan = await session.LoadAsync<SubscriptionPlan>(req.PlanId, ct);
        if (plan is null)
        {
            ThrowError("Subscription plan not found", 404);
        }

        if (!plan.IsActive)
        {
            ThrowError("This subscription plan is not available", 400);
        }

        // Check if tenant already has an active subscription
        var existingSubscription = await session.Query<TenantSubscription>()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId
                && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.PastDue), token: ct);

        if (existingSubscription is not null)
        {
            ThrowError("Tenant already has an active subscription", 409);
        }

        var startDate = DateTime.UtcNow;
        var endDate = req.BillingCycle == BillingCycleDto.Monthly
            ? startDate.AddMonths(1)
            : startDate.AddYears(1);

        var subscription = new TenantSubscription
        {
            Id = TenantSubscriptionId.New(),
            TenantId = tenantId,
            PlanId = req.PlanId,
            Status = SubscriptionStatus.Active,
            BillingCycle = req.BillingCycle == BillingCycleDto.Monthly
                ? BillingCycle.Monthly
                : BillingCycle.Yearly,
            StartDate = startDate,
            EndDate = endDate,
            TrialEndDate = tenant.IsInTrial ? tenant.TrialEndDate : null,
            IsTrialConverted = tenant.IsInTrial,
            CreatedAt = DateTime.UtcNow,
            CurrentUsage = new UsageStats
            {
                BookingsThisMonth = 0,
                BookingsToday = 0,
                SchedulesThisMonth = 0,
                SchedulesToday = 0,
                ActiveUsers = 0,
                ActiveProviders = 0,
                ActiveBranches = 0,
                LastUpdated = DateTime.UtcNow
            },
            UsageResetDate = req.BillingCycle == BillingCycleDto.Monthly
                ? startDate.AddMonths(1)
                : startDate.AddYears(1)
        };

        session.Store(subscription);

        // Update tenant to mark trial as ended if applicable
        if (tenant.IsInTrial)
        {
            tenant.IsInTrial = false;
            session.Update(tenant);
        }

        await session.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Tenant {TenantId} subscribed to plan {PlanId} with {BillingCycle} billing",
            tenantId, req.PlanId, req.BillingCycle);

        HttpContext.Response.StatusCode = 201;
        Response = MapToResponse(subscription, plan);
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
