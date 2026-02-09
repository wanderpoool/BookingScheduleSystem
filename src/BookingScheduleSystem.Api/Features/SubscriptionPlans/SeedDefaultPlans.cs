using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.SubscriptionPlans;

public sealed class SeedDefaultPlansResponse
{
    public required List<string> CreatedPlans { get; init; }
    public required List<string> SkippedPlans { get; init; }
}

public sealed class SeedDefaultPlans : EndpointWithoutRequest<SeedDefaultPlansResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/subscription-plans/seed-defaults");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("SubscriptionPlans")
            .WithSummary("Seed default subscription plans")
            .WithDescription("Creates the 4 default subscription plans (Starter, Growth, Business, Multi-Branch). Skips plans that already exist. Admin only."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var createdPlans = new List<string>();
        var skippedPlans = new List<string>();

        var defaultPlans = new[]
        {
            new
            {
                Name = "Starter",
                Description = "Perfect for small businesses getting started with appointment scheduling. Includes email reminders and basic support with 48-hour SLA.",
                PricePerMonth = 499m,
                PricePerYear = 5390m, // ~10% annual discount
                Limits = new PlanLimits
                {
                    MaxBookingsPerDay = 20,
                    MaxBookingsPerMonth = 500,
                    MaxConcurrentBookings = 100,
                    MaxSchedulesPerDay = 10,
                    MaxSchedulesPerMonth = 150,
                    MaxUsers = 5,
                    MaxProviders = 5,
                    MaxBranches = 1,
                    AllowMultipleBranches = false,
                    AllowCustomBranding = false,
                    AllowApiAccess = false,
                    AllowAdvancedReporting = false,
                    AllowPrioritySupport = false
                }
            },
            new
            {
                Name = "Growth",
                Description = "Ideal for growing businesses with multiple locations and providers. Includes email reminders, SMS (pay-per-SMS), logo branding, and 24-hour email support.",
                PricePerMonth = 999m,
                PricePerYear = 10790m, // ~10% annual discount
                Limits = new PlanLimits
                {
                    MaxBookingsPerDay = 70,
                    MaxBookingsPerMonth = 2000,
                    MaxConcurrentBookings = 400,
                    MaxSchedulesPerDay = 15,
                    MaxSchedulesPerMonth = 450,
                    MaxUsers = 15,
                    MaxProviders = 15,
                    MaxBranches = 3,
                    AllowMultipleBranches = true,
                    AllowCustomBranding = true,
                    AllowApiAccess = true,
                    AllowAdvancedReporting = true,
                    AllowPrioritySupport = false
                }
            },
            new
            {
                Name = "Business",
                Description = "For established businesses requiring advanced features and higher capacity. Includes email reminders, discounted SMS rates, full branding, and email + chat support.",
                PricePerMonth = 1999m,
                PricePerYear = 21590m, // ~10% annual discount
                Limits = new PlanLimits
                {
                    MaxBookingsPerDay = 250,
                    MaxBookingsPerMonth = 7500,
                    MaxConcurrentBookings = 1500,
                    MaxSchedulesPerDay = 40,
                    MaxSchedulesPerMonth = 1200,
                    MaxUsers = 40,
                    MaxProviders = 40,
                    MaxBranches = 5,
                    AllowMultipleBranches = true,
                    AllowCustomBranding = true,
                    AllowApiAccess = true,
                    AllowAdvancedReporting = true,
                    AllowPrioritySupport = false
                }
            },
            new
            {
                Name = "Multi-Branch",
                Description = "Enterprise solution for organizations with multiple locations. Includes email reminders, SMS with monthly cap, full branding, and priority support.",
                PricePerMonth = 3999m,
                PricePerYear = 43190m, // ~10% annual discount
                Limits = new PlanLimits
                {
                    MaxBookingsPerDay = 700,
                    MaxBookingsPerMonth = 20000,
                    MaxConcurrentBookings = 4000,
                    MaxSchedulesPerDay = 80,
                    MaxSchedulesPerMonth = 2400,
                    MaxUsers = 80,
                    MaxProviders = 80,
                    MaxBranches = 10,
                    AllowMultipleBranches = true,
                    AllowCustomBranding = true,
                    AllowApiAccess = true,
                    AllowAdvancedReporting = true,
                    AllowPrioritySupport = true
                }
            }
        };

        foreach (var planData in defaultPlans)
        {
            // Check if plan already exists
            var existingPlan = await session.Query<SubscriptionPlan>()
                .FirstOrDefaultAsync(p => p.Name == planData.Name, token: ct);

            if (existingPlan is not null)
            {
                skippedPlans.Add(planData.Name);
                Logger.LogInformation("Skipped creating plan {PlanName} - already exists", planData.Name);
                continue;
            }

            var plan = new SubscriptionPlan
            {
                Id = SubscriptionPlanId.New(),
                Name = planData.Name,
                Description = planData.Description,
                PricePerMonth = planData.PricePerMonth,
                PricePerYear = planData.PricePerYear,
                Limits = planData.Limits,
                IsActive = true,
                IsCustomPlan = false,
                CreatedAt = DateTime.UtcNow
            };

            session.Store(plan);
            createdPlans.Add(planData.Name);
            Logger.LogInformation("Created default plan {PlanName} with ID {PlanId}", plan.Name, plan.Id);
        }

        await session.SaveChangesAsync(ct);

        Logger.LogInformation(
            "Seeded {CreatedCount} default plans. Skipped {SkippedCount} existing plans.",
            createdPlans.Count, skippedPlans.Count);

        HttpContext.Response.StatusCode = 201;
        Response = new SeedDefaultPlansResponse
        {
            CreatedPlans = createdPlans,
            SkippedPlans = skippedPlans
        };
    }
}
