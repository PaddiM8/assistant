namespace Assistant.Services;

public class TimeService(IConfiguration configuration)
{
    public TimeZoneInfo TimeZone { get; } = TimeZoneInfo.FindSystemTimeZoneById(configuration.GetValue<string>("Timezone")!);

    public DateTime GetNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZone);
    }

    public DateTime ToLocal(DateTime dateTimeUtc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dateTimeUtc, DateTimeKind.Utc), TimeZone);
    }
}
