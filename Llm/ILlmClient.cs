namespace Assistant.Llm;

public interface ILlmClient
{
    Task<LlmResponse> SendAsync(string message, string userIdentifier, IEnumerable<string>? fallbackHistory = null);

    Task<LlmResponse> SendSelfPromptAsync(string prompt, string userIdentifier);

    void AddAssistantMessageToHistory(string message);
}
