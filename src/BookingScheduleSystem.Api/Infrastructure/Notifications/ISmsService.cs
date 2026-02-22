namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

public interface ISmsService
{
    Task<bool> SendSmsAsync(string phoneNumber, string message, CancellationToken ct = default);
}
