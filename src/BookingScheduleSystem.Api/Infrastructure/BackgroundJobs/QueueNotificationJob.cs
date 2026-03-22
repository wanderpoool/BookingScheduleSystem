using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Notifications;
using BookingScheduleSystem.Api.Infrastructure.Queue;
using BookingScheduleSystem.Contracts.Queue;
using Marten;
using Microsoft.Extensions.Options;

namespace BookingScheduleSystem.Api.Infrastructure.BackgroundJobs;

/// <summary>
/// Sends SMS notifications to queue customers whose estimated wait time
/// is within the tenant's notification lead time. Runs every 5 minutes.
/// Tracks sent notifications via the NotificationSent flag on QueueEntry
/// to prevent duplicate sends.
/// </summary>
public sealed class QueueNotificationJob : PeriodicBackgroundService
{
    private readonly ILogger<QueueNotificationJob> _logger;

    public QueueNotificationJob(
        IServiceScopeFactory scopeFactory,
        ILogger<QueueNotificationJob> logger,
        IOptions<BackgroundJobOptions> options)
        : base(
            scopeFactory,
            logger,
            TimeSpan.FromMinutes(options.Value.QueueNotificationIntervalMinutes))
    {
        _logger = logger;
    }

    protected override string JobName => nameof(QueueNotificationJob);

    protected override async Task ExecuteJobAsync(IServiceScope scope, CancellationToken ct)
    {
        var session = scope.ServiceProvider.GetRequiredService<IDocumentSession>();
        var smsService = scope.ServiceProvider.GetRequiredService<ISmsService>();

        if (!smsService.IsConfigured)
            return;

        var today = DateTime.Today;

        // Find all today's queues with waiting entries
        var queues = await session.Query<DailyQueue>()
            .Where(q => q.QueueDate == today)
            .ToListAsync(ct);

        if (queues.Count == 0)
            return;

        var tenantsToNotify = queues
            .Where(q => q.Entries.Any(e => e.Status == QueueEntryStatus.Waiting && !e.NotificationSent))
            .ToList();

        if (tenantsToNotify.Count == 0)
            return;

        // Load tenants for notification settings
        var tenantIds = tenantsToNotify.Select(q => q.TenantId).Distinct().ToList();
        var tenants = new Dictionary<Contracts.Common.TenantId, Tenant>();
        foreach (var tenantId in tenantIds)
        {
            var tenant = await session.LoadAsync<Tenant>(tenantId, ct);
            if (tenant is not null && tenant.QueueEnabled)
                tenants[tenantId] = tenant;
        }

        var notificationCount = 0;

        foreach (var queue in tenantsToNotify)
        {
            if (!tenants.TryGetValue(queue.TenantId, out var tenant))
                continue;

            var leadMinutes = tenant.QueueNotificationLeadMinutes;
            var avgServiceTime = tenant.QueueAverageServiceTimeMinutes;
            var modified = false;

            foreach (var entry in queue.Entries.Where(e =>
                e.Status == QueueEntryStatus.Waiting && !e.NotificationSent))
            {
                var eta = QueueHelper.CalculateEstimatedWaitMinutes(queue, entry, avgServiceTime);

                if (eta > leadMinutes)
                    continue;

                // ETA is within lead time — send notification
                try
                {
                    var name = entry.CustomerName ?? "Customer";
                    var message = $"Hi {name}! Your turn at {tenant.Name} is coming up in about {Math.Max(1, eta)} minutes. " +
                                  $"You are #{QueueHelper.CalculatePosition(queue, entry)} in line. Please be ready.";

                    using var smsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    smsCts.CancelAfter(TimeSpan.FromSeconds(10));
                    var sent = await smsService.SendSmsAsync(entry.PhoneNumber, message, smsCts.Token);
                    if (sent)
                    {
                        entry.NotificationSent = true;
                        modified = true;
                        notificationCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send queue notification to {PhoneNumber} for entry {EntryId}",
                        entry.PhoneNumber, entry.Id);
                }
            }

            if (modified)
                session.Update(queue);
        }

        if (notificationCount > 0)
        {
            await session.SaveChangesAsync(ct);
            _logger.LogInformation("Sent {Count} queue notifications", notificationCount);
        }
    }
}
