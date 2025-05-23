namespace Assistant.Llm;

public record LlmResponse(string Message, List<string> FunctionCallResponses);
