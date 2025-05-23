using System.ComponentModel;

namespace Assistant.Llm.Schema;

[FirstLayerSchema]
[Description("Schedules a self-prompt to the assistant. Use-cases for this may be to ask the user something at a later time, to check the status of something at a later time, to perform an action at a later time, etc.")]
public class ScheduledSelfPromptSchema : IToolSchema
{
    [Description("When the self-prompt should (first) be sent")]
    public required DateTimeOffset TriggerDateTime { get; init; }

    [Description("Instructions that the assistant writes to its future self.")]
    public required string Prompt { get; init; }

    [Description("How often to repeat the prompt")]
    public Recurrence? Recurrence { get; set; }
}
