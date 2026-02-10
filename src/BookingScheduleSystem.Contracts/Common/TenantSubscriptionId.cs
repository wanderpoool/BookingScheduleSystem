using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingScheduleSystem.Contracts.Common;

[JsonConverter(typeof(TenantSubscriptionIdJsonConverter))]
public readonly record struct TenantSubscriptionId(Guid Value)
{
    public static TenantSubscriptionId New() => new(Guid.NewGuid());
    public static TenantSubscriptionId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public sealed class TenantSubscriptionIdJsonConverter : JsonConverter<TenantSubscriptionId>
{
    public override TenantSubscriptionId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, TenantSubscriptionId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
