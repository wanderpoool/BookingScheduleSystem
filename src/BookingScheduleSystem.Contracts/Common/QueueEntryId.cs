using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingScheduleSystem.Contracts.Common;

/// <summary>
/// Strongly-typed queue entry identifier
/// </summary>
[JsonConverter(typeof(QueueEntryIdJsonConverter))]
public readonly record struct QueueEntryId(Guid Value)
{
    public static QueueEntryId New() => new(Guid.NewGuid());
    public static QueueEntryId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public sealed class QueueEntryIdJsonConverter : JsonConverter<QueueEntryId>
{
    public override QueueEntryId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, QueueEntryId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
