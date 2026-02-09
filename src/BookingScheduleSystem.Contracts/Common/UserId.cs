namespace BookingScheduleSystem.Contracts.Common;

/// <summary>
/// Strongly-typed user identifier
/// </summary>
public readonly record struct UserId(Guid Value)
{
    public static UserId New() => new(Guid.NewGuid());
    public static UserId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
