using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Assistant.Services.Models;

namespace Assistant.Services;

public class HomeAssistantState
{
    public required string EntityId { get; set; }

    public string? State { get; set; }

    public Dictionary<string, JsonNode> Attributes { get; set; } = [];
}

public class HomeAssistantService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<HomeAssistantService> logger
)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<HomeAssistantService> _logger = logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<IEnumerable<LightState>> GetLightsAsync()
    {
        var httpClient = GetClient();
        var apiUrl = _configuration.GetSection("HomeAssistant").GetValue<string>("Url")!.TrimEnd('/');

        var response = await httpClient.GetAsync($"{apiUrl}/states");
        response.EnsureSuccessStatusCode();

        var homeAssistantStates = await response.Content.ReadFromJsonAsync<List<HomeAssistantState>>(_jsonSerializerOptions);

        return homeAssistantStates!
            .Where(x => x.EntityId.StartsWith("light."))
            .Select(BuildLightState);
    }

    public async Task<HomeAssistantState> GetStateAsync(string entityId)
    {
        var httpClient = GetClient();
        var apiUrl = _configuration.GetSection("HomeAssistant").GetValue<string>("Url")!.TrimEnd('/');

        var response = await httpClient.GetAsync($"{apiUrl}/states/{entityId}");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<HomeAssistantState>(_jsonSerializerOptions))!;
    }

    public async Task SetLightStateAsync(string entityId, bool isOn, int? brightnessPointChange, int? coldnessPointChange)
    {
        var automaticLightsOffData = new JsonObject
        {
            { "entity_id", "automation.update_lights" },
        };

        await CallServiceAsync("automation/turn_off", automaticLightsOffData);

        var node = new JsonObject
        {
            { "entity_id", entityId }
        };

        if (!isOn)
        {
            await CallServiceAsync("light/turn_off", node);

            return;
        }

        int? brightnessStep = null;
        if (brightnessPointChange.HasValue)
        {
            brightnessStep = (int)(255 * ((double)brightnessPointChange.Value / 100));
        }

        int? newTemperature = null;
        if (coldnessPointChange.HasValue)
        {
            var oldState = await GetStateAsync(entityId);
            oldState.Attributes.TryGetValue("color_temp_kelvin", out var oldColorTempKelvinNode);
            oldState.Attributes.TryGetValue("max_color_temp_kelvin", out var maxColorTempKelvinNode);
            oldState.Attributes.TryGetValue("min_color_temp_kelvin", out var minColorTempKelvinNode);
            var maxColorTempKelvin = maxColorTempKelvinNode?.GetValue<int>() ?? 0;
            var minColorTempKelvin = minColorTempKelvinNode?.GetValue<int>() ?? 0;
            var temperatureStep = (maxColorTempKelvin - minColorTempKelvin) * ((double)coldnessPointChange.Value / 100);

            newTemperature = (int)temperatureStep + oldColorTempKelvinNode?.GetValue<int>() ?? 0;
        }

        if (brightnessStep.HasValue)
           node.Add("brightness_step", brightnessStep.Value);

        if (newTemperature.HasValue)
            node.Add("kelvin", newTemperature.Value);

        await CallServiceAsync("light/turn_on", node);

        _logger.LogInformation(
            "Changed state of HomeAssistant entity {EntityId}. Brightness change: {Brightness}, New temperature: {Temperature}",
            entityId,
            brightnessStep,
            newTemperature
        );
    }

    public async Task ResetLightAsync(string entityId)
    {
        var node = new JsonObject();
        await CallServiceAsync("script/turn_on_lights", node);
    }

    private async Task CallServiceAsync(string name, JsonNode data)
    {
        var httpClient = GetClient();
        var apiUrl = _configuration.GetSection("HomeAssistant").GetValue<string>("Url")!.TrimEnd('/');

        var content = new StringContent(
            JsonSerializer.Serialize(data, _jsonSerializerOptions),
            Encoding.UTF8,
            new MediaTypeHeaderValue("application/json")
        );

        var response = await httpClient.PostAsync($"{apiUrl}/services/{name}", content);
        response.EnsureSuccessStatusCode();
    }

    private int ToPercentage(int? current, int max, int min = 0)
    {
        if (!current.HasValue)
            return 0;

        return (int)((double)current / (max - min) * 100);
    }

    private LightState BuildLightState(HomeAssistantState state)
    {
        state.Attributes.TryGetValue("brightness", out var brightnessNode);
        state.Attributes.TryGetValue("color_temp_kelvin", out var temperatureNode);
        state.Attributes.TryGetValue("max_color_temp_kelvin", out var maxColorTempKelvinNode);
        state.Attributes.TryGetValue("min_color_temp_kelvin", out var minColorTempKelvinNode);
        var maxColorTempKelvin = maxColorTempKelvinNode?.GetValue<int>() ?? 0;
        var minColorTempKelvin = minColorTempKelvinNode?.GetValue<int>() ?? 0;
        var description = state.EntityId.Split('.').ElementAtOrDefault(1)?.Replace("_", " ");
        if (state.EntityId == "light.automatic_lights")
        {
            description = "Combined entity for ALL lights";
        }

        return new LightState
        {
            EntityId = state.EntityId,
            IsOn = state.State == "on",
            Description = description,
            BrightnessPercentage = ToPercentage(brightnessNode?.GetValue<int>(), 255),
            ColdnessPercentage = ToPercentage(temperatureNode?.GetValue<int>(), maxColorTempKelvin, minColorTempKelvin),
        };
    }

    private HttpClient GetClient()
    {
        var client = _httpClientFactory.CreateClient();
        var token = _configuration.GetSection("HomeAssistant").GetValue<string>("Token")!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
