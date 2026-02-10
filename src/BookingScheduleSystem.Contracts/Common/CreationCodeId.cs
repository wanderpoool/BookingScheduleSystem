using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingScheduleSystem.Contracts.Common;

/// <summary>
/// Strongly-typed creation code identifier
/// </summary>
[JsonConverter(typeof(CreationCodeIdJsonConverter))]
public readonly record struct CreationCodeId(Guid Value)
{
    public static CreationCodeId New() => new(Guid.NewGuid());
    public static CreationCodeId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public sealed class CreationCodeIdJsonConverter : JsonConverter<CreationCodeId>
{
    public override CreationCodeId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, CreationCodeId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
