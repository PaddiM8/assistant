using Pgvector;

namespace Assistant.Llm;

public interface IEmbeddingClient
{
    Task<Vector> GetEmbeddingAsync(string content);
}
