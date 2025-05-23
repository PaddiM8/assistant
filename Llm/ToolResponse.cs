using OpenAI.Chat;

namespace Assistant.Llm;

public class ToolResponse
{
    public string AssistantResponse { get; }

    public string UserResponse { get; }

    public List<ChatTool>? Tools { get; init; }

    public ToolResponse(string assistantResponse, string userResponse)
    {
        AssistantResponse = assistantResponse;
        UserResponse = userResponse;
    }

    public ToolResponse(string response)
    {
        AssistantResponse = response;
        UserResponse = response;
    }
}
