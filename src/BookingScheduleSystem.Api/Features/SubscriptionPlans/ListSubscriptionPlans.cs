using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.SubscriptionPlans;

public sealed class ListSubscriptionPlansRequest
{
    public bool IncludeInactive { get; set; } = false;
}

public sealed class ListSubscriptionPlansResponse
{
    public required List<SubscriptionPlanResponse> Plans { get; init; }
}

public sealed class ListSubscriptionPlans : Endpoint<ListSubscriptionPlansRequest, ListSubscriptionPlansResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/subscription-plans");
        AllowAnonymous();
        Description(d => d
            .WithTags("SubscriptionPlans")
            .WithSummary("List all subscription plans")
            .WithDescription("Retrieves all active subscription plans. Optionally include inactive plans."));
    }

    public override async Task HandleAsync(ListSubscriptionPlansRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var plans = req.IncludeInactive
            ? await session.Query<SubscriptionPlan>()
                .OrderBy(p => p.PricePerMonth)
                .ToListAsync(ct)
            : await session.Query<SubscriptionPlan>()
                .Where(p => p.IsActive)
                .OrderBy(p => p.PricePerMonth)
                .ToListAsync(ct);

        Response = new ListSubscriptionPlansResponse
        {
            Plans = plans.Select(p => new SubscriptionPlanResponse
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                PricePerMonth = p.PricePerMonth,
                PricePerYear = p.PricePerYear,
                Limits = new PlanLimitsDto
                {
                    MaxBookingsPerDay = p.Limits.MaxBookingsPerDay,
                    MaxBookingsPerMonth = p.Limits.MaxBookingsPerMonth,
                    MaxConcurrentBookings = p.Limits.MaxConcurrentBookings,
                    MaxSchedulesPerDay = p.Limits.MaxSchedulesPerDay,
                    MaxSchedulesPerMonth = p.Limits.MaxSchedulesPerMonth,
                    MaxUsers = p.Limits.MaxUsers,
                    MaxProviders = p.Limits.MaxProviders,
                    MaxBranches = p.Limits.MaxBranches,
                    AllowMultipleBranches = p.Limits.AllowMultipleBranches,
                    AllowCustomBranding = p.Limits.AllowCustomBranding,
                    AllowApiAccess = p.Limits.AllowApiAccess,
                    AllowAdvancedReporting = p.Limits.AllowAdvancedReporting,
                    AllowPrioritySupport = p.Limits.AllowPrioritySupport
                },
                IsActive = p.IsActive,
                IsCustomPlan = p.IsCustomPlan,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            }).ToList()
        };
    }
}
