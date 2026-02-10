using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingScheduleSystem.Contracts.Common;

/// <summary>
/// Strongly-typed tenant identifier
/// </summary>
[JsonConverter(typeof(TenantIdJsonConverter))]
public readonly record struct TenantId(Guid Value)
{
    public static TenantId New() => new(Guid.NewGuid());
    public static TenantId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public sealed class TenantIdJsonConverter : JsonConverter<TenantId>
{
    public override TenantId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, TenantId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
