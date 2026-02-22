using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Analytics;

namespace BookingScheduleSystem.Web.Services;

public class AdminDashboardService : IAdminDashboardService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AdminDashboardService> _logger;

    public AdminDashboardService(HttpClient httpClient, ILogger<AdminDashboardService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PlatformSummaryResponse?> GetPlatformSummaryAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/analytics/platform-summary");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<PlatformSummaryResponse>();
            }

            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("Failed to get platform summary: {StatusCode} {Body}", response.StatusCode, body);
            var message = ApiErrorHelper.ExtractMessage(body) ?? "Failed to load platform summary.";
            throw new HttpRequestException(message, null, response.StatusCode);
        }
        catch (HttpRequestException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting platform summary");
            throw new HttpRequestException("Unable to connect to the server.", ex);
        }
    }
}
