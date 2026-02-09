namespace BookingScheduleSystem.Contracts.Common;

public readonly record struct ScheduleId(Guid Value)
{
    public static ScheduleId New() => new(Guid.NewGuid());
    public static ScheduleId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
