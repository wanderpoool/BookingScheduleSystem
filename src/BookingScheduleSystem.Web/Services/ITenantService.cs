using BookingScheduleSystem.Contracts.Tenants;

namespace BookingScheduleSystem.Web.Services;

public interface ITenantService
{
    Task<TenantResponse?> GetTenantAsync(Guid tenantId);
}
