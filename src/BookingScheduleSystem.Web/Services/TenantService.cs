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

    public async Task<TenantResponse?> UpdateTenantAsync(Guid tenantId, UpdateTenantRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/tenants/{tenantId}", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TenantResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to update tenant {TenantId}: {StatusCode} {Body}", tenantId, response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't update the organization settings.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tenant {TenantId}", tenantId);
            throw new HttpRequestException("Unable to update organization settings. Please try again.", ex);
        }
    }
}
