using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using BookingScheduleSystem.Contracts.Common;
using Marten;

namespace BookingScheduleSystem.Api.Infrastructure.Auth;

/// <summary>
/// Ensures a user is enrolled in a tenant. Customers are auto-enrolled
/// ("lazy enrollment"); providers and GlobalAdmins must have pre-existing membership.
/// Takes the caller's IDocumentSession so enrollment is part of the same unit-of-work.
/// </summary>
public sealed class LazyEnrollmentService
{
    private readonly ILogger<LazyEnrollmentService> _logger;

    public LazyEnrollmentService(ILogger<LazyEnrollmentService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Returns the UserTenant membership for this user + tenant, creating it if the user
    /// is a customer and none exists. Returns null if enrollment is denied (provider/admin
    /// without membership, or subscription limit reached).
    /// </summary>
    public async Task<UserTenant?> EnsureEnrollmentAsync(
        IDocumentSession session,
        User user,
        TenantId requestedTenantId,
        CancellationToken ct)
    {
        // Check for existing membership
        var existing = await session.Query<UserTenant>()
            .FirstOrDefaultAsync(ut => ut.UserId == user.Id && ut.TenantId == requestedTenantId, token: ct);

        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                _logger.LogWarning("User {UserId} has inactive membership for tenant {TenantId}", user.Id, requestedTenantId);
                return null;
            }
            return existing;
        }

        // Providers and GlobalAdmins are NOT auto-enrolled
        if (user.IsProvider || user.IsGlobalAdmin)
        {
            _logger.LogInformation("Provider/admin {UserId} has no membership for tenant {TenantId}, denying", user.Id, requestedTenantId);
            return null;
        }

        // Customer auto-enrollment: check subscription limits first
        var tenant = await session.LoadAsync<Tenant>(requestedTenantId, ct);
        if (tenant is null || !tenant.IsActive)
        {
            _logger.LogWarning("Tenant {TenantId} not found or inactive for enrollment", requestedTenantId);
            return null;
        }

        var (planLimits, _) = await PlanLimitHelper.GetCurrentLimitsAsync(session, tenant, ct);
        if (planLimits is not null)
        {
            var activeUserCount = await session.Query<UserTenant>()
                .CountAsync(ut => ut.TenantId == requestedTenantId && ut.IsActive, ct);

            if (activeUserCount >= planLimits.MaxUsers)
            {
                _logger.LogWarning("Tenant {TenantId} at user limit ({Limit}), denying enrollment for {UserId}",
                    requestedTenantId, planLimits.MaxUsers, user.Id);
                return null;
            }
        }

        // Auto-enroll customer
        var userTenant = new UserTenant
        {
            Id = UserTenantId.New(),
            UserId = user.Id,
            TenantId = requestedTenantId,
            Role = "Customer",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        session.Store(userTenant);

        _logger.LogInformation("Auto-enrolled customer {UserId} in tenant {TenantId}", user.Id, requestedTenantId);

        return userTenant;
    }
}
