using System.Collections.Concurrent;

namespace BookingScheduleSystem.Web.Services.Chatbot;

/// <summary>
/// Singleton service that tracks per-tenant Anthropic API usage across all circuits.
/// Enforces rolling hourly and daily call limits to prevent budget overruns.
/// </summary>
public sealed class ChatbotUsageTracker
{
    private readonly ConcurrentDictionary<Guid, TenantUsage> _tenantUsage = new();
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatbotUsageTracker> _logger;

    public ChatbotUsageTracker(IConfiguration configuration, ILogger<ChatbotUsageTracker> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Checks whether the tenant has exceeded their API call limits.
    /// Returns a user-friendly message if blocked, null if allowed.
    /// </summary>
    public string? CheckTenantLimit(Guid tenantId)
    {
        var maxPerHour = _configuration.GetValue("Anthropic:RateLimiting:TenantMaxCallsPerHour", 200);
        var maxPerDay = _configuration.GetValue("Anthropic:RateLimiting:TenantMaxCallsPerDay", 1000);

        var usage = _tenantUsage.GetOrAdd(tenantId, _ => new TenantUsage());
        var now = DateTime.UtcNow;

        lock (usage)
        {
            usage.PruneExpired(now);

            if (usage.HourlyCount >= maxPerHour)
            {
                _logger.LogWarning("Tenant {TenantId} exceeded hourly API call limit ({Limit})",
                    tenantId, maxPerHour);
                return "Our booking assistant is very busy right now. Please try again in a little while.";
            }

            if (usage.DailyCount >= maxPerDay)
            {
                _logger.LogWarning("Tenant {TenantId} exceeded daily API call limit ({Limit})",
                    tenantId, maxPerDay);
                return "Our booking assistant has reached its daily limit. Please try again tomorrow or contact the business directly.";
            }
        }

        return null;
    }

    /// <summary>
    /// Records a successful Anthropic API call for the given tenant.
    /// Called after each API response (including tool-use iterations).
    /// </summary>
    public void RecordApiCall(Guid tenantId)
    {
        var usage = _tenantUsage.GetOrAdd(tenantId, _ => new TenantUsage());
        var now = DateTime.UtcNow;

        lock (usage)
        {
            usage.CallTimestamps.Enqueue(now);
            _logger.LogDebug("Recorded API call for tenant {TenantId}. Hourly: {Hourly}, Daily: {Daily}",
                tenantId, usage.HourlyCount, usage.DailyCount);
        }
    }

    private sealed class TenantUsage
    {
        public Queue<DateTime> CallTimestamps { get; } = new();

        public int HourlyCount
        {
            get
            {
                var cutoff = DateTime.UtcNow.AddHours(-1);
                return CallTimestamps.Count(t => t >= cutoff);
            }
        }

        public int DailyCount
        {
            get
            {
                var cutoff = DateTime.UtcNow.AddHours(-24);
                return CallTimestamps.Count(t => t >= cutoff);
            }
        }

        /// <summary>
        /// Removes timestamps older than 24 hours to prevent unbounded growth.
        /// </summary>
        public void PruneExpired(DateTime now)
        {
            var cutoff = now.AddHours(-24);
            while (CallTimestamps.Count > 0 && CallTimestamps.Peek() < cutoff)
            {
                CallTimestamps.Dequeue();
            }
        }
    }
}
