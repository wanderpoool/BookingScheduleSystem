namespace BookingScheduleSystem.Contracts.Common;

public readonly record struct SubscriptionPlanId(Guid Value)
{
    public static SubscriptionPlanId New() => new(Guid.NewGuid());
    public static SubscriptionPlanId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
