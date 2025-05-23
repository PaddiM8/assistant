using OpenAI.Embeddings;
using Pgvector;

namespace Assistant.Llm;

public class OpenAiEmbeddingClient : IEmbeddingClient
{
    private readonly ILogger<OpenAiEmbeddingClient> _logger;
    private readonly EmbeddingClient _client;

    public OpenAiEmbeddingClient(IConfiguration configuration, ILogger<OpenAiEmbeddingClient> logger)
    {
        string apiKey = configuration.GetSection("OpenAi").GetValue<string>("ApiKey")
            ?? throw new ArgumentException("Missing API key for OpenAI.");

        _logger = logger;
        _client = new EmbeddingClient("text-embedding-3-large", apiKey);
    }

    public async Task<Vector> GetEmbeddingAsync(string input)
    {
        _logger.LogInformation("Generating embedding for: '{Input}'.", input);

        var options = new EmbeddingGenerationOptions
        {
            Dimensions = 3072,
        };
        var result = await _client.GenerateEmbeddingAsync(input, options);

        return new Vector(result.Value.ToFloats());
    }
}
