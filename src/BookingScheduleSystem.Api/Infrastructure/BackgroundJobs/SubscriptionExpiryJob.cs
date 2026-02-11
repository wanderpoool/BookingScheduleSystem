using BookingScheduleSystem.Api.Infrastructure.Subscriptions;
using Marten;
using Microsoft.Extensions.Options;

namespace BookingScheduleSystem.Api.Infrastructure.BackgroundJobs;

public sealed class SubscriptionExpiryJob : PeriodicBackgroundService
{
    public SubscriptionExpiryJob(
        IServiceScopeFactory scopeFactory,
        ILogger<SubscriptionExpiryJob> logger,
        IOptions<BackgroundJobOptions> options)
        : base(
            scopeFactory,
            logger,
            TimeSpan.FromMinutes(options.Value.SubscriptionExpiryIntervalMinutes))
    {
        _batchSize = options.Value.BatchSize;
        _logger = logger;
    }

    private readonly int _batchSize;
    private readonly ILogger<SubscriptionExpiryJob> _logger;

    protected override string JobName => nameof(SubscriptionExpiryJob);

    protected override async Task ExecuteJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var now = DateTime.UtcNow;

        var expiredSubscriptions = await session
            .Query<TenantSubscription>()
            .Where(s => s.EndDate != null
                        && s.EndDate < now
                        && (s.Status == SubscriptionStatus.Active || s.Status == SubscriptionStatus.Cancelled))
            .Take(_batchSize)
            .ToListAsync(ct);

        if (expiredSubscriptions.Count == 0)
            return;

        _logger.LogInformation("Found {Count} subscriptions to expire", expiredSubscriptions.Count);

        foreach (var subscription in expiredSubscriptions)
        {
            var previousStatus = subscription.Status;
            subscription.Status = SubscriptionStatus.Expired;
            subscription.UpdatedAt = now;
            session.Store(subscription);

            _logger.LogInformation(
                "Expired subscription {SubscriptionId} for tenant {TenantId}. " +
                "Previous status: {PreviousStatus}, EndDate: {EndDate}",
                subscription.Id,
                subscription.TenantId,
                previousStatus,
                subscription.EndDate);
        }

        await session.SaveChangesAsync(ct);

        _logger.LogInformation("Successfully expired {Count} subscriptions", expiredSubscriptions.Count);
    }
}
