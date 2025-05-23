using System.Reflection;
using System.Text.Json;
using System.Text.Json.Schema;
using Assistant.Llm.Schema;
using OpenAI.Chat;

namespace Assistant.Llm;

public static class OpenAiUtils
{
    private static readonly Dictionary<Type, ChatTool> _tools = [];

    static OpenAiUtils()
    {
        var schemaTypes = Assembly
            .GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.IsClass)
            .Where(x => x.GetCustomAttribute<FirstLayerSchemaAttribute>() != null);
        foreach (var schemaType in schemaTypes)
            _tools[schemaType] = CreateFunctionTool(schemaType);
    }

    public static ChatTool GetTool<T>()
    {
        return _tools[typeof(T)];
    }

    public static IEnumerable<ChatTool> GetTools()
    {
        return _tools.Values;
    }

    public static ChatTool CreateFunctionTool(Type schemaType)
    {
        var schema = SchemaUtils.GetAsNode(schemaType).AsObject();
        var description = schema["description"]!.ToString();
        schema.Remove("description");

        var binarySchema = BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(schema));

        return ChatTool.CreateFunctionTool(SchemaUtils.GetToolName(schemaType), description, binarySchema);
    }
}
