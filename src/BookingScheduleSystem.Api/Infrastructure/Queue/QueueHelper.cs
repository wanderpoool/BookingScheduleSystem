using System.Security.Cryptography;
using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Queue;

namespace BookingScheduleSystem.Api.Infrastructure.Queue;

public static class QueueHelper
{
    /// <summary>
    /// Calculate estimated wait minutes for a given entry based on its position among waiting entries.
    /// </summary>
    public static int CalculateEstimatedWaitMinutes(DailyQueue queue, QueueEntry entry, int averageServiceTimeMinutes)
    {
        if (entry.Status != QueueEntryStatus.Waiting)
            return 0;

        var waitingEntries = queue.Entries
            .Where(e => e.Status == QueueEntryStatus.Waiting)
            .OrderBy(e => e.IsPriority ? 0 : 1)
            .ThenBy(e => e.QueueNumber)
            .ToList();

        var position = waitingEntries.FindIndex(e => e.Id == entry.Id);
        if (position < 0)
            return 0;

        // Account for entries currently being served
        var inServiceCount = queue.Entries.Count(e => e.Status is QueueEntryStatus.Called or QueueEntryStatus.InService);
        var effectivePosition = Math.Max(0, position - Math.Max(0, inServiceCount - 1));

        return effectivePosition * averageServiceTimeMinutes;
    }

    /// <summary>
    /// Calculate position in queue (1-based) for a waiting entry.
    /// </summary>
    public static int CalculatePosition(DailyQueue queue, QueueEntry entry)
    {
        if (entry.Status != QueueEntryStatus.Waiting)
            return 0;

        var waitingEntries = queue.Entries
            .Where(e => e.Status == QueueEntryStatus.Waiting)
            .OrderBy(e => e.IsPriority ? 0 : 1)
            .ThenBy(e => e.QueueNumber)
            .ToList();

        return waitingEntries.FindIndex(e => e.Id == entry.Id) + 1;
    }

    /// <summary>
    /// Map a QueueEntry to its response DTO.
    /// </summary>
    public static QueueEntryResponse ToResponse(DailyQueue queue, QueueEntry entry, int averageServiceTimeMinutes)
    {
        return new QueueEntryResponse
        {
            Id = entry.Id,
            QueueNumber = entry.QueueNumber,
            CustomerName = entry.CustomerName,
            PhoneNumber = entry.PhoneNumber,
            Status = entry.Status,
            IsPriority = entry.IsPriority,
            EstimatedWaitMinutes = CalculateEstimatedWaitMinutes(queue, entry, averageServiceTimeMinutes),
            JoinedAt = entry.JoinedAt,
            CalledAt = entry.CalledAt,
            CompletedAt = entry.CompletedAt
        };
    }

    /// <summary>
    /// Generate a cryptographically secure daily token (URL-safe, 12 chars).
    /// </summary>
    public static string GenerateDailyToken()
    {
        Span<byte> buffer = stackalloc byte[12];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace("+", "X")
            .Replace("/", "Y")
            .Replace("=", "")[..12];
    }
}
