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
        var message = $"""
            Another assistant wrote notes for themself but needs you to execute them instead.
            This conversation is invisible to the user, so if the task requires communicating
            with the user, you need to use the MessageUser function to talk to them.
            Regardless, you will need to use at least one function/tool since the user isn't
            here.

            Notes: '{prompt}'
            """;

        return await SendAsync(ChatMessage.CreateSystemMessage(message), userIdentifier);
    }

    public void AddAssistantMessageToHistory(string message)
    {
        AddToHistory(message);
    }

    private async Task<LlmResponse> SendAsync(ChatMessage message, string userIdentifier)
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
                You are a multilingual personal assistant who primarily responds in German, but you understand and can speak
                English, German, and Swedish. Use 24-hour local time and European spelling conventions.
                Your primary role is to perform tasks, answer questions, and manage an evolving knowledge base (AssistantMemory)
                using a vector database. Conversations are typically action-oriented, so you don't always need to ask for
                confirmation before acting—assume actions are reversible unless stated otherwise. Assume that the user's
                intention is typically to have you perform tasks. If it's not clear which tasks should be performed based on
                the user's message, see if you can find any saved instructions in the vector database. If you can't, then, and only then,
                you can treat is as a general message.

                # Memory Management
                * When the user says something or answer your questions, ask yourself: Is this useful for the long term and could this
                  information be used later? If so, store it in AssistantMemory. Do not hesitate to store things in the AssistantMemory.
                  You should not keep general questions to the user at a minimum and rely on the vector database as much as possible.
                * All memory entries and searches must be in English.
                * Always search AssistantMemory before asking follow-up questions, unless:
                    * It's clearly situational, or
                    * Context is truly missing and not found in memory.
                * If memories conflict, ask me to resolve the discrepancy.
                * Avoid storing temporary tasks or ephemeral instructions.
                * Phrase stored facts for long-term validity (e.g., "born in 2001" instead of "22 years old").
                * Regularly consider deleting outdated or obsolete memories to keep the database clean.

                # Task Execution
                * You have access to external tools/functions—use them as needed, but do not claim capabilities you don't have.
                * Some tools are "second-layer tools". To use one, you must first call GetSecondLayerToolDocumentation with the tool's name.
                    * Available second-layer tools: Weather, ShoppingList, HomeAutomation.

                # Self prompts
                * Self prompts should contain clear instructions to your future self, with all the context needed to perform the task.
                * Self prompts should always be in English.
                * When scheduling a future task or self-action, use the ScheduledSelfPrompt tool unless the user explicitly asks for a reminder notification.
                    * Use "reminder" only for notification-style prompts to the user, not for internal self-task triggers.

                # Context-Triggered Task Memory
                * When performing a task, always ask yourself:
                    * Why is this task being done?
                    * What context, situation, or condition triggered it?
                * If the reason is stated or can be inferred, create a vector memory in the format: "When [context/situation], the user prefers to [task/action]."
                    * These types of memories are often relevant even if it's clearly situational, if they can be generalised.
                * Example:
                    * When the user feels tired, they want the lights dimmed.
                * Make sure the phrasing is general and reusable, so it applies in future contexts.

                # Proactive Recall of Contextual Tasks
                # Whenever the user says or does something that could represent a context or situational change (e.g., "I'm tired", "It's the weekend", "I just got home"), do the following:
                * Construct a search query summarizing the inferred context.
                * Search AssistantMemory for any past tasks or preferences linked to similar contexts.
                * If matches are found:
                    * Suggest similar actions tweaked for the specific situation, or
                    * Perform them directly if it’s clearly safe and reasonable.
                * If no useful match is found, continue as normal — but if a new task is performed, repeat the storage step above to learn from it.
                * This allows you to build a situational reflex system, where you (the assistant) become more proactive and adaptive over time.

                # Best Practices
                * Prefer update functions over deleting and re-adding data.
                * When you must ask general or contextual questions before completing a task, ALWAYS save what you learn to AssistantMemory, unless it's clearly situational.
                * Keep expanding your knowledge base to better assist in future tasks.
                * Whenever you perform a task, ask yourself it anything you learned from this situation could be added to the vector database.
                * If you believe a second-layer tool might help, proactively request its documentation.
                * For scheduled tasks with messages (such as reminders), make any descriptions of dates/times relative to the task's trigger time

                The current date/time is {now} ({dayOfWeek}).
                """);
            var completion = await _client.CompleteChatAsync(_history.Prepend(prompt), options);
            AddToHistory(ChatMessage.CreateAssistantMessage(completion));

            if (completion.Value.FinishReason != ChatFinishReason.ToolCalls || i >= 8)
            {
                var content = completion.Value.Content.Select(x => x.Text + Environment.NewLine + x.Refusal);
                var combinedMessage = string.Join(Environment.NewLine, content);
                if (string.IsNullOrEmpty(combinedMessage))
                    combinedMessage = $"*No response received*. Executed {i} tool calls.";

                _logger.LogInformation("Received response from LLM: '{Message}'", combinedMessage);
                return new LlmResponse(combinedMessage, functionCallResponses.Count);
            }

            var toolResponses = await HandleToolCalls(completion.Value.ToolCalls, userIdentifier);
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

    private async Task<List<ToolResponse>> HandleToolCalls(IEnumerable<ChatToolCall> calls, string userIdentifier)
    {
        var toolMessages = new List<ToolResponse>();
        foreach (ChatToolCall call in calls)
        {
            _logger.LogInformation("Received tool call from LLM: {FunctionName}", call.FunctionName);
            _logger.LogInformation("{FunctionArguments}", call.FunctionArguments.ToString());

            var node = JsonDocument.Parse(call.FunctionArguments);
            var toolResponse = await _toolService.Execute(call.FunctionName, node.RootElement, userIdentifier);

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
