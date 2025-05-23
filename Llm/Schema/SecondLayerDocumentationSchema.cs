using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Assistant.Llm.Schema;

public enum SecondLayerToolGroup
{
    Weather,
}

[FirstLayerSchema]
[Description("Fetches the documentation/specification for a second layer tool. A second layer tool works just like the other ones, but since they are not used as frequently the assistant needs to explicitly ask for more information about them before deciding whether they're good for the task")]
public class SecondLayerDocumentationSchema : IToolSchema
{
    [Description("The tool group to request documentation for")]
    public required SecondLayerToolGroup ToolGroupName { get; set; }
}
