namespace BookingScheduleSystem.Contracts.Common;

public readonly record struct BookingId(Guid Value)
{
    public static BookingId New() => new(Guid.NewGuid());
    public static BookingId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
