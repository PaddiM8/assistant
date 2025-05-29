using System.Text.Json;
using System.Text.Json.Serialization;
using Assistant.Services;

namespace Assistant.Llm.Schema;

public class DateTimeOffsetJsonConverter(TimeService timeService) : JsonConverter<DateTimeOffset>
{
    private readonly TimeService _timeService = timeService;

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var original = reader.GetDateTimeOffset().DateTime;


        return new DateTimeOffset(original, _timeService.TimeZone.GetUtcOffset(original));
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("O"));
    }
}
