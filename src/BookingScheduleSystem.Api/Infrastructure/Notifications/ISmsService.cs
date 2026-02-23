namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

public interface ISmsService
{
    bool IsConfigured { get; }
    Task<bool> SendSmsAsync(string phoneNumber, string message, CancellationToken ct = default);
}
