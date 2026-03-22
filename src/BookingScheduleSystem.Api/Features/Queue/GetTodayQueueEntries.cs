using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Queue;
using BookingScheduleSystem.Contracts.Queue;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Queue;

/// <summary>
/// GET /api/queue/today/entries — List all entries for today's queue.
/// </summary>
public sealed class GetTodayQueueEntries : EndpointWithoutRequest<List<QueueEntryResponse>>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Get("/api/queue/today/entries");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Queue")
            .WithSummary("List today's queue entries")
            .WithDescription("Returns all entries for today's queue."));
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

        var avgServiceTime = tenant.QueueAverageServiceTimeMinutes;

        var today = DateTime.Today;
        var queue = await session.Query<DailyQueue>()
            .FirstOrDefaultAsync(q => q.TenantId == tenantId.Value && q.QueueDate == today, ct);

        if (queue is null)
        {
            Response = [];
            return;
        }

        Response = queue.Entries
            .Select(e => QueueHelper.ToResponse(queue, e, avgServiceTime))
            .ToList();
    }
}
