using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Queue;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Queue;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Queue;

public sealed class GetQueueEntryStatusRequest
{
    public Guid Id { get; set; }
}

/// <summary>
/// GET /api/queue/entries/{id} — Public endpoint to check queue entry status.
/// Uses SQL/JSONB query to find the entry without loading all queues.
/// </summary>
public sealed class GetQueueEntryStatus : Endpoint<GetQueueEntryStatusRequest, QueueEntryResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/queue/entries/{id}");
        AllowAnonymous();
        Throttle(30, 60);
        Description(d => d
            .WithTags("Queue")
            .WithSummary("Get queue entry status")
            .WithDescription("Public endpoint. Returns the current status and ETA for a queue entry."));
    }

    public override async Task HandleAsync(GetQueueEntryStatusRequest req, CancellationToken ct)
    {
        var entryId = new QueueEntryId(req.Id);

        await using var session = DocumentStore.LightweightSession();

        var today = DateTime.Today;
        var queue = await session.Query<DailyQueue>()
            .Where(q => q.QueueDate == today && q.Entries.Any(e => e.Id == entryId))
            .FirstOrDefaultAsync(ct);

        if (queue is null)
        {
            ThrowError("Queue entry not found", 404);
        }

        var entry = queue.Entries.First(e => e.Id == entryId);

        var tenant = await session.LoadAsync<Tenant>(queue.TenantId, ct);
        if (tenant is null || !tenant.QueueEnabled)
        {
            ThrowError("Queue is not enabled for this organization", 400);
        }

        var avgServiceTime = tenant.QueueAverageServiceTimeMinutes;

        // Mask phone number for anonymous public access
        Response = new QueueEntryResponse
        {
            Id = entry.Id,
            QueueNumber = entry.QueueNumber,
            CustomerName = entry.CustomerName,
            PhoneNumber = MaskPhoneNumber(entry.PhoneNumber),
            Status = entry.Status,
            IsPriority = entry.IsPriority,
            EstimatedWaitMinutes = QueueHelper.CalculateEstimatedWaitMinutes(queue, entry, avgServiceTime),
            JoinedAt = entry.JoinedAt,
            CalledAt = entry.CalledAt,
            CompletedAt = entry.CompletedAt
        };
    }

    private static string MaskPhoneNumber(string phone)
    {
        if (phone.Length <= 4) return "****";
        return $"****{phone[^4..]}";
    }
}
