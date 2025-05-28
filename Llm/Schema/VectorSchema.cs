using System.ComponentModel;
using Assistant.Database;

namespace Assistant.Llm.Schema;

[FirstLayerSchema]
[Description("Add an entry to the vector database with information that the USER might want to keep for later. This is from the perspective of the user")]
public class AddUserVectorMemorySchema : IToolSchema
{
    [Description("The information to save (in English)")]
    public required string Content { get; init; }
}

[FirstLayerSchema]
[Description("Add an entry to the vector database with information that the assistant itself might want to remember, eg. information about the user")]
public class AddAssistantVectorMemorySchema : IToolSchema
{
    [Description("The information to save (in English)")]
    public required string Content { get; init; }
}

[FirstLayerSchema]
[Description("Search in the vector database to find previously saved information/memories. The nearest neighbour is always returned, so you need to look in the content to determine if it's a match yourself. The vector database contains information about previously executed tools/functions as well as information saved by the assistant")]
public class SearchVectorMemorySchema : IToolSchema
{
    [Description("The context of the memory. UserMemory entries are added by the assistant for things the user might want to remember. AssistantMemory entries are for things the assistant might need to remember, eg. information about the user. AssistantAction entries are added automatically by the backend when the assistant invokes a tool and contain information about the action (eg. reminders)")]
    public EmbeddingContextKind? Context { get; init; }

    [Description("The content to search for (in English). Keep in mind that it's a vector search, so you can (and often should) write entire sentences")]
    public required string Content { get; init; }

    [Description("Whether to include stale memories. Eg. memories for reminders are marked as stale after they are triggered")]
    public bool IncludeStale { get; init; }

    [Description("Used to only include memories after a specific date/time")]
    public DateTimeOffset? AfterDateTime { get; init; }

    [Description("Used to only include memories before a specific date/time")]
    public DateTimeOffset? BeforeDateTime { get; init; }
}

[FirstLayerSchema]
[Description("remove a vector memory")]
public class RemoveVectorMemorySchema : IToolSchema
{
    [Description("The ID of the vector memory")]
    public required int Id { get; init; }
}

[FirstLayerSchema]
[Description("Update a vector memory")]
public class UpdateVectorMemorySchema : IToolSchema
{
    [Description("The ID of the vector memory")]
    public required int Id { get; init; }

    [Description("The new content (in English)")]
    public required string Content { get; init; }
}
