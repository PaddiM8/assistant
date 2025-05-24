using System.ComponentModel;
using Assistant.Messaging;

namespace Assistant.Llm.Schema;

[FirstLayerSchema]
[Description("Sends a message to the user. Should ONLY be used in combination with a scheduled self-prompt, for situations where the assistant wants to send a message at a later time")]
public class MessageUserSchema : IToolSchema
{
    [Description("The message content to send to the user")]
    public required string Message { get; init; }

    [Description("At which urgency the user should be contacted. 'Ping' is used when the user must see it the same day, otherwise 'Normal'")]
    public MessagePriority Priority { get; set; }

}
