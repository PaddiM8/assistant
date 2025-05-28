using System.ComponentModel;

namespace Assistant.Llm.Schema;

[FirstLayerSchema]
[Description("Schedules a self-prompt to the assistant (in English). Use-cases for this may be to ask the user something at a later time, to check the status of something at a later time, to perform an action at a later time, etc.")]
public class ScheduleSelfPromptSchema : IToolSchema
{
    [Description("When the self-prompt should (first) be sent")]
    public required DateTimeOffset TriggerDateTime { get; init; }

    [Description("Instructions that the assistant writes to its future self (in English)")]
    public required string Prompt { get; init; }

    [Description("How often to repeat the prompt")]
    public Recurrence? Recurrence { get; set; }
}

[FirstLayerSchema]
[Description("Updates a scheduled self-prompt (eg. recurring tasks set up by the assistant)")]
public class UpdateSelfPromptSchema : IToolSchema
{
    [Description("The ID of the entry")]
    public required int Id { get; set; }

    [Description("When the self-prompt should (first) be sent")]
    public required DateTimeOffset TriggerDateTime { get; init; }

    [Description("Instructions that the assistant writes to its future self (in English)")]
    public string? Prompt { get; init; }

    [Description("How often to repeat the prompt")]
    public Recurrence? Recurrence { get; set; }
}

[FirstLayerSchema]
[Description("Deleted a scheduled self-prompt (eg. recurring tasks set up by the assistant)")]
public class DeleteSelfPromptSchema : IToolSchema
{
    [Description("The ID of the entry")]
    public required int Id { get; set; }
}
