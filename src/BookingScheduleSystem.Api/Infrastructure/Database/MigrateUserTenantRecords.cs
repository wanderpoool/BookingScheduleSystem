using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Contracts.Common;
using Marten;

namespace BookingScheduleSystem.Api.Infrastructure.Database;

/// <summary>
/// One-time startup migration: creates UserTenant records for all existing users
/// that have a TenantId but no corresponding UserTenant entry. Idempotent.
/// </summary>
public sealed class MigrateUserTenantRecords : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MigrateUserTenantRecords> _logger;

    public MigrateUserTenantRecords(IServiceProvider services, ILogger<MigrateUserTenantRecords> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentStore>();
        await using var session = store.LightweightSession();

        var usersWithTenant = await session.Query<User>()
            .Where(u => u.TenantId != null && !u.IsGlobalAdmin)
            .ToListAsync(cancellationToken);

        if (usersWithTenant.Count == 0)
        {
            _logger.LogInformation("UserTenant migration: no users with TenantId found, skipping");
            return;
        }

        var existingMemberships = await session.Query<UserTenant>()
            .ToListAsync(cancellationToken);

        var existingPairs = existingMemberships
            .Select(ut => (ut.UserId, ut.TenantId))
            .ToHashSet();

        var migrated = 0;
        foreach (var user in usersWithTenant)
        {
            if (!user.TenantId.HasValue) continue;

            var pair = (user.Id, user.TenantId.Value);
            if (existingPairs.Contains(pair)) continue;

            var userTenant = new UserTenant
            {
                Id = UserTenantId.New(),
                UserId = user.Id,
                TenantId = user.TenantId.Value,
                Role = user.IsProvider ? "Provider" : "Customer",
                JoinedAt = user.CreatedAt,
                IsActive = user.IsActive
            };

            session.Store(userTenant);
            migrated++;
        }

        if (migrated > 0)
        {
            await session.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("UserTenant migration: migrated {Count} users to UserTenant records", migrated);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
