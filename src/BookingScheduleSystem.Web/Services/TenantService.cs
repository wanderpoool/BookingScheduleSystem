using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Tenants;

namespace BookingScheduleSystem.Web.Services;

public class TenantService : ITenantService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TenantService> _logger;

    public TenantService(HttpClient httpClient, ILogger<TenantService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TenantResponse?> GetTenantAsync(Guid tenantId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/tenants/{tenantId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TenantResponse>();
            }

            _logger.LogWarning("Failed to get tenant {TenantId}: {StatusCode}", tenantId, response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tenant {TenantId}", tenantId);
            return null;
        }
    }
}
