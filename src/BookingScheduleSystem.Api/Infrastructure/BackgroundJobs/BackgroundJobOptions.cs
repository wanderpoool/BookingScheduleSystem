namespace BookingScheduleSystem.Api.Infrastructure.BackgroundJobs;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public int SubscriptionExpiryIntervalMinutes { get; set; } = 60;
    public int UsageResetCheckIntervalMinutes { get; set; } = 15;
    public int BatchSize { get; set; } = 100;
}
