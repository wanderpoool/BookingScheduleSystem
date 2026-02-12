using BookingScheduleSystem.Contracts.Tenants;

namespace BookingScheduleSystem.Web.Services;

public interface ITenantService
{
    Task<TenantResponse?> GetTenantAsync(Guid tenantId);
    Task<TenantResponse?> GetTenantBySubdomainAsync(string subdomain);
    Task<TenantResponse?> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request);
}
