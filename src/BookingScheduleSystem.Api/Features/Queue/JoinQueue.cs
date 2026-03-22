using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Queue;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Queue;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Queue;

/// <summary>
/// POST /api/queue/join — Public endpoint for customers to join a queue.
/// Uses optimistic concurrency with retry to prevent duplicate queue numbers.
/// </summary>
public sealed class JoinQueue : Endpoint<JoinQueueRequest, JoinQueueResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Post("/api/queue/join");
        AllowAnonymous();
        Throttle(10, 60);
        Description(d => d
            .WithTags("Queue")
            .WithSummary("Join a queue")
            .WithDescription("Public endpoint. Customer joins a queue using a daily token and phone number."));
    }

    public override async Task HandleAsync(JoinQueueRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.DailyToken))
            ThrowError("Queue token is required", 400);

        if (string.IsNullOrWhiteSpace(req.PhoneNumber) || req.PhoneNumber.Length > 20)
            ThrowError("Valid phone number is required", 400);

        const int maxRetries = 3;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await using var session = DocumentStore.LightweightSession();

                var today = DateTime.Today;
                var queue = await session.Query<DailyQueue>()
                    .FirstOrDefaultAsync(q => q.DailyToken == req.DailyToken && q.QueueDate == today, ct);

                if (queue is null)
                    ThrowError("Invalid or expired queue link", 404);

                var tenant = await session.LoadAsync<Tenant>(queue.TenantId, ct);
                if (tenant is null || !tenant.QueueEnabled)
                    ThrowError("Queue is not available", 404);

                var existingEntry = queue.Entries.FirstOrDefault(e =>
                    e.PhoneNumber == req.PhoneNumber &&
                    e.Status is QueueEntryStatus.Waiting or QueueEntryStatus.Called or QueueEntryStatus.InService);

                if (existingEntry is not null)
                    ThrowError("This phone number is already in the queue", 409);

                var entry = new QueueEntry
                {
                    Id = QueueEntryId.New(),
                    QueueNumber = queue.NextQueueNumber,
                    CustomerName = req.CustomerName?.Trim()[..Math.Min(req.CustomerName.Trim().Length, 100)],
                    PhoneNumber = req.PhoneNumber.Trim(),
                    Status = QueueEntryStatus.Waiting,
                    JoinedAt = DateTime.Now
                };

                queue.Entries.Add(entry);
                queue.NextQueueNumber++;
                session.Update(queue);
                await session.SaveChangesAsync(ct);

                var position = QueueHelper.CalculatePosition(queue, entry);
                var eta = QueueHelper.CalculateEstimatedWaitMinutes(queue, entry, tenant.QueueAverageServiceTimeMinutes);

                Logger.LogInformation(
                    "Customer joined queue {QueueId}, Entry {EntryId}, Number {QueueNumber}, TenantId {TenantId}",
                    queue.Id, entry.Id, entry.QueueNumber, queue.TenantId);

                HttpContext.Response.StatusCode = 201;
                Response = new JoinQueueResponse
                {
                    EntryId = entry.Id,
                    QueueNumber = entry.QueueNumber,
                    PositionInQueue = position,
                    EstimatedWaitMinutes = eta
                };
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && QueueConcurrencyHelper.IsConcurrencyException(ex))
            {
                Logger.LogWarning("Concurrency conflict joining queue, retry {Attempt}/{Max}", attempt + 1, maxRetries);
                await Task.Delay(Random.Shared.Next(20, 80), ct);
            }
        }
    }
}
