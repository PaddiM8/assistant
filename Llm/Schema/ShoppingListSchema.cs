using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Assistant.Llm.Schema;

[SecondLayerSchema]
[Description("Adds an entry to the shopping list (in Swedish)")]
public class AddToShoppingListSchema : IToolSchema
{
    [Description("What the shopping list item should say (in Swedish)")]
    public required string Content { get; set; }
}

[SecondLayerSchema]
[Description("Lists the item in the shopping list")]
public class RetrieveShoppingListSchema : IToolSchema
{
    // Schemas need at least one property
    [JsonPropertyName("Confirm")]
    public bool DummyProperty { get; set; }
}
