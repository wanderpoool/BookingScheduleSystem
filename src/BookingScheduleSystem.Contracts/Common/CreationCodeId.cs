namespace BookingScheduleSystem.Contracts.Common;

/// <summary>
/// Strongly-typed creation code identifier
/// </summary>
public readonly record struct CreationCodeId(Guid Value)
{
    public static CreationCodeId New() => new(Guid.NewGuid());
    public static CreationCodeId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
