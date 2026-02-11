using BookingScheduleSystem.Contracts.Subscriptions;

namespace BookingScheduleSystem.Web.Services;

public interface ISubscriptionService
{
    Task<TenantSubscriptionResponse?> GetCurrentSubscriptionAsync();
    Task<List<SubscriptionPlanResponse>?> ListPlansAsync();
    Task<TenantSubscriptionResponse?> CancelSubscriptionAsync(string? reason = null);
}
