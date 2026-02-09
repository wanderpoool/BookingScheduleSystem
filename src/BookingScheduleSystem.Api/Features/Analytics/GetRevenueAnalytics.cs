using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Analytics;

public sealed class RevenueAnalyticsResponse
{
    public required decimal MonthlyRecurringRevenue { get; init; }
    public required decimal AnnualRecurringRevenue { get; init; }
    public required int ActiveSubscriptions { get; init; }
    public required int CancelledSubscriptions { get; init; }
    public required int ExpiredSubscriptions { get; init; }
    public required int TrialConversions { get; init; }
    public required List<RevenueByPlanDto> RevenueByPlan { get; init; }
    public required List<RevenueByStatusDto> RevenueByStatus { get; init; }
}

public sealed class RevenueByPlanDto
{
    public required string PlanName { get; init; }
    public required int SubscriptionCount { get; init; }
    public required decimal MonthlyRevenue { get; init; }
    public required decimal AnnualRevenue { get; init; }
}

public sealed class RevenueByStatusDto
{
    public required string Status { get; init; }
    public required int Count { get; init; }
}

public sealed class GetRevenueAnalytics : EndpointWithoutRequest<RevenueAnalyticsResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/analytics/revenue");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("Analytics")
            .WithSummary("Get revenue analytics (Admin)")
            .WithDescription("Retrieves comprehensive revenue analytics including MRR, ARR, and breakdown by plan. Admin only."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        // Get all subscriptions
        var allSubscriptions = await session.Query<TenantSubscription>()
            .ToListAsync(ct);

        // Get active subscriptions
        var activeSubscriptions = allSubscriptions
            .Where(s => s.Status == SubscriptionStatus.Active)
            .ToList();

        // Calculate MRR and ARR
        var mrr = 0m;
        var arr = 0m;

        // Load all plans
        var planIdGuids = allSubscriptions.Select(s => s.PlanId.Value).Distinct().ToArray();
        var plans = await session.Query<SubscriptionPlan>()
            .Where(p => planIdGuids.Contains(p.Id.Value))
            .ToListAsync(ct);
        var planDict = plans.ToDictionary(p => p.Id, p => p);

        foreach (var subscription in activeSubscriptions)
        {
            if (planDict.TryGetValue(subscription.PlanId, out var plan))
            {
                if (subscription.BillingCycle == BillingCycle.Monthly)
                {
                    mrr += plan.PricePerMonth;
                    arr += plan.PricePerMonth * 12;
                }
                else // Yearly
                {
                    mrr += plan.PricePerYear / 12;
                    arr += plan.PricePerYear;
                }
            }
        }

        // Revenue by plan
        var revenueByPlan = activeSubscriptions
            .GroupBy(s => s.PlanId)
            .Select(group =>
            {
                var plan = planDict.GetValueOrDefault(group.Key);
                var monthlyRevenue = 0m;
                var annualRevenue = 0m;

                foreach (var sub in group)
                {
                    if (plan is not null)
                    {
                        if (sub.BillingCycle == BillingCycle.Monthly)
                        {
                            monthlyRevenue += plan.PricePerMonth;
                            annualRevenue += plan.PricePerMonth * 12;
                        }
                        else
                        {
                            monthlyRevenue += plan.PricePerYear / 12;
                            annualRevenue += plan.PricePerYear;
                        }
                    }
                }

                return new RevenueByPlanDto
                {
                    PlanName = plan?.Name ?? "Unknown",
                    SubscriptionCount = group.Count(),
                    MonthlyRevenue = monthlyRevenue,
                    AnnualRevenue = annualRevenue
                };
            })
            .OrderByDescending(r => r.MonthlyRevenue)
            .ToList();

        // Revenue by status
        var revenueByStatus = allSubscriptions
            .GroupBy(s => s.Status)
            .Select(group => new RevenueByStatusDto
            {
                Status = group.Key.ToString(),
                Count = group.Count()
            })
            .OrderByDescending(r => r.Count)
            .ToList();

        // Count trial conversions
        var trialConversions = allSubscriptions.Count(s => s.IsTrialConverted);

        Response = new RevenueAnalyticsResponse
        {
            MonthlyRecurringRevenue = mrr,
            AnnualRecurringRevenue = arr,
            ActiveSubscriptions = activeSubscriptions.Count,
            CancelledSubscriptions = allSubscriptions.Count(s => s.Status == SubscriptionStatus.Cancelled),
            ExpiredSubscriptions = allSubscriptions.Count(s => s.Status == SubscriptionStatus.Expired),
            TrialConversions = trialConversions,
            RevenueByPlan = revenueByPlan,
            RevenueByStatus = revenueByStatus
        };
    }
}
