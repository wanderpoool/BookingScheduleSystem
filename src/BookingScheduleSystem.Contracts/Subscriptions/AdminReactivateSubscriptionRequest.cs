using BookingScheduleSystem.Contracts.Common;

namespace BookingScheduleSystem.Contracts.Subscriptions;

public sealed record AdminReactivateSubscriptionRequest
{
    public required TenantId TenantId { get; init; }
    public string? Reason { get; init; }
}
