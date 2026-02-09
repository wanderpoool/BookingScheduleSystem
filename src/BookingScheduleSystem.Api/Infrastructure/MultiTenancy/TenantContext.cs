using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Scoped service that holds the current tenant identifier
/// </summary>
public sealed class TenantContext : ITenantContext
{
    private TenantId? _currentTenantId;

    public TenantId? CurrentTenantId => _currentTenantId;

    public bool HasTenant => _currentTenantId.HasValue;

    public void SetTenant(TenantId tenantId)
    {
        if (_currentTenantId.HasValue)
        {
            throw new InvalidOperationException("Tenant context has already been set for this request");
        }

        _currentTenantId = tenantId;
    }
}
