namespace BookingScheduleSystem.Contracts.Common;

public readonly record struct TenantSubscriptionId(Guid Value)
{
    public static TenantSubscriptionId New() => new(Guid.NewGuid());
    public static TenantSubscriptionId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
