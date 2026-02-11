using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using Marten;
using Microsoft.Extensions.Options;

namespace BookingScheduleSystem.Api.Infrastructure.BackgroundJobs;

public sealed class UsageStatisticsResetJob : PeriodicBackgroundService
{
    public UsageStatisticsResetJob(
        IServiceScopeFactory scopeFactory,
        ILogger<UsageStatisticsResetJob> logger,
        IOptions<BackgroundJobOptions> options)
        : base(
            scopeFactory,
            logger,
            TimeSpan.FromMinutes(options.Value.UsageResetCheckIntervalMinutes))
    {
        _batchSize = options.Value.BatchSize;
        _logger = logger;
    }

    private readonly int _batchSize;
    private readonly ILogger<UsageStatisticsResetJob> _logger;

    protected override string JobName => nameof(UsageStatisticsResetJob);

    protected override async Task ExecuteJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var now = DateTime.UtcNow;
        var todayUtc = now.Date;

        var subscriptions = await session
            .Query<TenantSubscription>()
            .Where(s => s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Cancelled)
            .Take(_batchSize)
            .ToListAsync(ct);

        if (subscriptions.Count == 0)
            return;

        var dailyResetCount = 0;
        var monthlyResetCount = 0;

        foreach (var subscription in subscriptions)
        {
            var changed = false;

            // Daily reset: if last updated was a previous day, zero out daily counters
            if (subscription.CurrentUsage.LastUpdated.Date < todayUtc)
            {
                subscription.CurrentUsage.BookingsToday = 0;
                subscription.CurrentUsage.SchedulesToday = 0;
                subscription.CurrentUsage.LastUpdated = now;
                changed = true;
                dailyResetCount++;
            }

            // Monthly reset: if the usage reset date has passed, zero out monthly counters
            // and advance the reset date by one billing cycle
            if (subscription.UsageResetDate <= now)
            {
                subscription.CurrentUsage.BookingsThisMonth = 0;
                subscription.CurrentUsage.SchedulesThisMonth = 0;
                subscription.CurrentUsage.LastUpdated = now;

                subscription.UsageResetDate = subscription.BillingCycle == BillingCycle.Monthly
                    ? subscription.UsageResetDate.AddMonths(1)
                    : subscription.UsageResetDate.AddYears(1);

                changed = true;
                monthlyResetCount++;
            }

            if (changed)
            {
                subscription.UpdatedAt = now;
                session.Store(subscription);
            }
        }

        if (dailyResetCount > 0 || monthlyResetCount > 0)
        {
            await session.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Usage reset complete. Daily resets: {DailyCount}, Monthly resets: {MonthlyCount}",
                dailyResetCount,
                monthlyResetCount);
        }
    }
}
