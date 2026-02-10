using BookingScheduleSystem.Contracts.Schedules;

namespace BookingScheduleSystem.Web.Services;

public interface IScheduleService
{
    Task<ListPublicSchedulesResponse?> ListPublicSchedulesAsync(Guid tenantId, DateTime? startDate = null, DateTime? endDate = null, int page = 1, int pageSize = 20);
    Task<ScheduleResponse?> CreateScheduleAsync(CreateScheduleRequest request);
}
