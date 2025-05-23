using System.ComponentModel;

namespace Assistant.Llm.Schema;

[SecondLayerSchema]
[Description("Retrieves information about the weather for the given location")]
public class GetWeatherSchema : IToolSchema
{
    [Description("The longitude of the location")]
    public required float Longitude { get; set; }

    [Description("The latitude of the location")]
    public required float Latitude { get; set; }

    [Description("Start date")]
    public required DateTime StartDate { get; set; }

    [Description("End date")]
    public required DateTime EndDate { get; set; }
}
