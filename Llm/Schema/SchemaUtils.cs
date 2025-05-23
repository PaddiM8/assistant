using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;

namespace Assistant.Llm.Schema;

public static class SchemaUtils
{
    private static readonly JsonSchemaExporterOptions _exporterOptions = new()
    {
        TransformSchemaNode = TransformSchemaNode,
        TreatNullObliviousAsNonNullable = true,
    };

    public static JsonNode GetAsNode<T>()
    {
        return GetAsNode(typeof(T));
    }

    public static JsonNode GetAsNode(Type schemaType)
    {
        JsonSerializerOptions options = JsonSerializerOptions.Default;
        var schema = options.GetJsonSchemaAsNode(schemaType, _exporterOptions);

        return schema;
    }

    public static string GetToolName(Type type)
    {
        var typeName = type.Name;

        return typeName.EndsWith("Schema")
            ? typeName[..^6]
            : typeName;
    }

    private static JsonNode TransformSchemaNode(JsonSchemaExporterContext context, JsonNode schema)
    {
        ICustomAttributeProvider? attributeProvider = context.PropertyInfo is not null
            ? context.PropertyInfo.AttributeProvider
            : context.TypeInfo.Type;

        DescriptionAttribute? descriptionAttribute = attributeProvider?
            .GetCustomAttributes(inherit: true)
            .Select(x => x as DescriptionAttribute)
            .FirstOrDefault(x => x is not null);

        if (descriptionAttribute == null || schema is not JsonObject jsonObject)
            return schema;

        jsonObject.Insert(0, "description", descriptionAttribute.Description);

        return schema;
    }
}
