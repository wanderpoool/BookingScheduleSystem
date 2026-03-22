using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Queue;
using BookingScheduleSystem.Contracts.Queue;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Queue;

public sealed class GetQueueInfoRequest
{
    [QueryParam]
    public string Token { get; set; } = string.Empty;
}

/// <summary>
/// GET /api/queue/info?token={token} — Public endpoint for the join page.
/// Returns tenant name, queue date, current waiting count, and avg service time.
/// </summary>
public sealed class GetQueueInfo : Endpoint<GetQueueInfoRequest, QueueInfoResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/queue/info");
        AllowAnonymous();
        Description(d => d
            .WithTags("Queue")
            .WithSummary("Get queue info for join page")
            .WithDescription("Public endpoint. Returns tenant name and queue stats for the customer join page."));
    }

    public override async Task HandleAsync(GetQueueInfoRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Token))
        {
            ThrowError("Queue token is required", 400);
        }

        await using var session = DocumentStore.LightweightSession();

        var today = DateTime.Today;
        var queue = await session.Query<DailyQueue>()
            .FirstOrDefaultAsync(q => q.DailyToken == req.Token && q.QueueDate == today, ct);

        if (queue is null)
        {
            ThrowError("Invalid or expired queue link", 404);
        }

        var tenant = await session.LoadAsync<Tenant>(queue.TenantId, ct);
        if (tenant is null || !tenant.QueueEnabled)
        {
            ThrowError("Queue is not available", 404);
        }

        Response = new QueueInfoResponse
        {
            TenantName = tenant.Name,
            QueueDate = queue.QueueDate,
            CurrentWaitingCount = queue.Entries.Count(e => e.Status == QueueEntryStatus.Waiting),
            AverageServiceTimeMinutes = tenant.QueueAverageServiceTimeMinutes
        };
    }
}
