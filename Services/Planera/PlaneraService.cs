using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Assistant.Services.Planera;

public class PlaneraService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IConfiguration _configuration = configuration;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task<List<PlaneraTicket>> GetTicketsAsync(string projectSlug, PlaneraTicketFilter? filter = null)
    {
        var httpClient = GetClient();

        // Build query
        var planeraConfig = _configuration.GetSection("Planera");
        var url = planeraConfig.GetValue<string>("Url")?.TrimEnd('/');
        var username = planeraConfig.GetValue<string>("Username");

        var queryBuilder = new StringBuilder();
        queryBuilder.Append(url);
        queryBuilder.Append($"/api/tickets/{username}/{projectSlug}");

        if (filter.HasValue)
            queryBuilder.Append($"?filter={filter}");

        // Make request
        var response = await httpClient.GetAsync(queryBuilder.ToString());
        response.EnsureSuccessStatusCode();

        var node = await response.Content.ReadFromJsonAsync<JsonObject>();
        if (node?.TryGetPropertyValue("tickets", out var ticketNodes) is true)
            return ticketNodes.Deserialize<List<PlaneraTicket>>(_jsonSerializerOptions) ?? [];

        throw new Exception($"Received invalid response from Planera API: {node?.ToJsonString()}");
    }

    public async Task<int> CreateTicketAsync(string title, string description, PlaneraTicketPriority priority, string projectId)
    {
        var httpClient = GetClient();

        // Build query
        var planeraConfig = _configuration.GetSection("Planera");
        var url = planeraConfig.GetValue<string>("Url")?.TrimEnd('/');
        var username = planeraConfig.GetValue<string>("Username");

        var queryBuilder = new StringBuilder();
        queryBuilder.Append(url);
        queryBuilder.Append($"/api/tickets/{projectId}");

        // Make request
        var model = new
        {
            Title = title,
            Description = description,
            Priority = priority,
        };

        var content = new StringContent(JsonSerializer.Serialize(model), Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(queryBuilder.ToString(), content);
        response.EnsureSuccessStatusCode();

        var node = await response.Content.ReadFromJsonAsync<JsonObject>();

        return node.Deserialize<PlaneraTicket>(_jsonSerializerOptions)?.Id ??
            throw new Exception($"Received inv alid response from Planera API: {node?.ToJsonString()}");
    }

    public async Task DeleteTicketAsync(string projectId, string id)
    {
        var httpClient = GetClient();

        var planeraConfig = _configuration.GetSection("Planera");
        var url = planeraConfig.GetValue<string>("Url")?.TrimEnd('/');
        var response = await httpClient.DeleteAsync($"{url}/api/tickets/{projectId}/{id}");
        response.EnsureSuccessStatusCode();
    }

    private HttpClient GetClient()
    {
        var planeraConfig = _configuration.GetSection("Planera");
        var httpClient = _httpClientFactory.CreateClient();
        var token = planeraConfig.GetValue<string>("Token");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Pat", token);

        return httpClient;
    }
}
