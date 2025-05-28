namespace Assistant.Services.Models;

public class LightState
{
    public required string EntityId { get; set; }

    public bool IsOn { get; set; }

    public string? Description { get; set; }

    public int? BrightnessPercentage { get; set; }

    public int? ColdnessPercentage { get; set; }
}
