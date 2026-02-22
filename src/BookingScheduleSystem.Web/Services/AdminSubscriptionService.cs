using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Subscriptions;

namespace BookingScheduleSystem.Web.Services;

public class AdminSubscriptionService : IAdminSubscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AdminSubscriptionService> _logger;

    public AdminSubscriptionService(HttpClient httpClient, ILogger<AdminSubscriptionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<SubscriptionPlanResponse>?> ListAllPlansAsync(bool includeInactive = true)
    {
        try
        {
            var query = $"/api/subscription-plans?includeInactive={includeInactive.ToString().ToLowerInvariant()}";
            var response = await _httpClient.GetAsync(query);
            if (response.IsSuccessStatusCode)
            {
                var wrapper = await response.Content.ReadFromJsonAsync<ListSubscriptionPlansWrapper>();
                return wrapper?.Plans;
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to list all plans: {StatusCode} {Body}", response.StatusCode, body);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing all subscription plans");
            return null;
        }
    }

    public async Task<ListAllSubscriptionsResponse?> ListAllSubscriptionsAsync(
        SubscriptionStatusDto? status = null,
        string? planId = null,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            var query = $"/api/analytics/subscriptions?page={page}&pageSize={pageSize}";
            if (status.HasValue)
                query += $"&status={status.Value}";
            if (!string.IsNullOrEmpty(planId))
                query += $"&planId={planId}";

            var response = await _httpClient.GetAsync(query);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ListAllSubscriptionsResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to list subscriptions: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "Failed to load subscriptions.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing all subscriptions");
            throw new HttpRequestException("Unable to connect to the server.", ex);
        }
    }

    public async Task<TenantSubscriptionResponse?> AdminChangePlanAsync(AdminChangePlanRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/subscriptions/change-plan", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TenantSubscriptionResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to change plan: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "Failed to change subscription plan.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing subscription plan");
            throw new HttpRequestException("Unable to change plan. Please try again.", ex);
        }
    }

    public async Task<TenantSubscriptionResponse?> AdminSuspendAsync(AdminSuspendSubscriptionRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/subscriptions/suspend", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TenantSubscriptionResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to suspend subscription: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "Failed to suspend subscription.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suspending subscription");
            throw new HttpRequestException("Unable to suspend subscription. Please try again.", ex);
        }
    }

    public async Task<TenantSubscriptionResponse?> AdminReactivateAsync(AdminReactivateSubscriptionRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("/api/admin/subscriptions/reactivate", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TenantSubscriptionResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to reactivate subscription: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "Failed to reactivate subscription.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reactivating subscription");
            throw new HttpRequestException("Unable to reactivate subscription. Please try again.", ex);
        }
    }
}
