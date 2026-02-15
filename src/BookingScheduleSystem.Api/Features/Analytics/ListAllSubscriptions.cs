using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Analytics;

public sealed class ListAllSubscriptionsRequest
{
    public SubscriptionStatusDto? Status { get; set; }
    public SubscriptionPlanId? PlanId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class ListAllSubscriptionsResponse
{
    public required List<TenantSubscriptionResponse> Subscriptions { get; init; }
    public required int TotalCount { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
}

public sealed class ListAllSubscriptions : Endpoint<ListAllSubscriptionsRequest, ListAllSubscriptionsResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/analytics/subscriptions");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("Analytics")
            .WithSummary("List all subscriptions (Admin)")
            .WithDescription("Retrieves all tenant subscriptions with optional filtering by status or plan. Admin only."));
    }

    public override async Task HandleAsync(ListAllSubscriptionsRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var pageNumber = Math.Max(1, req.PageNumber);
        var pageSize = Math.Clamp(req.PageSize, 1, 100);

        // Load all subscriptions
        var allSubscriptions = await session.Query<TenantSubscription>()
            .ToListAsync(ct);

        // Load all tenants and plans
        var allTenants = await session.Query<Tenant>().ToListAsync(ct);
        var allPlans = await session.Query<SubscriptionPlan>().ToListAsync(ct);
        var planDict = allPlans.ToDictionary(p => p.Id, p => p);

        // Build unified list: subscriptions + trial tenants without subscriptions
        var subscribedTenantIds = allSubscriptions.Select(s => s.TenantId).ToHashSet();
        var results = new List<TenantSubscriptionResponse>();

        // Add subscription-backed entries
        foreach (var s in allSubscriptions)
        {
            var plan = planDict.GetValueOrDefault(s.PlanId);
            var tenant = allTenants.FirstOrDefault(t => t.Id == s.TenantId);
            results.Add(new TenantSubscriptionResponse
            {
                Id = s.Id,
                TenantId = s.TenantId,
                TenantName = tenant?.Name,
                PlanId = s.PlanId,
                PlanName = plan?.Name ?? "Unknown",
                Status = s.Status switch
                {
                    SubscriptionStatus.Active => SubscriptionStatusDto.Active,
                    SubscriptionStatus.Expired => SubscriptionStatusDto.Expired,
                    SubscriptionStatus.Cancelled => SubscriptionStatusDto.Cancelled,
                    SubscriptionStatus.PastDue => SubscriptionStatusDto.PastDue,
                    SubscriptionStatus.Suspended => SubscriptionStatusDto.Suspended,
                    _ => SubscriptionStatusDto.Expired
                },
                BillingCycle = s.BillingCycle == BillingCycle.Monthly
                    ? BillingCycleDto.Monthly
                    : BillingCycleDto.Yearly,
                StartDate = s.StartDate,
                EndDate = s.EndDate,
                CancelledAt = s.CancelledAt,
                CancellationReason = s.CancellationReason,
                TrialEndDate = s.TrialEndDate,
                IsTrialConverted = s.IsTrialConverted,
                CreatedAt = s.CreatedAt,
                UpdatedAt = s.UpdatedAt,
                CurrentUsage = new UsageStatsDto
                {
                    BookingsThisMonth = s.CurrentUsage.BookingsThisMonth,
                    BookingsToday = s.CurrentUsage.BookingsToday,
                    SchedulesThisMonth = s.CurrentUsage.SchedulesThisMonth,
                    SchedulesToday = s.CurrentUsage.SchedulesToday,
                    ActiveUsers = s.CurrentUsage.ActiveUsers,
                    ActiveProviders = s.CurrentUsage.ActiveProviders,
                    ActiveBranches = s.CurrentUsage.ActiveBranches,
                    LastUpdated = s.CurrentUsage.LastUpdated
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
            });
        }

        // Add trial tenants that don't have a subscription yet
        foreach (var tenant in allTenants.Where(t => t.IsInTrial && !subscribedTenantIds.Contains(t.Id)))
        {
            results.Add(new TenantSubscriptionResponse
            {
                Id = new TenantSubscriptionId(Guid.Empty),
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                PlanId = new SubscriptionPlanId(Guid.Empty),
                PlanName = "Free Trial",
                Status = SubscriptionStatusDto.Trial,
                BillingCycle = BillingCycleDto.Monthly,
                StartDate = tenant.TrialStartDate ?? tenant.CreatedAt,
                EndDate = tenant.TrialEndDate,
                TrialEndDate = tenant.TrialEndDate,
                CreatedAt = tenant.CreatedAt,
                CurrentUsage = new UsageStatsDto(),
                PlanLimits = new PlanLimitsDto()
            });
        }

        // Apply status filter
        if (req.Status.HasValue)
        {
            results = results.Where(r => r.Status == req.Status.Value).ToList();
        }

        // Apply plan filter
        if (req.PlanId.HasValue)
        {
            results = results.Where(r => r.PlanId == req.PlanId.Value).ToList();
        }

        // Sort by creation date descending and paginate
        var sorted = results.OrderByDescending(r => r.CreatedAt).ToList();
        var totalCount = sorted.Count;

        Response = new ListAllSubscriptionsResponse
        {
            Subscriptions = sorted
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList(),
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    private static SubscriptionStatus MapStatus(SubscriptionStatusDto status)
    {
        return status switch
        {
            SubscriptionStatusDto.Active => SubscriptionStatus.Active,
            SubscriptionStatusDto.Expired => SubscriptionStatus.Expired,
            SubscriptionStatusDto.Cancelled => SubscriptionStatus.Cancelled,
            SubscriptionStatusDto.PastDue => SubscriptionStatus.PastDue,
            SubscriptionStatusDto.Suspended => SubscriptionStatus.Suspended,
            _ => SubscriptionStatus.Active
        };
    }
}
