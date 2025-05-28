using System.ComponentModel;
using System.Text.Json.Serialization;
using Assistant.Services.Planera;

namespace Assistant.Llm.Schema;

[SecondLayerSchema]
[Description("Adds an entry to the shopping list (in Swedish)")]
public class AddToShoppingListSchema : IToolSchema
{
    [Description("What the shopping list item should say (in Swedish)")]
    public required string Content { get; set; }

    [Description("The priority of the ticket. Defaults to 'Normal'.")]
    public PlaneraTicketPriority Priority { get; set; } = PlaneraTicketPriority.Normal;
}

[SecondLayerSchema]
[Description("Lists the items in the shopping list")]
public class RetrieveShoppingListSchema : IToolSchema
{
    // Schemas need at least one property
    [JsonPropertyName("Confirm")]
    public bool DummyProperty { get; set; }
}

[SecondLayerSchema]
[Description("Deletes item(s) from the shopping list. The assistant must inform the user of which items were removed")]
public class DeleteFromShoppingListSchema : IToolSchema
{
    [Description("The IDs of the item(s) to delete")]
    public required List<string> Ids { get; set; }
}
