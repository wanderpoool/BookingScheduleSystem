using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.SubscriptionPlans;

public sealed class GetSubscriptionPlanRequest
{
    public Guid Id { get; set; }
}

public sealed class GetSubscriptionPlan : Endpoint<GetSubscriptionPlanRequest, SubscriptionPlanResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/subscription-plans/{id}");
        AllowAnonymous();
        Description(d => d
            .WithTags("SubscriptionPlans")
            .WithSummary("Get subscription plan by ID")
            .WithDescription("Retrieves a subscription plan by its identifier"));
    }

    public override async Task HandleAsync(GetSubscriptionPlanRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var planId = new SubscriptionPlanId(req.Id);
        var plan = await session.LoadAsync<SubscriptionPlan>(planId, ct);

        if (plan is null)
        {
            ThrowError("Subscription plan not found", 404);
        }

        Response = new SubscriptionPlanResponse
        {
            Id = plan.Id,
            Name = plan.Name,
            Description = plan.Description,
            PricePerMonth = plan.PricePerMonth,
            PricePerYear = plan.PricePerYear,
            Limits = new PlanLimitsDto
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
            },
            IsActive = plan.IsActive,
            IsCustomPlan = plan.IsCustomPlan,
            CreatedAt = plan.CreatedAt,
            UpdatedAt = plan.UpdatedAt
        };
    }
}
