using BookingScheduleSystem.Contracts.Subscriptions;

namespace BookingScheduleSystem.Web.Services;

public interface IAdminSubscriptionService
{
    Task<ListAllSubscriptionsResponse?> ListAllSubscriptionsAsync(
        SubscriptionStatusDto? status = null,
        string? planId = null,
        int page = 1,
        int pageSize = 20);

    Task<List<SubscriptionPlanResponse>?> ListAllPlansAsync(bool includeInactive = true);
    Task<TenantSubscriptionResponse?> AdminChangePlanAsync(AdminChangePlanRequest request);
    Task<TenantSubscriptionResponse?> AdminSuspendAsync(AdminSuspendSubscriptionRequest request);
    Task<TenantSubscriptionResponse?> AdminReactivateAsync(AdminReactivateSubscriptionRequest request);
}
