using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Web;

namespace Assistant.Services;

public class WeatherService(
    IHttpClientFactory httpClientFactory,
    ILogger<WeatherService> logger,
    IConfiguration configuration
)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly ILogger<WeatherService> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public async Task<string> GetWeatherDataAsync(string locationName, DateTime startDateUtc, DateTime endDateUtc)
    {
        // Geocoding API
        var geocodingHttpClient = _httpClientFactory.CreateClient();
        var nominatimUserAgent = _configuration.GetSection("Nominatim").GetValue<string>("UserAgent");
        geocodingHttpClient.DefaultRequestHeaders.UserAgent.ParseAdd(nominatimUserAgent);
        var geocodingResponse = await geocodingHttpClient.GetAsync($"https://nominatim.openstreetmap.org/search?q={HttpUtility.UrlEncode(locationName)}&format=jsonv2");
        geocodingResponse.EnsureSuccessStatusCode();

        var geocodingNodes = (await geocodingResponse.Content.ReadFromJsonAsync<JsonNode>())!.AsArray();
        var latitude = geocodingNodes.FirstOrDefault()?["lat"]?.GetValue<string>();
        var longitude = geocodingNodes.FirstOrDefault()?["lon"]?.GetValue<string>();

        if (string.IsNullOrEmpty(latitude) || string.IsNullOrEmpty(longitude))
            throw new ArgumentException($"Couldn't find coordinates for '{locationName}'.");

        // Weather API
        var weatherHttpClient = _httpClientFactory.CreateClient();
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

        var response = await weatherHttpClient.GetAsync(queryBuilder.ToString());
        var responseString = await response.Content.ReadAsStringAsync();
        _logger.LogInformation("Received weather data: {ApiResponse}.", responseString);

        return responseString;
    }
}
