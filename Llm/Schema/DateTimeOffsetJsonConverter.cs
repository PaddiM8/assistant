using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assistant.Llm.Schema;

public class DateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var now = DateTime.Now;

        return DateTimeOffset
            .Parse(reader.GetString()!)
            .AddMinutes(now.Minute)
            .AddSeconds(now.Second);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O"));
    }
}
