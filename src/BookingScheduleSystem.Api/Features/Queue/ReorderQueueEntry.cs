using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Queue;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Queue;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Queue;

public sealed class ReorderQueueEntryEndpointRequest
{
    public Guid Id { get; set; }
    public int NewPosition { get; set; }
}

/// <summary>
/// PATCH /api/queue/entries/{id}/reorder — Change a waiting entry's position in the queue.
/// Swaps queue numbers to achieve the desired position.
/// </summary>
public sealed class ReorderQueueEntry : Endpoint<ReorderQueueEntryEndpointRequest, QueueEntryResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Patch("/api/queue/entries/{id}/reorder");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Queue")
            .WithSummary("Reorder queue entry")
            .WithDescription("Moves a waiting queue entry to a new position (1-based)."));
    }

    public override async Task HandleAsync(ReorderQueueEntryEndpointRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (tenantId is null)
            ThrowError("Tenant context required", 400);

        if (req.NewPosition < 1)
            ThrowError("Position must be at least 1", 400);

        // Verify queue is enabled for this tenant
        {
            await using var checkSession = DocumentStore.LightweightSession();
            var tenant = await checkSession.LoadAsync<Infrastructure.MultiTenancy.Tenant>(tenantId.Value, ct);
            if (tenant is null || !tenant.QueueEnabled)
                ThrowError("Queue is not enabled for this organization", 400);
        }

        var entryId = new QueueEntryId(req.Id);

        const int maxRetries = 3;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await using var session = DocumentStore.LightweightSession();

                var today = DateTime.Today;
                var queue = await session.Query<DailyQueue>()
                    .FirstOrDefaultAsync(q => q.TenantId == tenantId.Value && q.QueueDate == today, ct);

                if (queue is null)
                    ThrowError("No queue exists for today", 404);

                var entry = queue.Entries.FirstOrDefault(e => e.Id == entryId);
                if (entry is null)
                    ThrowError("Queue entry not found", 404);

                if (entry.Status != QueueEntryStatus.Waiting)
                    ThrowError("Only waiting entries can be reordered", 400);

                var waitingEntries = queue.Entries
                    .Where(e => e.Status == QueueEntryStatus.Waiting)
                    .OrderBy(e => e.IsPriority ? 0 : 1)
                    .ThenBy(e => e.QueueNumber)
                    .ToList();

                if (req.NewPosition > waitingEntries.Count)
                    ThrowError($"Position must be between 1 and {waitingEntries.Count}", 400);

                var currentIndex = waitingEntries.FindIndex(e => e.Id == entryId);
                var targetIndex = req.NewPosition - 1;

                if (currentIndex != targetIndex)
                {
                    var targetEntry = waitingEntries[targetIndex];
                    (entry.QueueNumber, targetEntry.QueueNumber) = (targetEntry.QueueNumber, entry.QueueNumber);

                    session.Update(queue);
                    await session.SaveChangesAsync(ct);

                    Logger.LogInformation(
                        "Queue entry {EntryId} reordered from position {OldPos} to {NewPos}, TenantId {TenantId}",
                        entryId, currentIndex + 1, req.NewPosition, tenantId.Value);
                }

                var tenant = await session.LoadAsync<Infrastructure.MultiTenancy.Tenant>(tenantId.Value, ct);
                Response = QueueHelper.ToResponse(queue, entry, tenant?.QueueAverageServiceTimeMinutes ?? 15);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && QueueConcurrencyHelper.IsConcurrencyException(ex))
            {
                Logger.LogWarning("Concurrency conflict reordering entry, retry {Attempt}/{Max}", attempt + 1, maxRetries);
                await Task.Delay(Random.Shared.Next(20, 80), ct);
            }
        }
    }
}
