using System.ComponentModel;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Assistant.Llm.Schema;

[SecondLayerSchema]
[Description("Returns a list of all the smart home entities and their states (brightness, temperature, etc.). The assistant should only use the entity IDs for function calls, not when talking to the user")]
public class ListSmartHomeEntityIdsSchema : IToolSchema
{
    // OpenAI requires at least one property
    [JsonPropertyName("Confirm")]
    public bool DummyProperty { get; set; }
}

[SecondLayerSchema]
[Description("Used to do one or several of the operations on a smart light: turn on/off, change brightness, change temperature")]
public class ControlSmartLightSchema : IToolSchema
{
    [Description("The entity_id of the light OR group of lights. Prefer using a relevant group ID if possible, when updating several")]
    public required string EntityId { get; set; }

    [Description("Describes if the light should be on or off. NOTE: To explicitly turn light(s) off, use this instead of the reset function")]
    public required bool IsOn { get; set; }

    [Description("A value between 0 and 100 of how much the brightness should change")]
    public int? BrightnessPointChange { get; set; }

    [Description("A value between 0 and 100 of how much the coldness should change")]
    public int? ColdnessChangePointChange { get; set; }
}

[SecondLayerSchema]
[Description("Resets a light to its default brightness and temperature. IMPORTANT: Always use this to go back to how it was before. The user desires very specific values day-to-day")]
public class ResetLightSchema : IToolSchema
{
    [Description("The entity_id of the light OR group of lights. Prefer using a relevant group ID if possible, when updating several")]
    public required string EntityId { get; set; }
}
