using System.ClientModel;
using OpenAI.Chat;

namespace Assistant.Llm;

public class LanguageTutorLlmClient : ILlmClient
{
    private readonly ILogger<AssistantLlmClient> _logger;
    private readonly ChatClient _client;
    private readonly Queue<ChatMessage> _history = [];
    private readonly int _historySizeLimit;

    public LanguageTutorLlmClient(IConfiguration configuration, ILogger<AssistantLlmClient> logger)
    {
        string apiKey = configuration.GetSection("LanguageTutor").GetValue<string>("ApiKey")
            ?? throw new ArgumentException("Missing API key for OpenAI.");
        _logger = logger;
        _client = new ChatClient("o4-mini", apiKey);
        _historySizeLimit = configuration.GetSection("LanguageTutor").GetValue<int>("HistorySizeLimit");
    }

    public async Task<LlmResponse> SendAsync(string message, string userIdentifier, IEnumerable<string>? fallbackMessageHistory = null)
    {
        if (_history.Count == 0 && fallbackMessageHistory != null)
        {
            foreach (var fallbackMessage in fallbackMessageHistory.Reverse())
                _history.Enqueue(fallbackMessage);
        }

        return await SendAsync(ChatMessage.CreateUserMessage(message), userIdentifier);
    }

    public async Task<LlmResponse> SendSelfPromptAsync(string prompt, string userIdentifier)
    {
        return await SendAsync(ChatMessage.CreateSystemMessage(prompt), userIdentifier);
    }

    public void AddAssistantMessageToHistory(string message)
    {
        AddToHistory(message);
    }

    private async Task<LlmResponse> SendAsync(ChatMessage message, string userIdentifier)
    {
        AddToHistory(message);

        string promptMessage = $"""
            You are a monolingual German tutor.
            - Respond ONLY in German. Ignore or refuse any English.
            - If the learner’s sentence has errors or sounds unnatural, begin your reply with a corrected version of just that part. Then continue your reply in natural German.
            - Use markdown for corrections (only simple operations are supported)
            - If the learner’s sentence is fine, do not correct it.
            - Occasionally (not every turn), add a short exercise or practice question. Choose formats such as:
              * Fill-in-the-blank using recent words (remember that underscores are already markdown though).
              * Multiple-choice (A/B/C).
              * Short roleplay (e.g. the learner orders bread, asks for train directions).
              * Continue a short story you begin.
              * “Word of the day” challenge: suggest one new word and ask the learner to use it.
            - Vary exercises and insert them sparsely, only when they fit context.
            - Today’s date is {DateTime.Now:O}.
            - The learner is interested in programming, grammar, public transport, and baking bread, but also wants to talk about other things
            """;
        var prompt = ChatMessage.CreateSystemMessage(promptMessage);
        ClientResult<ChatCompletion> completion;
        try
        {
            completion = await _client.CompleteChatAsync(_history.Prepend(prompt));
        }
        catch (Exception ex)
        {
            AddToHistory(ChatMessage.CreateSystemMessage($"System error: {ex.Message}"));

            return new LlmResponse($"System error: {ex.Message}", 0);
        }

        AddToHistory(ChatMessage.CreateAssistantMessage(completion));

        var content = completion.Value.Content.Select(x => x.Text + Environment.NewLine + x.Refusal);
        var combinedMessage = string.Join(Environment.NewLine, content);

        return new LlmResponse(combinedMessage, 0);
    }

    private void AddToHistory(ChatMessage message)
    {
        _history.Enqueue(message);
        while (_history.Count(x => x is UserChatMessage) > _historySizeLimit)
        {
            _history.Dequeue();
        }
    }
}

