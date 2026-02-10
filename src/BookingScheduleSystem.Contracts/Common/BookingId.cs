using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingScheduleSystem.Contracts.Common;

[JsonConverter(typeof(BookingIdJsonConverter))]
public readonly record struct BookingId(Guid Value)
{
    public static BookingId New() => new(Guid.NewGuid());
    public static BookingId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public sealed class BookingIdJsonConverter : JsonConverter<BookingId>
{
    public override BookingId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, BookingId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
