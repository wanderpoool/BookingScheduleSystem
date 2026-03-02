using System.Text.Json;
using System.Text.Json.Serialization;

namespace BookingScheduleSystem.Contracts.Common;

[JsonConverter(typeof(UserTenantIdJsonConverter))]
public readonly record struct UserTenantId(Guid Value)
{
    public static UserTenantId New() => new(Guid.NewGuid());
    public static UserTenantId Parse(string value) => new(Guid.Parse(value));
    public override string ToString() => Value.ToString();
}

public sealed class UserTenantIdJsonConverter : JsonConverter<UserTenantId>
{
    public override UserTenantId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, UserTenantId value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.Value);
}
