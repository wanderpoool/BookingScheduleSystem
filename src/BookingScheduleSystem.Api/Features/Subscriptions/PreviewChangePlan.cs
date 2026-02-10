using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Subscriptions;

public sealed class PreviewChangePlanRequest
{
    public Guid NewPlanId { get; set; }
    public BillingCycleDto? NewBillingCycle { get; set; }
}

public sealed class PreviewChangePlan : Endpoint<PreviewChangePlanRequest, ProrationPreviewResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/subscriptions/preview-change");
        Description(d => d
            .WithTags("Subscriptions")
            .WithSummary("Preview plan change")
            .WithDescription("Previews the proration calculation for changing subscription plans without committing the change."));
    }

    public override async Task HandleAsync(PreviewChangePlanRequest req, CancellationToken ct)
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

        Response = CalculateProration(currentSubscription, currentPlan, newPlan, newBillingCycle);
    }

    internal static ProrationPreviewResponse CalculateProration(
        TenantSubscription subscription,
        SubscriptionPlan currentPlan,
        SubscriptionPlan newPlan,
        BillingCycleDto newBillingCycle)
    {
        var now = DateTime.UtcNow;
        var endDate = subscription.EndDate ?? now;
        var startDate = subscription.StartDate;

        var totalDays = Math.Max(1, (int)(endDate - startDate).TotalDays);
        var daysRemaining = Math.Max(0, (int)(endDate - now).TotalDays);

        var currentPrice = subscription.BillingCycle == BillingCycle.Monthly
            ? currentPlan.PricePerMonth
            : currentPlan.PricePerYear;

        var newPrice = newBillingCycle == BillingCycleDto.Monthly
            ? newPlan.PricePerMonth
            : newPlan.PricePerYear;

        // Prorate: credit for unused days on current plan, charge for remaining days on new plan
        var dailyRateCurrent = currentPrice / totalDays;
        var dailyRateNew = newPrice / totalDays;

        var proratedCredit = Math.Round(dailyRateCurrent * daysRemaining, 2);
        var proratedCharge = Math.Round(dailyRateNew * daysRemaining, 2);
        var netAmount = Math.Round(proratedCharge - proratedCredit, 2);

        var isUpgrade = newPrice > currentPrice;

        return new ProrationPreviewResponse
        {
            CurrentPlanName = currentPlan.Name,
            CurrentPlanPrice = currentPrice,
            NewPlanName = newPlan.Name,
            NewPlanPrice = newPrice,
            DaysRemaining = daysRemaining,
            TotalDaysInCycle = totalDays,
            ProratedCredit = proratedCredit,
            ProratedCharge = proratedCharge,
            NetAmount = netAmount,
            EffectiveDate = now,
            IsUpgrade = isUpgrade
        };
    }
}
