using BookingScheduleSystem.Contracts.Analytics;

namespace BookingScheduleSystem.Web.Services;

public interface IAdminDashboardService
{
    Task<PlatformSummaryResponse?> GetPlatformSummaryAsync();
}
