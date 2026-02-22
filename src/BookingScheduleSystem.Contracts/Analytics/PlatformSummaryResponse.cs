using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Analytics;

public sealed class PlatformSummaryResponse
{
    public required int TotalTenants { get; init; }
    public required int ActiveTenants { get; init; }
    public required int TrialTenants { get; init; }
    public required int TotalCustomers { get; init; }
    public required int TotalProviders { get; init; }
    public required int TotalBookings { get; init; }
    public required int BookingsThisMonth { get; init; }
    public required int BookingsToday { get; init; }
    public required List<TenantStatsDto> TenantStats { get; init; }
}

public sealed class TenantStatsDto
{
    public required TenantId TenantId { get; init; }
    public required string TenantName { get; init; }
    public required int CustomerCount { get; init; }
    public required int ProviderCount { get; init; }
    public required int BookingCount { get; init; }
    public required bool IsInTrial { get; init; }
    public required bool IsActive { get; init; }
}
