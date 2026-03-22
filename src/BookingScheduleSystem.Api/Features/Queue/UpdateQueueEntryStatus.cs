using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Queue;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Queue;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Queue;

public sealed class UpdateQueueEntryStatusEndpointRequest
{
    public Guid Id { get; set; }
    public QueueEntryStatus Status { get; set; }
}

/// <summary>
/// PATCH /api/queue/entries/{id}/status — Update a queue entry's status.
/// Valid transitions: Waiting→Called, Called→InService, InService→Completed,
/// Waiting→Skipped, Waiting→NoShow, Called→NoShow, Called→Skipped.
/// </summary>
public sealed class UpdateQueueEntryStatus : Endpoint<UpdateQueueEntryStatusEndpointRequest, QueueEntryResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Patch("/api/queue/entries/{id}/status");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Queue")
            .WithSummary("Update queue entry status")
            .WithDescription("Updates the status of a queue entry (Call, Start Service, Complete, Skip, No Show)."));
    }

    public override async Task HandleAsync(UpdateQueueEntryStatusEndpointRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (tenantId is null)
            ThrowError("Tenant context required", 400);

        if (!Enum.IsDefined(req.Status))
            ThrowError("Invalid status value", 400);

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

                var oldStatus = entry.Status;
                if (!IsValidTransition(oldStatus, req.Status))
                    ThrowError($"Cannot transition from {oldStatus} to {req.Status}", 400);

                entry.Status = req.Status;
                var now = DateTime.Now;

                switch (req.Status)
                {
                    case QueueEntryStatus.Called:
                        entry.CalledAt = now;
                        break;
                    case QueueEntryStatus.Completed:
                        entry.CompletedAt = now;
                        break;
                }

                session.Update(queue);
                await session.SaveChangesAsync(ct);

                Logger.LogInformation(
                    "Queue entry {EntryId} status changed {OldStatus} → {NewStatus}, Number {QueueNumber}, TenantId {TenantId}",
                    entryId, oldStatus, req.Status, entry.QueueNumber, tenantId.Value);

                var tenant = await session.LoadAsync<Infrastructure.MultiTenancy.Tenant>(tenantId.Value, ct);
                Response = QueueHelper.ToResponse(queue, entry, tenant?.QueueAverageServiceTimeMinutes ?? 15);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && QueueConcurrencyHelper.IsConcurrencyException(ex))
            {
                Logger.LogWarning("Concurrency conflict updating entry status, retry {Attempt}/{Max}", attempt + 1, maxRetries);
                await Task.Delay(Random.Shared.Next(20, 80), ct);
            }
        }
    }

    private static bool IsValidTransition(QueueEntryStatus current, QueueEntryStatus next)
    {
        return (current, next) switch
        {
            (QueueEntryStatus.Waiting, QueueEntryStatus.Called) => true,
            (QueueEntryStatus.Waiting, QueueEntryStatus.Skipped) => true,
            (QueueEntryStatus.Waiting, QueueEntryStatus.NoShow) => true,
            (QueueEntryStatus.Called, QueueEntryStatus.InService) => true,
            (QueueEntryStatus.Called, QueueEntryStatus.NoShow) => true,
            (QueueEntryStatus.Called, QueueEntryStatus.Skipped) => true,
            (QueueEntryStatus.InService, QueueEntryStatus.Completed) => true,
            _ => false
        };
    }
}
