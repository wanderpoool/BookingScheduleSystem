namespace BookingScheduleSystem.Api.Infrastructure.Queue;

/// <summary>
/// Detects Marten optimistic concurrency exceptions regardless of version-specific exception types.
/// </summary>
public static class QueueConcurrencyHelper
{
    public static bool IsConcurrencyException(Exception ex)
    {
        // Marten wraps concurrency violations in various exception types depending on version.
        // Check the exception type name to avoid hard dependency on a specific namespace.
        if (ex.GetType().Name.Contains("Concurrency", StringComparison.OrdinalIgnoreCase))
            return true;

        // PostgreSQL serialization failure (error code 40001)
        if (ex.InnerException?.GetType().Name == "PostgresException")
            return true;

        return false;
    }
}
