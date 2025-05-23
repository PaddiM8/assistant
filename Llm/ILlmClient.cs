namespace Assistant.Llm;

public interface ILlmClient
{
    Task<LlmResponse> SendAsync(string message, IEnumerable<string>? fallbackHistory = null);

    Task<LlmResponse> SendSelfPromptAsync(string prompt);

    void AddAssistantMessageToHistory(string message);
}
