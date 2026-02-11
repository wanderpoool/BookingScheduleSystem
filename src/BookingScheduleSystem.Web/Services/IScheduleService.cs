using BookingScheduleSystem.Contracts.Schedules;

namespace BookingScheduleSystem.Web.Services;

public interface IScheduleService
{
    Task<ListPublicSchedulesResponse?> ListPublicSchedulesAsync(Guid tenantId, DateTime? startDate = null, DateTime? endDate = null, int page = 1, int pageSize = 20);
    Task<ListSchedulesResponse?> ListSchedulesAsync(Guid? providerId = null, DateTime? startDate = null, DateTime? endDate = null, bool? isActive = null, int page = 1, int pageSize = 20);
    Task<ScheduleResponse?> GetScheduleAsync(Guid scheduleId);
    Task<ScheduleResponse?> CreateScheduleAsync(CreateScheduleRequest request);
    Task<ScheduleResponse?> UpdateScheduleAsync(Guid scheduleId, UpdateScheduleRequest request);
    Task DeleteScheduleAsync(Guid scheduleId);
}
