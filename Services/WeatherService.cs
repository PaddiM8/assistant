using System.Text;

namespace Assistant.Services;

public class WeatherService(IHttpClientFactory httpClientFactory, ILogger<WeatherService> logger)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<WeatherService> _logger = logger;

    public async Task<string> GetWeatherDataAsync(float longitude, float latitude, DateTime startDateUtc, DateTime endDateUtc)
    {
        var startDateString = startDateUtc.ToString("yyyy-MM-dd");
        var endDateString = endDateUtc.ToString("yyyy-MM-dd");

        var queryBuilder = new StringBuilder();
        if (startDateUtc < DateTime.UtcNow.AddDays(-7))
        {
            queryBuilder.Append("https://archive-api.open-meteo.com/v1/archive");
            queryBuilder.Append($"?latitude={latitude}");
            queryBuilder.Append($"&longitude={longitude}");
            queryBuilder.Append($"&start_date={startDateString}");
            queryBuilder.Append($"&end_date={endDateString}");
            queryBuilder.Append("&daily=temperature_2m_mean,temperature_2m_max,temperature_2m_min,apparent_temperature_mean,apparent_temperature_min,apparent_temperature_max,rain_sum,snowfall_sum,precipitation_hours,wind_speed_10m_max,precipitation_sum,daylight_duration");
            queryBuilder.Append("&hourly=temperature_2m,apparent_temperature,rain,snowfall,snow_depth");
        }
        else
        {
            queryBuilder.Append("https://api.open-meteo.com/v1/forecast");
            queryBuilder.Append($"?longitude={longitude}");
            queryBuilder.Append($"&latitude={latitude}");
            queryBuilder.Append($"&daily=temperature_2m_max,temperature_2m_min,apparent_temperature_max,apparent_temperature_min,rain_sum,showers_sum,snowfall_sum,precipitation_probability_max,wind_speed_10m_max");
            queryBuilder.Append($"&hourly=temperature_2m,precipitation_probability,rain,apparent_temperature,showers");
            queryBuilder.Append($"&start_date={startDateString}");
            queryBuilder.Append($"&end_date={endDateString}");
        }

        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.GetAsync(queryBuilder.ToString());
        var responseString = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Received weather data: {ApiResponse}.", responseString);

        return responseString;
    }
}
