using OpenAI.Chat;

namespace Assistant.Llm;

public class ToolResponse(string assistantResponse)
{
    public string AssistantResponse { get; } = assistantResponse;

    public List<ChatTool>? Tools { get; init; }
}
