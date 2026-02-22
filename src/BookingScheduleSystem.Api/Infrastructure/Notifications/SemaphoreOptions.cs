namespace BookingScheduleSystem.Api.Infrastructure.Notifications;

public sealed class SemaphoreOptions
{
    public const string SectionName = "Semaphore";

    public string ApiKey { get; set; } = "";
    public string SenderName { get; set; } = "BookMeApp";
    public string BaseUrl { get; set; } = "https://api.semaphore.co/api/v4";
}
