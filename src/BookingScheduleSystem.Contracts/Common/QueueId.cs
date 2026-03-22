using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingScheduleSystem.Contracts.Common;

/// <summary>
/// Strongly-typed daily queue identifier
/// </summary>
[JsonConverter(typeof(QueueIdJsonConverter))]
public readonly record struct QueueId(Guid Value)
{
    public static QueueId New() => new(Guid.NewGuid());
    public static QueueId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public sealed class QueueIdJsonConverter : JsonConverter<QueueId>
{
    public override QueueId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, QueueId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
