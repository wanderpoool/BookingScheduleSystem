using BookingScheduleSystem.Api.Infrastructure.Auth;
using BookingScheduleSystem.Api.Infrastructure.Bookings;
using BookingScheduleSystem.Api.Infrastructure.MultiTenancy;
using BookingScheduleSystem.Contracts.Analytics;
using BookingScheduleSystem.Contracts.Common;
using FastEndpoints;
using Marten;

namespace BookingScheduleSystem.Api.Features.Analytics;

public sealed class GetPlatformSummary : EndpointWithoutRequest<PlatformSummaryResponse>
{
    public required IDocumentStore DocumentStore { get; init; }

    public override void Configure()
    {
        Get("/api/analytics/platform-summary");
        Roles("GlobalAdmin");
        Description(d => d
            .WithTags("Analytics")
            .WithSummary("Get platform summary (Admin)")
            .WithDescription("Retrieves platform-wide summary with per-tenant breakdowns. GlobalAdmin only."));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await using var session = DocumentStore.LightweightSession();

        var allTenants = await session.Query<Tenant>().ToListAsync(ct);
        var allUsers = await session.Query<User>().ToListAsync(ct);
        var allBookings = await session.Query<Booking>()
            .Where(b => b.Status != BookingStatus.Cancelled)
            .ToListAsync(ct);

        var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified);
        var tomorrow = today.AddDays(1);
        var firstDayOfMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var firstDayOfNextMonth = firstDayOfMonth.AddMonths(1);

        // Group users by tenant
        var usersByTenant = allUsers
            .Where(u => u.TenantId.HasValue && !u.IsGlobalAdmin)
            .GroupBy(u => u.TenantId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Group bookings by tenant
        var bookingsByTenant = allBookings
            .GroupBy(b => b.TenantId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var tenantStats = allTenants.Select(t =>
        {
            var users = usersByTenant.GetValueOrDefault(t.Id, []);
            var bookings = bookingsByTenant.GetValueOrDefault(t.Id, []);

            return new TenantStatsDto
            {
                TenantId = t.Id,
                TenantName = t.Name,
                CustomerCount = users.Count(u => !u.IsProvider),
                ProviderCount = users.Count(u => u.IsProvider),
                BookingCount = bookings.Count,
                IsInTrial = t.IsInTrial,
                IsActive = t.IsActive
            };
        }).ToList();

        var totalNonAdminUsers = allUsers.Count(u => u.TenantId.HasValue && !u.IsGlobalAdmin);

        Response = new PlatformSummaryResponse
        {
            TotalTenants = allTenants.Count,
            ActiveTenants = allTenants.Count(t => t.IsActive),
            TrialTenants = allTenants.Count(t => t.IsInTrial),
            TotalCustomers = allUsers.Count(u => u.TenantId.HasValue && !u.IsGlobalAdmin && !u.IsProvider),
            TotalProviders = allUsers.Count(u => u.TenantId.HasValue && u.IsProvider),
            TotalBookings = allBookings.Count,
            BookingsThisMonth = allBookings.Count(b => b.BookedAt >= firstDayOfMonth && b.BookedAt < firstDayOfNextMonth),
            BookingsToday = allBookings.Count(b => b.BookedAt >= today && b.BookedAt < tomorrow),
            TenantStats = tenantStats
        };
    }
}
