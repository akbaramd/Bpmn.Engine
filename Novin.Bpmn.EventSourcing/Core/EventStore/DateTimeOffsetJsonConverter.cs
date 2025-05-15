using System;
using System.Text.Json;
using System.Text.Json.Serialization;

public class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var str = reader.GetString();
        if (DateTimeOffset.TryParse(str, out var dto))
            return dto;

        throw new JsonException($"Cannot convert \"{str}\" to DateTimeOffset.");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("o")); // استاندارد ISO 8601
    }
}