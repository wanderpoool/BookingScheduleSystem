using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Subscriptions;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.SubscriptionPlans;

public sealed class CreateSubscriptionPlan : Endpoint<CreateSubscriptionPlanRequest, SubscriptionPlanResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/subscription-plans");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("SubscriptionPlans")
            .WithSummary("Create a subscription plan")
            .WithDescription("Creates a new subscription plan. Admin only."));
    }

    public override async Task HandleAsync(CreateSubscriptionPlanRequest req, CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        // Check for plan name uniqueness
        var existingPlan = await session.Query<SubscriptionPlan>()
            .FirstOrDefaultAsync(p => p.Name == req.Name, token: ct);

        if (existingPlan is not null)
        {
            ThrowError("A subscription plan with this name already exists", 409);
        }

        var plan = new SubscriptionPlan
        {
            Id = SubscriptionPlanId.New(),
            Name = req.Name,
            Description = req.Description,
            PricePerMonth = req.PricePerMonth,
            PricePerYear = req.PricePerYear,
            Limits = new PlanLimits
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
            },
            IsActive = true,
            IsCustomPlan = req.IsCustomPlan,
            CreatedAt = DateTime.UtcNow
        };

        session.Store(plan);
        await session.SaveChangesAsync(ct);

        Logger.LogInformation("Created subscription plan {PlanId} with name {PlanName}", plan.Id, plan.Name);

        HttpContext.Response.StatusCode = 201;
        Response = MapToResponse(plan);
    }

    private static SubscriptionPlanResponse MapToResponse(SubscriptionPlan plan)
    {
        return new SubscriptionPlanResponse
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
