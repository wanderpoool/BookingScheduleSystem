using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingScheduleSystem.Contracts.Common;

[JsonConverter(typeof(SubscriptionPlanIdJsonConverter))]
public readonly record struct SubscriptionPlanId(Guid Value)
{
    public static SubscriptionPlanId New() => new(Guid.NewGuid());
    public static SubscriptionPlanId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public sealed class SubscriptionPlanIdJsonConverter : JsonConverter<SubscriptionPlanId>
{
    public override SubscriptionPlanId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, SubscriptionPlanId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
