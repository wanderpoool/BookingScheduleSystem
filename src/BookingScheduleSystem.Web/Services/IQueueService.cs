using BookingScheduleSystem.Contracts.Common;
using BookingScheduleSystem.Contracts.Queue;

namespace BookingScheduleSystem.Web.Services;

public interface IQueueService
{
    // Provider endpoints
    Task<GetTodayQueueResponse?> GetTodayQueueAsync();
    Task<List<QueueEntryResponse>?> GetTodayQueueEntriesAsync();
    Task<QueueEntryResponse?> AddQueueEntryAsync(AddQueueEntryRequest request);
    Task<QueueEntryResponse?> UpdateEntryStatusAsync(Guid entryId, UpdateQueueEntryStatusRequest request);
    Task<QueueEntryResponse?> ReorderEntryAsync(Guid entryId, ReorderQueueEntryRequest request);

    // Public endpoints
    Task<QueueInfoResponse?> GetQueueInfoAsync(string token);
    Task<JoinQueueResponse?> JoinQueueAsync(JoinQueueRequest request);
    Task<QueueEntryResponse?> GetEntryStatusAsync(Guid entryId);
}
