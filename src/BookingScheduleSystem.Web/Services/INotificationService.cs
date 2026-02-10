using BookingScheduleSystem.Contracts.Notifications;

namespace BookingScheduleSystem.Web.Services;

public interface INotificationService
{
    Task<ListNotificationsResponse?> GetNotificationsAsync(int page = 1, int pageSize = 10);
    Task<int> GetUnreadCountAsync();
    Task MarkAsReadAsync(Guid notificationId);
    Task MarkAllAsReadAsync();
}
