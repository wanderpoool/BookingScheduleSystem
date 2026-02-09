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

        // Build query based on filters
        IReadOnlyList<TenantSubscription> subscriptions;
        long totalCount;

        if (req.Status.HasValue && req.PlanId.HasValue)
        {
            var status = MapStatus(req.Status.Value);
            totalCount = await session.Query<TenantSubscription>()
                .Where(s => s.Status == status && s.PlanId == req.PlanId.Value)
                .CountAsync(ct);

            subscriptions = await session.Query<TenantSubscription>()
                .Where(s => s.Status == status && s.PlanId == req.PlanId.Value)
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }
        else if (req.Status.HasValue)
        {
            var status = MapStatus(req.Status.Value);
            totalCount = await session.Query<TenantSubscription>()
                .Where(s => s.Status == status)
                .CountAsync(ct);

            subscriptions = await session.Query<TenantSubscription>()
                .Where(s => s.Status == status)
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }
        else if (req.PlanId.HasValue)
        {
            totalCount = await session.Query<TenantSubscription>()
                .Where(s => s.PlanId == req.PlanId.Value)
                .CountAsync(ct);

            subscriptions = await session.Query<TenantSubscription>()
                .Where(s => s.PlanId == req.PlanId.Value)
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }
        else
        {
            totalCount = await session.Query<TenantSubscription>()
                .CountAsync(ct);

            subscriptions = await session.Query<TenantSubscription>()
                .OrderByDescending(s => s.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);
        }

        // Load plan details for each subscription
        var planIdGuids = subscriptions.Select(s => s.PlanId.Value).Distinct().ToArray();
        var plans = await session.Query<SubscriptionPlan>()
            .Where(p => planIdGuids.Contains(p.Id.Value))
            .ToListAsync(ct);
        var planDict = plans.ToDictionary(p => p.Id, p => p);

        Response = new ListAllSubscriptionsResponse
        {
            Subscriptions = subscriptions.Select(s =>
            {
                var plan = planDict.GetValueOrDefault(s.PlanId);
                return new TenantSubscriptionResponse
                {
                    Id = s.Id,
                    TenantId = s.TenantId,
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
                };
            }).ToList(),
            TotalCount = (int)totalCount,
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
