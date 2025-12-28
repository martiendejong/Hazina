using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Tools.Validation;

/// <summary>
/// Manages JSON schemas for tools
/// </summary>
public class SchemaManager
{
    private readonly Dictionary<string, JsonElement> _schemas = new();
    private readonly ILogger? _logger;

    public SchemaManager(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register a JSON schema for a tool
    /// </summary>
    public void RegisterSchema(string toolName, JsonElement schema)
    {
        _schemas[toolName] = schema;
        _logger?.LogInformation("Registered schema for tool: {ToolName}", toolName);
    }

    /// <summary>
    /// Register a schema from JSON string
    /// </summary>
    public void RegisterSchema(string toolName, string schemaJson)
    {
        try
        {
            var schema = JsonSerializer.Deserialize<JsonElement>(schemaJson);
            RegisterSchema(toolName, schema);
        }
        catch (JsonException ex)
        {
            _logger?.LogError(ex, "Failed to parse schema JSON for tool: {ToolName}", toolName);
            throw;
        }
    }

    /// <summary>
    /// Get schema for a tool
    /// </summary>
    public JsonElement? GetSchema(string toolName)
    {
        return _schemas.TryGetValue(toolName, out var schema) ? schema : null;
    }

    /// <summary>
    /// Generate schema from tool parameters
    /// </summary>
    public JsonElement GenerateSchema(List<ChatToolParameter> parameters)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            var propertySchema = new Dictionary<string, object>
            {
                ["type"] = param.Type ?? "string"
            };

            if (!string.IsNullOrEmpty(param.Description))
            {
                propertySchema["description"] = param.Description;
            }

            properties[param.Name] = propertySchema;

            if (param.Required)
            {
                required.Add(param.Name);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Any())
        {
            schema["required"] = required;
        }

        var json = JsonSerializer.Serialize(schema);
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    /// <summary>
    /// Validate data against a schema (basic validation)
    /// </summary>
    public ValidationResult ValidateAgainstSchema(JsonElement schema, JsonElement data)
    {
        var result = new ValidationResult();

        try
        {
            // Get schema type
            if (!schema.TryGetProperty("type", out var typeElement))
            {
                result.AddWarning("Schema has no type property");
                return result;
            }

            var schemaType = typeElement.GetString();

            // Validate type
            if (!ValidateType(data, schemaType))
            {
                result.AddError($"Data type mismatch. Expected: {schemaType}, Got: {data.ValueKind}");
                return result;
            }

            // For objects, validate properties
            if (schemaType == "object" && data.ValueKind == JsonValueKind.Object)
            {
                ValidateObjectProperties(schema, data, result);
            }

            // For arrays, validate items
            if (schemaType == "array" && data.ValueKind == JsonValueKind.Array)
            {
                ValidateArrayItems(schema, data, result);
            }
        }
        catch (Exception ex)
        {
            result.AddError($"Schema validation error: {ex.Message}");
        }

        return result;
    }

    private bool ValidateType(JsonElement data, string? expectedType)
    {
        return expectedType?.ToLowerInvariant() switch
        {
            "string" => data.ValueKind == JsonValueKind.String,
            "number" or "integer" => data.ValueKind == JsonValueKind.Number,
            "boolean" => data.ValueKind == JsonValueKind.True || data.ValueKind == JsonValueKind.False,
            "array" => data.ValueKind == JsonValueKind.Array,
            "object" => data.ValueKind == JsonValueKind.Object,
            "null" => data.ValueKind == JsonValueKind.Null,
            _ => true
        };
    }

    private void ValidateObjectProperties(JsonElement schema, JsonElement data, ValidationResult result)
    {
        // Check required properties
        if (schema.TryGetProperty("required", out var requiredElement) &&
            requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var reqProp in requiredElement.EnumerateArray())
            {
                var propName = reqProp.GetString();
                if (propName != null && !data.TryGetProperty(propName, out _))
                {
                    result.AddError($"Required property '{propName}' is missing");
                }
            }
        }

        // Validate each property
        if (schema.TryGetProperty("properties", out var propertiesElement) &&
            propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in data.EnumerateObject())
            {
                if (propertiesElement.TryGetProperty(prop.Name, out var propSchema))
                {
                    var propResult = ValidateAgainstSchema(propSchema, prop.Value);
                    foreach (var error in propResult.Errors)
                    {
                        result.AddError($"Property '{prop.Name}': {error}");
                    }
                }
                else
                {
                    result.AddWarning($"Unknown property: {prop.Name}");
                }
            }
        }
    }

    private void ValidateArrayItems(JsonElement schema, JsonElement data, ValidationResult result)
    {
        if (schema.TryGetProperty("items", out var itemsSchema))
        {
            var index = 0;
            foreach (var item in data.EnumerateArray())
            {
                var itemResult = ValidateAgainstSchema(itemsSchema, item);
                foreach (var error in itemResult.Errors)
                {
                    result.AddError($"Array item [{index}]: {error}");
                }
                index++;
            }
        }
    }

    /// <summary>
    /// Get all registered tool schemas
    /// </summary>
    public Dictionary<string, JsonElement> GetAllSchemas()
    {
        return new Dictionary<string, JsonElement>(_schemas);
    }

    /// <summary>
    /// Clear all schemas
    /// </summary>
    public void Clear()
    {
        _schemas.Clear();
        _logger?.LogInformation("Cleared all schemas");
    }
}
