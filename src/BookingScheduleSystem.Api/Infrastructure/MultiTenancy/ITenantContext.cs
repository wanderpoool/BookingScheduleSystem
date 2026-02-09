using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Api.Infrastructure.MultiTenancy;

/// <summary>
/// Provides access to the current tenant context
/// </summary>
public interface ITenantContext
{
    TenantId? CurrentTenantId { get; }
    bool HasTenant { get; }
    void SetTenant(TenantId tenantId);
}
