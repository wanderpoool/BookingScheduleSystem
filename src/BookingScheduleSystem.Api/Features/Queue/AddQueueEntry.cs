using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Queue;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Queue;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Queue;

/// <summary>
/// POST /api/queue/entries — Provider manually adds a customer to the queue.
/// Uses optimistic concurrency with retry to prevent duplicate queue numbers.
/// </summary>
public sealed class AddQueueEntry : Endpoint<AddQueueEntryRequest, QueueEntryResponse>
{
    public required IDocumentStore DocumentStore { get; init; }
    public required ITenantContext TenantContext { get; init; }

    public override void Configure()
    {
        Post("/api/queue/entries");
        Policies("TenantUser");
        Description(d => d
            .WithTags("Queue")
            .WithSummary("Add customer to queue")
            .WithDescription("Provider manually adds a customer to today's queue."));
    }

    public override async Task HandleAsync(AddQueueEntryRequest req, CancellationToken ct)
    {
        var tenantId = TenantContext.CurrentTenantId;
        if (tenantId is null)
            ThrowError("Tenant context required", 400);

        if (string.IsNullOrWhiteSpace(req.PhoneNumber) || req.PhoneNumber.Length > 20)
            ThrowError("Valid phone number is required", 400);

        if (string.IsNullOrWhiteSpace(req.CustomerName) || req.CustomerName.Length > 100)
            ThrowError("Customer name is required (max 100 characters)", 400);

        const int maxRetries = 3;
        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await using var session = DocumentStore.LightweightSession();

                var tenant = await session.LoadAsync<Infrastructure.MultiTenancy.Tenant>(tenantId.Value, ct);
                if (tenant is null || !tenant.QueueEnabled)
                    ThrowError("Queue is not enabled for this organization", 400);

                var today = DateTime.Today;
                var queue = await session.Query<DailyQueue>()
                    .FirstOrDefaultAsync(q => q.TenantId == tenantId.Value && q.QueueDate == today, ct);

                if (queue is null)
                {
                    queue = new DailyQueue
                    {
                        Id = QueueId.New(),
                        TenantId = tenantId.Value,
                        QueueDate = today,
                        DailyToken = QueueHelper.GenerateDailyToken()
                    };
                    session.Store(queue);
                }

                var existingEntry = queue.Entries.FirstOrDefault(e =>
                    e.PhoneNumber == req.PhoneNumber &&
                    e.Status is QueueEntryStatus.Waiting or QueueEntryStatus.Called or QueueEntryStatus.InService);

                if (existingEntry is not null)
                    ThrowError("This phone number is already in the queue", 409);

                var entry = new QueueEntry
                {
                    Id = QueueEntryId.New(),
                    QueueNumber = queue.NextQueueNumber,
                    CustomerName = req.CustomerName.Trim(),
                    PhoneNumber = req.PhoneNumber.Trim(),
                    Status = QueueEntryStatus.Waiting,
                    IsPriority = req.IsPriority,
                    JoinedAt = DateTime.Now
                };

                queue.Entries.Add(entry);
                queue.NextQueueNumber++;
                session.Update(queue);
                await session.SaveChangesAsync(ct);

                Logger.LogInformation(
                    "Provider added entry {EntryId} to queue {QueueId}, Number {QueueNumber}, Priority {IsPriority}, TenantId {TenantId}",
                    entry.Id, queue.Id, entry.QueueNumber, entry.IsPriority, tenantId.Value);

                HttpContext.Response.StatusCode = 201;
                Response = QueueHelper.ToResponse(queue, entry, tenant.QueueAverageServiceTimeMinutes);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && QueueConcurrencyHelper.IsConcurrencyException(ex))
            {
                Logger.LogWarning("Concurrency conflict adding queue entry, retry {Attempt}/{Max}", attempt + 1, maxRetries);
                await Task.Delay(Random.Shared.Next(20, 80), ct);
            }
        }
    }
}
