namespace BookingScheduleSystem.Contracts.Common;

/// <summary>
/// Strongly-typed tenant identifier
/// </summary>
public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}
