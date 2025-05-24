using System.ComponentModel;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Assistant.Llm.Schema;

[SecondLayerSchema]
[Description("Returns a list of all the smart home entities, mapping IDs to descriptions")]
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
    [Description("The entity_id of the light")]
    public required string EntityId { get; set; }

    [Description("Describes if the light should be on or off")]
    public required bool IsOn { get; set; }

    [Description("A value between 0 and 100 (percentage)")]
    public int? Brightness { get; set; }

    [Description("A value between 0 and 100 (percentage)")]
    public int? Temperature { get; set; }
}
