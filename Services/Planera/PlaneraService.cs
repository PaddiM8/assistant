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
        var planeraConfig = _configuration.GetSection("Planera");

        // Set up client and auth
        var httpClient = _httpClientFactory.CreateClient();
        var token = planeraConfig.GetValue<string>("Token");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Pat", token);

        // Build query
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
}
