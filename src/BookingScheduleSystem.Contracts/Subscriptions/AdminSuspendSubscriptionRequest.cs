using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record AdminSuspendSubscriptionRequest
{
    public required TenantId TenantId { get; init; }
    public required string Reason { get; init; }
}
