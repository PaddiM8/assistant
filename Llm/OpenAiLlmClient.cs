using System.Text.Json;
using Assistant.Llm.Schema;
using OpenAI;
using OpenAI.Chat;

namespace Assistant.Llm;

public class OpenAiLlmClient : ILlmClient
{
    private readonly ILogger<OpenAiLlmClient> _logger;
    private readonly ToolService _toolService;
    private readonly ChatClient _client;
    private readonly Queue<ChatMessage> _history = [];
    private readonly int _historySizeLimit;

    public OpenAiLlmClient(
        IConfiguration configuration,
        ILogger<OpenAiLlmClient> logger,
        ToolService toolService
    )
    {
        string apiKey = configuration.GetSection("OpenAi").GetValue<string>("ApiKey")
            ?? throw new ArgumentException("Missing API key for OpenAI.");
        _logger = logger;
        _toolService = toolService;
        _client = new ChatClient("o4-mini", apiKey);
        _historySizeLimit = configuration.GetSection("OpenAi").GetValue<int>("HistorySizeLimit");
    }

    public async Task<LlmResponse> SendAsync(string message, IEnumerable<string>? fallbackMessageHistory = null)
    {
        if (_history.Count == 0 && fallbackMessageHistory != null)
        {
            foreach (var fallbackMessage in fallbackMessageHistory.Reverse())
                _history.Enqueue(fallbackMessage);
        }

        return await SendAsync(ChatMessage.CreateUserMessage(message));
    }

    public async Task<LlmResponse> SendSelfPromptAsync(string prompt)
    {
        var message = $"""
            Another assistant wrote notes for themself but needs you to execute them instead.
            This conversation is invisible to the user, so if the task requires communicating
            with the user, you need to use the MessageUser function to talk to them.
            Regardless, you will need to use at least one function/tool since the user isn't
            here.

            Notes: '{prompt}'
            """;

        return await SendAsync(ChatMessage.CreateSystemMessage(message), isSelfPrompt: true);
    }

    public void AddAssistantMessageToHistory(string message)
    {
        AddToHistory(message);
    }

    private async Task<LlmResponse> SendAsync(ChatMessage message, bool isSelfPrompt = false)
    {
        var options = new ChatCompletionOptions();
        foreach (var tool in OpenAiUtils.GetTools())
            options.Tools.Add(tool);

        AddToHistory(message);

        var i = 0;
        var functionCallResponses = new List<ToolResponse>();
        do
        {
            var now = DateTimeOffset.Now.ToString("O");
            var dayOfWeek = DateTimeOffset.Now.DayOfWeek.ToString();
            var prompt = ChatMessage.CreateSystemMessage($"""
                You are a personal assistant that speaks English, German and Swedish, but you mostly reply in German. Use 24h local time
                and European spelling. Normally when I talk to you it's because I want something to be done, so you may not always
                need to ask for confirmation. Actions can typically be reverted. If you are missing some context, you should always
                query the vector database as a first step. Don't make assumptions without querying the vector database first. If you don't
                find it there, you may ask about it, unless the specifics aren't that important. But remember, DO NOT ask BEFORE querying the
                vector database first UNLESS it's situational. ALSO, prefer 'update' functions over removing and re-adding.

                Your job is to answer questions, perform tasks using the given tools, and to expand your own database of memories/facts about the user.
                Whenever I tell you something, or when I reply to a question you asked, ask yourself if it's worth putting your personal knowledge base,
                and if so, add it there (vector database AssistantMemory). Enums are string-based. ALL memories and memory searches MUST be in English.

                Things like reminders can be queried with the vector search function. Make sure to contiuously add your own AssistantMemory entries
                that may help you assist me in the future. For example, if you have to ask me about something general before completing a task, make
                sure to also save what you learned from that in the vector database. Make sure to put all the relevant context in the vector memories,
                and make sure to phrase them in a way that makes them valid in the long-term, eg. "X years old" to "born year X".
                DON'T add vector memories for very temporary things, like things I tell you to do.
                Similarly, remember to search for AssistantMemory entries before asking me about general things that you may have stored in the past.
                If you find conflicting memories, you may ask the user about them. Keep it mind that assistant memories won't *make* you do things,
                only provide context when you have already started doing tasks.

                You have saved some vector memories in the past, so don't forget to query them. Also, remember to look for memories that may exist
                that should be *deleted* due to no longer being relevant, to keep the database clean and up to date.

                You have access to some external tools/functions that can help you perform some tasks, but keep in mind that those are the
                ONLY external tasks you can perform. It is important to admit when you don't have the means to perform something.

                There is also a second layer of external tools/functions that you can call, but you have to explicitly request their
                specification/documentation before being able to use them. If you believe you *might* need a second layer tool, call
                the GetSecondLayerToolDocumentation function together with the tool's name. Available second layer tools are:
                * Weather
                * ShoppingList

                The current date/time is {now} ({dayOfWeek}).
                """);
            var completion = await _client.CompleteChatAsync(_history.Prepend(prompt), options);
            AddToHistory(ChatMessage.CreateAssistantMessage(completion));

            if (completion.Value.FinishReason != ChatFinishReason.ToolCalls || i >= 5)
            {
                var content = completion.Value.Content.Select(x => x.Text + Environment.NewLine + x.Refusal);
                var combinedMessage = string.Join(Environment.NewLine, content);
                if (string.IsNullOrEmpty(combinedMessage))
                    combinedMessage = $"*No response received*. Executed {i} tool calls.";

                _logger.LogInformation("Received response from LLM: '{Message}'", combinedMessage);
                var functionCallResponsesForUser = functionCallResponses
                    .Select(x => x.UserResponse)
                    .ToList();

                return new LlmResponse(combinedMessage, functionCallResponsesForUser);
            }

            var toolResponses = await HandleToolCalls(completion.Value.ToolCalls);
            functionCallResponses.AddRange(toolResponses);
            foreach (var toolResponse in toolResponses)
            {
                if (toolResponse.Tools == null)
                    continue;

                foreach (var tool in toolResponse.Tools)
                    options.Tools.Add(tool);
            }

            i++;
        }
        while (true);
    }

    private async Task<List<ToolResponse>> HandleToolCalls(IEnumerable<ChatToolCall> calls)
    {
        var toolMessages = new List<ToolResponse>();
        foreach (ChatToolCall call in calls)
        {
            _logger.LogInformation("Received tool call from LLM: {FunctionName}", call.FunctionName);
            _logger.LogInformation("{FunctionArguments}", call.FunctionArguments.ToString());

            var node = JsonDocument.Parse(call.FunctionArguments);
            var toolResponse = await _toolService.Execute(call.FunctionName, node.RootElement);

            AddToHistory(ChatMessage.CreateToolMessage(call.Id, toolResponse.AssistantResponse));
            toolMessages.Add(toolResponse);
        }

        return toolMessages;
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
