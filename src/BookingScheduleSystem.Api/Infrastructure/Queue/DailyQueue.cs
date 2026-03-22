using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Queue;

namespace BookingScheduleSystem.Api.Infrastructure.Queue;

/// <summary>
/// Marten document representing a single day's queue for a tenant.
/// A new DailyQueue is created each day with a fresh daily token.
/// </summary>
public sealed class DailyQueue
{
    public QueueId Id { get; set; }
    public Guid Version { get; set; }
    public TenantId TenantId { get; set; }
    public DateTime QueueDate { get; set; }
    public string DailyToken { get; set; } = string.Empty;
    public int NextQueueNumber { get; set; } = 1;
    public List<QueueEntry> Entries { get; set; } = [];
}

/// <summary>
/// Embedded type within DailyQueue representing a single customer in line.
/// </summary>
public sealed class QueueEntry
{
    public QueueEntryId Id { get; set; }
    public int QueueNumber { get; set; }
    public string? CustomerName { get; set; }
    public required string PhoneNumber { get; set; }
    public QueueEntryStatus Status { get; set; } = QueueEntryStatus.Waiting;
    public bool IsPriority { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool NotificationSent { get; set; }
}
