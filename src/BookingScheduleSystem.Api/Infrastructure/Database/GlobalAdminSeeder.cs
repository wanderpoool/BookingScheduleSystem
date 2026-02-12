using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Contracts.Common;
using Marten;

namespace BookingScheduleSystem.Api.Infrastructure.Database;

/// <summary>
/// Seeds the Global Admin user on application startup.
/// Configured via Seed:GlobalAdmin:Email and Seed:GlobalAdmin:Password settings.
/// Idempotent — skips if user already exists.
/// </summary>
public static class GlobalAdminSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var logger = services.GetRequiredService<ILogger<IDocumentStore>>();

        var email = config["Seed:GlobalAdmin:Email"];
        var password = config["Seed:GlobalAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Global Admin seed skipped — Seed:GlobalAdmin:Email or Password not configured");
            return;
        }

        var store = services.GetRequiredService<IDocumentStore>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher>();

        await using var session = store.LightweightSession();

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existingUser = await session.Query<User>()
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (existingUser is not null)
        {
            logger.LogInformation("Global Admin user already exists: {Email}", normalizedEmail);
            return;
        }

        var user = new User
        {
            Id = UserId.New(),
            Email = normalizedEmail,
            PasswordHash = passwordHasher.HashPassword(password),
            FirstName = "Admin",
            LastName = "Global",
            IsGlobalAdmin = true,
            IsProvider = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        session.Store(user);
        await session.SaveChangesAsync();

        logger.LogInformation("Global Admin user seeded: {Email}", normalizedEmail);
    }
}
