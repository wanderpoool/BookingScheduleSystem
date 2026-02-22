using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using Marten;

namespace BookingScheduleSystem.Api.Infrastructure.Database;

/// <summary>
/// Seeds the default subscription plans on application startup.
/// Idempotent — skips plans that already exist (matched by name).
/// </summary>
public static class SubscriptionPlanSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<IDocumentStore>>();
        var store = services.GetRequiredService<IDocumentStore>();

        await using var session = store.LightweightSession();

        var existingPlans = await session.Query<SubscriptionPlan>().ToListAsync();
        var existingNames = existingPlans.Select(p => p.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var defaultPlans = GetDefaultPlans();
        var createdCount = 0;

        foreach (var plan in defaultPlans)
        {
            if (existingNames.Contains(plan.Name))
                continue;

            session.Store(plan);
            createdCount++;
            logger.LogInformation("Seeding default subscription plan: {PlanName}", plan.Name);
        }

        if (createdCount > 0)
        {
            await session.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} default subscription plans", createdCount);
        }
        else
        {
            logger.LogInformation("All default subscription plans already exist — skipping seed");
        }
    }

    private static List<SubscriptionPlan> GetDefaultPlans() =>
    [
        new SubscriptionPlan
        {
            Id = SubscriptionPlanId.New(),
            Name = "Starter",
            Description = "Perfect for small businesses getting started with appointment scheduling. Includes email reminders and basic support with 48-hour SLA.",
            PricePerMonth = 499m,
            PricePerYear = 5390m,
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
            },
            IsActive = true,
            IsCustomPlan = false,
            CreatedAt = DateTime.UtcNow
        },
        new SubscriptionPlan
        {
            Id = SubscriptionPlanId.New(),
            Name = "Growth",
            Description = "Ideal for growing businesses with multiple locations and providers. Includes email reminders, SMS (pay-per-SMS), logo branding, and 24-hour email support.",
            PricePerMonth = 999m,
            PricePerYear = 10790m,
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
            },
            IsActive = true,
            IsCustomPlan = false,
            CreatedAt = DateTime.UtcNow
        },
        new SubscriptionPlan
        {
            Id = SubscriptionPlanId.New(),
            Name = "Business",
            Description = "For established businesses requiring advanced features and higher capacity. Includes email reminders, discounted SMS rates, full branding, and email + chat support.",
            PricePerMonth = 1999m,
            PricePerYear = 21590m,
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
            },
            IsActive = true,
            IsCustomPlan = false,
            CreatedAt = DateTime.UtcNow
        },
        new SubscriptionPlan
        {
            Id = SubscriptionPlanId.New(),
            Name = "Multi-Branch",
            Description = "Enterprise solution for organizations with multiple locations. Includes email reminders, SMS with monthly cap, full branding, and priority support.",
            PricePerMonth = 3999m,
            PricePerYear = 43190m,
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
            },
            IsActive = true,
            IsCustomPlan = false,
            CreatedAt = DateTime.UtcNow
        }
    ];
}
