using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingScheduleSystem.Contracts.Common;

[JsonConverter(typeof(ScheduleIdJsonConverter))]
public readonly record struct ScheduleId(Guid Value)
{
    public static ScheduleId New() => new(Guid.NewGuid());
    public static ScheduleId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public sealed class ScheduleIdJsonConverter : JsonConverter<ScheduleId>
{
    public override ScheduleId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, ScheduleId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
