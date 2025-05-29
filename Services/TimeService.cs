namespace Assistant.Services;

public class TimeService(IConfiguration configuration)
{
    private readonly TimeZoneInfo _timeZone = TimeZoneInfo.FindSystemTimeZoneById(configuration.GetValue<string>("Timezone")!);

    public DateTime GetNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone);
    }

    public DateTime ToLocal(DateTime dateTimeUtc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(dateTimeUtc, _timeZone);
    }

    public DateTime ToUtc(DateTime dateTimeLocal)
    {
        return TimeZoneInfo.ConvertTimeToUtc(dateTimeLocal, _timeZone);
    }
}
