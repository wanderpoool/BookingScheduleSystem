using System.Net.Http.Json;
using BookingScheduleSystem.Contracts.Notifications;

namespace BookingScheduleSystem.Web.Services;

public class NotificationService : INotificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(HttpClient httpClient, ILogger<NotificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ListNotificationsResponse?> GetNotificationsAsync(int page = 1, int pageSize = 10)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/notifications?pageNumber={page}&pageSize={pageSize}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<ListNotificationsResponse>();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting notifications");
            return null;
        }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/notifications/unread-count");
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
                return result?.Count ?? 0;
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }

    public async Task MarkAsReadAsync(Guid notificationId)
    {
        try
        {
            await _httpClient.PostAsync($"/api/notifications/{notificationId}/read", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking notification as read");
        }
    }

    public async Task MarkAllAsReadAsync()
    {
        try
        {
            await _httpClient.PostAsync("/api/notifications/read-all", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking all notifications as read");
        }
    }
}
