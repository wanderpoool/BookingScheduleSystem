using System.Net;
using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Schedules;

namespace BookingScheduleSystem.Web.Services;

public class ScheduleService : IScheduleService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ScheduleService> _logger;

    public ScheduleService(HttpClient httpClient, ILogger<ScheduleService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ListPublicSchedulesResponse?> ListPublicSchedulesAsync(
        Guid tenantId, DateTime? startDate = null, DateTime? endDate = null, int page = 1, int pageSize = 20)
    {
        try
        {
            var url = $"/api/tenants/{tenantId}/schedules?pageNumber={page}&pageSize={pageSize}";
            if (startDate.HasValue)
                url += $"&startDate={startDate.Value:O}";
            if (endDate.HasValue)
                url += $"&endDate={endDate.Value:O}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ListPublicSchedulesResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to list schedules for tenant {TenantId}: {StatusCode} {Body}", tenantId, response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? GetScheduleListError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing schedules for tenant {TenantId}", tenantId);
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<ListSchedulesResponse?> ListSchedulesAsync(
        Guid? providerId = null, DateTime? startDate = null, DateTime? endDate = null,
        bool? isActive = null, int page = 1, int pageSize = 20)
    {
        try
        {
            var url = $"/api/schedules?pageNumber={page}&pageSize={pageSize}";
            if (providerId.HasValue)
                url += $"&providerId={providerId.Value}";
            if (startDate.HasValue)
                url += $"&startDate={startDate.Value:O}";
            if (endDate.HasValue)
                url += $"&endDate={endDate.Value:O}";
            if (isActive.HasValue)
                url += $"&isActive={isActive.Value}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ListSchedulesResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to list schedules: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? GetScheduleListError(response.StatusCode);
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing schedules");
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<ScheduleResponse?> GetScheduleAsync(Guid scheduleId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/schedules/{scheduleId}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ScheduleResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to get schedule {ScheduleId}: {StatusCode} {Body}", scheduleId, response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't load this schedule. Please try again.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting schedule {ScheduleId}", scheduleId);
            throw new HttpRequestException("Unable to connect to the server. Please check your connection and try again.", ex);
        }
    }

    public async Task<ScheduleResponse?> CreateScheduleAsync(CreateScheduleRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/schedules", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ScheduleResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to create schedule: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't create the schedule. Please try again.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating schedule");
            throw new HttpRequestException("Unable to create the schedule. Please try again.", ex);
        }
    }

    public async Task<ScheduleResponse?> UpdateScheduleAsync(Guid scheduleId, UpdateScheduleRequest request)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/schedules/{scheduleId}", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ScheduleResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to update schedule {ScheduleId}: {StatusCode} {Body}", scheduleId, response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't update the schedule. Please try again.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating schedule {ScheduleId}", scheduleId);
            throw new HttpRequestException("Unable to update the schedule. Please try again.", ex);
        }
    }

    public async Task DeleteScheduleAsync(Guid scheduleId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/schedules/{scheduleId}");
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to delete schedule {ScheduleId}: {StatusCode} {Body}", scheduleId, response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't delete the schedule. It may have active bookings.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting schedule {ScheduleId}", scheduleId);
            throw new HttpRequestException("Unable to delete the schedule. Please try again.", ex);
        }
    }

    private static string GetScheduleListError(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.NotFound => "Your organization's schedules could not be found. Please contact your administrator.",
        HttpStatusCode.Unauthorized => "Your session has expired. Please sign in again to view schedules.",
        HttpStatusCode.Forbidden => "You don't have permission to view these schedules.",
        _ => "We couldn't load the schedules right now. Please try again in a moment."
    };
}
