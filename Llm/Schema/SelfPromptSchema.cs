using System.ComponentModel;

namespace Assistant.Llm.Schema;

[FirstLayerSchema]
[Description("Schedules a self-prompt to the assistant (in English). Use-cases for this may be to ask the user something at a later time, to check the status of something at a later time, to perform an action at a later time, etc. NOT reminders")]
public class ScheduleSelfPromptSchema : IToolSchema
{
    [Description("When the self-prompt should (first) be sent")]
    public required DateTimeOffset TriggerDateTime { get; init; }

    [Description("Whether the trigger time has been converted from relative time (eg. 'in 3 days')")]
    public required bool WasConvertedFromRelativeTime { get; init; }

    [Description("Instructions that the assistant writes to its future self (in English)")]
    public required string Prompt { get; init; }

    [Description("How often to repeat the prompt")]
    public Recurrence? Recurrence { get; set; }
}

[FirstLayerSchema]
[Description("Updates a scheduled self-prompt (eg. recurring tasks set up by the assistant), NOT reminders")]
public class UpdateSelfPromptSchema : IToolSchema
{
    [Description("The ID of the entry")]
    public required int Id { get; set; }

    [Description("When the self-prompt should (first) be sent")]
    public required DateTimeOffset TriggerDateTime { get; init; }

    [Description("Whether the trigger time has been converted from relative time (eg. 'in 3 days')")]
    public required bool WasConvertedFromRelativeTime { get; init; }

    [Description("Instructions that the assistant writes to its future self (in English)")]
    public string? Prompt { get; init; }

    [Description("How often to repeat the prompt")]
    public Recurrence? Recurrence { get; set; }
}

[FirstLayerSchema]
[Description("Deletes a scheduled self-prompt (eg. recurring tasks set up by the assistant), NOT reminders")]
public class DeleteSelfPromptSchema : IToolSchema
{
    [Description("The ID of the entry")]
    public required int Id { get; set; }
}
