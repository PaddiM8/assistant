using System.ComponentModel;
using Assistant.Messaging;

namespace Assistant.Llm.Schema;

public enum Frequency
{
    Daily,
    Weekly,
    Monthly,
    Yearly,
}

[FirstLayerSchema]
[Description("Recurrence. MUST be a JSON object. Example: { \"Frequency\" = \"Daily\", \"Interval\" = 3 } means 'every three days'.")]
public class Recurrence
{
    [Description("How often it should repeat")]
    public required Frequency Frequency { get; init; }

    [Description("The interval at which it should repeat")]
    public int Interval { get; set; } = 1;
}

[FirstLayerSchema]
[Description("Create a reminder. Keep it mind that reminders often should be triggered before events with some margin. You may decide the time and message yourself if relevant and doable.")]
public class CreateReminderSchema : IToolSchema
{
    [Description("When the reminder should (first) be triggered")]
    public required DateTimeOffset TriggerDateTime { get; init; }

    [Description("What the reminder should say")]
    public required string Message { get; init; }

    [Description("At which urgency the user should be contacted. 'Ping' is used when the user must see it the same day, otherwise 'Normal'")]
    public MessagePriority? Priority { get; set; }

    [Description("How often to repeat the reminder")]
    public Recurrence? Recurrence { get; set; }
}

[FirstLayerSchema]
[Description("Remove a reminder")]
public class RemoveReminderSchema : IToolSchema
{
    [Description("The ID of the reminder to remove")]
    public required int Id { get; init; }
}

[FirstLayerSchema]
[Description("Update a reminder")]
public class UpdateReminderSchema : IToolSchema
{
    [Description("The ID of the reminder to remove")]
    public required int Id { get; init; }

    [Description("When the reminder should (first) be triggered")]
    public required DateTimeOffset TriggerDateTime { get; init; }

    [Description("What the reminder should say instead")]
    public string? Message { get; init; }

    [Description("At which urgency the user should be contacted. 'Ping' is used when the user must see it the same day, otherwise 'Normal'")]
    public MessagePriority? Priority { get; set; }

    [Description("How often to repeat the reminder")]
    public Recurrence? Recurrence { get; set; }
}
