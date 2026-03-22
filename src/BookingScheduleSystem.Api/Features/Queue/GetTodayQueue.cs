using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Queue;
using BookingScheduleSystem.Contracts.Queue;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Queue;

/// <summary>
/// POST /api/queue/today — Get or create today's queue for the current tenant.
/// Returns the queue with its daily token and all entries.
/// </summary>
public sealed class GetTodayQueue : EndpointWithoutRequest<GetTodayQueueResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Post("/api/queue/today");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Queue")
            .WithSummary("Get or create today's queue")
            .WithDescription("Returns today's queue for the current tenant, creating it if it doesn't exist."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (tenantId is null)
        {
            ThrowError("Tenant context required", 400);
        }

        await using var session = DocumentStore.LightweightSession();

        var tenant = await session.LoadAsync<Infrastructure.MultiTenancy.Tenant>(tenantId.Value, ct);
        if (tenant is null || !tenant.QueueEnabled)
        {
            ThrowError("Queue is not enabled for this organization", 400);
        }

        var today = DateTime.Today;

        var queue = await session.Query<DailyQueue>()
            .FirstOrDefaultAsync(q => q.TenantId == tenantId.Value && q.QueueDate == today, ct);

        if (queue is null)
        {
            queue = new DailyQueue
            {
                Id = Contracts.Common.QueueId.New(),
                TenantId = tenantId.Value,
                QueueDate = today,
                DailyToken = QueueHelper.GenerateDailyToken()
            };
            session.Store(queue);
            await session.SaveChangesAsync(ct);
        }

        var avgServiceTime = tenant.QueueAverageServiceTimeMinutes;

        Response = new GetTodayQueueResponse
        {
            Id = queue.Id,
            DailyToken = queue.DailyToken,
            QueueDate = queue.QueueDate,
            Entries = queue.Entries.Select(e => QueueHelper.ToResponse(queue, e, avgServiceTime)).ToList(),
            TotalWaiting = queue.Entries.Count(e => e.Status == QueueEntryStatus.Waiting),
            TotalCalled = queue.Entries.Count(e => e.Status is QueueEntryStatus.Called or QueueEntryStatus.InService),
            TotalCompleted = queue.Entries.Count(e => e.Status == QueueEntryStatus.Completed)
        };
    }
}
