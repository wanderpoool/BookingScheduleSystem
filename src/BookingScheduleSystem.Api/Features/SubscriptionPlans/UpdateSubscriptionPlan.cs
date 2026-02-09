using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.SubscriptionPlans;

public sealed class UpdateSubscriptionPlanRouteRequest
{
    public Guid Id { get; set; }
}

public sealed class UpdateSubscriptionPlan : Endpoint<UpdateSubscriptionPlanRequest, SubscriptionPlanResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Put("/api/subscription-plans/{id}");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("SubscriptionPlans")
            .WithSummary("Update a subscription plan")
            .WithDescription("Updates an existing subscription plan. Admin only."));
    }

    public override async Task HandleAsync(UpdateSubscriptionPlanRequest req, CancellationToken ct)
    {
        var routeId = Route<UpdateSubscriptionPlanRouteRequest>("id")!.Id;
        await using var session = DocumentStore.LightweightSession();

        var planId = new SubscriptionPlanId(routeId);
        var plan = await session.LoadAsync<SubscriptionPlan>(planId, ct);

        if (plan is null)
        {
            ThrowError("Subscription plan not found", 404);
        }

        // Check for name conflicts with other plans
        var existingPlan = await session.Query<SubscriptionPlan>()
            .FirstOrDefaultAsync(p => p.Name == req.Name && p.Id != planId, token: ct);

        if (existingPlan is not null)
        {
            ThrowError("A subscription plan with this name already exists", 409);
        }

        plan.Name = req.Name;
        plan.Description = req.Description;
        plan.PricePerMonth = req.PricePerMonth;
        plan.PricePerYear = req.PricePerYear;
        plan.Limits = new PlanLimits
        {
            MaxBookingsPerDay = req.Limits.MaxBookingsPerDay,
            MaxBookingsPerMonth = req.Limits.MaxBookingsPerMonth,
            MaxConcurrentBookings = req.Limits.MaxConcurrentBookings,
            MaxSchedulesPerDay = req.Limits.MaxSchedulesPerDay,
            MaxSchedulesPerMonth = req.Limits.MaxSchedulesPerMonth,
            MaxUsers = req.Limits.MaxUsers,
            MaxProviders = req.Limits.MaxProviders,
            MaxBranches = req.Limits.MaxBranches,
            AllowMultipleBranches = req.Limits.AllowMultipleBranches,
            AllowCustomBranding = req.Limits.AllowCustomBranding,
            AllowApiAccess = req.Limits.AllowApiAccess,
            AllowAdvancedReporting = req.Limits.AllowAdvancedReporting,
            AllowPrioritySupport = req.Limits.AllowPrioritySupport
        };
        plan.IsActive = req.IsActive;
        plan.UpdatedAt = DateTime.UtcNow;

        session.Update(plan);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation("Updated subscription plan {PlanId}", plan.Id);

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
