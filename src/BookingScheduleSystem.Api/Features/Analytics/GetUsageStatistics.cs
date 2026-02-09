using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Api.Infrastructure.Schedules;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Analytics;

public sealed class UsageStatisticsResponse
{
    public required int TotalTenants { get; init; }
    public required int ActiveTenants { get; init; }
    public required int TrialTenants { get; init; }
    public required int SubscribedTenants { get; init; }
    public required int TotalUsers { get; init; }
    public required int TotalProviders { get; init; }
    public required int TotalBookingsToday { get; init; }
    public required int TotalBookingsThisMonth { get; init; }
    public required int TotalBookingsAllTime { get; init; }
    public required int TotalSchedulesToday { get; init; }
    public required int TotalSchedulesThisMonth { get; init; }
    public required int TotalSchedulesAllTime { get; init; }
    public required List<TenantUsageDto> TopTenantsByBookings { get; init; }
}

public sealed class TenantUsageDto
{
    public required string TenantName { get; init; }
    public required int BookingsThisMonth { get; init; }
    public required int SchedulesThisMonth { get; init; }
    public required bool IsInTrial { get; init; }
}

public sealed class GetUsageStatistics : EndpointWithoutRequest<UsageStatisticsResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/analytics/usage");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("Analytics")
            .WithSummary("Get usage statistics (Admin)")
            .WithDescription("Retrieves comprehensive usage statistics across all tenants. Admin only."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        // Tenant statistics
        var allTenants = await session.Query<Tenant>().ToListAsync(ct);
        var totalTenants = allTenants.Count;
        var activeTenants = allTenants.Count(t => t.IsActive);
        var trialTenants = allTenants.Count(t => t.IsInTrial);
        var subscribedTenants = totalTenants - trialTenants;

        // User statistics
        var allUsers = await session.Query<User>().ToListAsync(ct);
        var totalUsers = allUsers.Count;
        var totalProviders = allUsers.Count(u => u.IsProvider);

        // Booking statistics
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var firstDayOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var firstDayOfNextMonth = firstDayOfMonth.AddMonths(1);

        var allBookings = await session.Query<Booking>()
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToListAsync(ct);

        var totalBookingsToday = allBookings.Count(b => b.BookedAt >= today && b.BookedAt < tomorrow);
        var totalBookingsThisMonth = allBookings.Count(b => b.BookedAt >= firstDayOfMonth && b.BookedAt < firstDayOfNextMonth);
        var totalBookingsAllTime = allBookings.Count;

        // Schedule statistics
        var allSchedules = await session.Query<Schedule>()
            .Where(s => s.IsActive)
            .ToListAsync(ct);

        var totalSchedulesToday = allSchedules.Count(s => s.CreatedAt >= today && s.CreatedAt < tomorrow);
        var totalSchedulesThisMonth = allSchedules.Count(s => s.CreatedAt >= firstDayOfMonth && s.CreatedAt < firstDayOfNextMonth);
        var totalSchedulesAllTime = allSchedules.Count;

        // Top tenants by bookings this month
        var tenantBookingsThisMonth = allBookings
            .Where(b => b.BookedAt >= firstDayOfMonth && b.BookedAt < firstDayOfNextMonth)
            .GroupBy(b => b.TenantId)
            .Select(group => new
            {
                TenantId = group.Key,
                BookingCount = group.Count()
            })
            .OrderByDescending(x => x.BookingCount)
            .Take(10)
            .ToList();

        var tenantIdGuids = tenantBookingsThisMonth.Select(x => x.TenantId.Value).ToArray();
        var topTenants = await session.Query<Tenant>()
            .Where(t => tenantIdGuids.Contains(t.Id.Value))
            .ToListAsync(ct);
        var tenantDict = topTenants.ToDictionary(t => t.Id, t => t);

        var topTenantsByBookings = tenantBookingsThisMonth
            .Select(x =>
            {
                var tenant = tenantDict.GetValueOrDefault(x.TenantId);
                var schedulesForTenant = allSchedules.Count(s =>
                    s.TenantId == x.TenantId &&
                    s.CreatedAt >= firstDayOfMonth &&
                    s.CreatedAt < firstDayOfNextMonth);

                return new TenantUsageDto
                {
                    TenantName = tenant?.Name ?? "Unknown",
                    BookingsThisMonth = x.BookingCount,
                    SchedulesThisMonth = schedulesForTenant,
                    IsInTrial = tenant?.IsInTrial ?? false
                };
            })
            .ToList();

        Response = new UsageStatisticsResponse
        {
            TotalTenants = totalTenants,
            ActiveTenants = activeTenants,
            TrialTenants = trialTenants,
            SubscribedTenants = subscribedTenants,
            TotalUsers = totalUsers,
            TotalProviders = totalProviders,
            TotalBookingsToday = totalBookingsToday,
            TotalBookingsThisMonth = totalBookingsThisMonth,
            TotalBookingsAllTime = totalBookingsAllTime,
            TotalSchedulesToday = totalSchedulesToday,
            TotalSchedulesThisMonth = totalSchedulesThisMonth,
            TotalSchedulesAllTime = totalSchedulesAllTime,
            TopTenantsByBookings = topTenantsByBookings
        };
    }
}
