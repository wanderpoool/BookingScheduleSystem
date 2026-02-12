using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Subscriptions;

namespace BookingScheduleSystem.Web.Services;

internal sealed record ListSubscriptionPlansWrapper
{
    public List<SubscriptionPlanResponse> Plans { get; init; } = new();
}

public class SubscriptionService : ISubscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SubscriptionService> _logger;

    public SubscriptionService(HttpClient httpClient, ILogger<SubscriptionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<TenantSubscriptionResponse?> GetCurrentSubscriptionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/subscriptions/current");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TenantSubscriptionResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to get subscription: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't load your subscription.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription");
            throw new HttpRequestException("Unable to connect to the server.", ex);
        }
    }

    public async Task<List<SubscriptionPlanResponse>?> ListPlansAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/subscription-plans");
            if (response.IsSuccessStatusCode)
            {
                var wrapper = await response.Content.ReadFromJsonAsync<ListSubscriptionPlansWrapper>();
                return wrapper?.Plans;
            }

            _logger.LogWarning("Failed to list plans: {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing subscription plans");
            return null;
        }
    }

    public async Task<TenantSubscriptionResponse?> CancelSubscriptionAsync(string? reason = null)
    {
        try
        {
            var request = new CancelSubscriptionRequest { CancellationReason = reason };
            var response = await _httpClient.PostAsJsonAsync("/api/subscriptions/cancel", request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<TenantSubscriptionResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to cancel subscription: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "We couldn't cancel your subscription.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling subscription");
            throw new HttpRequestException("Unable to cancel subscription. Please try again.", ex);
        }
    }
}
