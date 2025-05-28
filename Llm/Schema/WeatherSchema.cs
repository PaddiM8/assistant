using System.ComponentModel;

namespace Assistant.Llm.Schema;

[SecondLayerSchema]
[Description("Retrieves information about the weather for the given location")]
public class GetWeatherSchema : IToolSchema
{
    [Description("The name of the location")]
    public required string LocationName { get; set; }

    [Description("Start date")]
    public required DateTime StartDate { get; set; }

    [Description("End date")]
    public required DateTime EndDate { get; set; }
}
