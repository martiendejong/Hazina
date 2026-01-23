using System.Dynamic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.API.Generic.Dynamic;

/// <summary>
/// A dynamic entity that can hold any fields defined in YAML.
/// No C# class needed - fields are determined at runtime.
/// </summary>
public class DynamicEntity : DynamicObject
{
    private readonly Dictionary<string, object?> _properties = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Entity ID (always present)
    /// </summary>
    public Guid Id
    {
        get => _properties.TryGetValue("id", out var val) && val is Guid g ? g : Guid.Empty;
        set => _properties["id"] = value;
    }

    /// <summary>
    /// When created (always present)
    /// </summary>
    public DateTime CreatedAt
    {
        get => _properties.TryGetValue("createdAt", out var val) && val is DateTime dt ? dt : DateTime.MinValue;
        set => _properties["createdAt"] = value;
    }

    /// <summary>
    /// When last updated
    /// </summary>
    public DateTime? UpdatedAt
    {
        get => _properties.TryGetValue("updatedAt", out var val) ? val as DateTime? : null;
        set => _properties["updatedAt"] = value;
    }

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted
    {
        get => _properties.TryGetValue("isDeleted", out var val) && val is bool b && b;
        set => _properties["isDeleted"] = value;
    }

    /// <summary>
    /// The entity type name (e.g., "Document", "Note")
    /// </summary>
    [JsonIgnore]
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// Get a property value by name
    /// </summary>
    public object? this[string propertyName]
    {
        get => _properties.TryGetValue(propertyName, out var val) ? val : null;
        set => _properties[propertyName] = value;
    }

    /// <summary>
    /// Get all properties as a dictionary
    /// </summary>
    public IReadOnlyDictionary<string, object?> Properties => _properties;

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        return _properties.TryGetValue(binder.Name, out result);
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        _properties[binder.Name] = value;
        return true;
    }

    public override IEnumerable<string> GetDynamicMemberNames()
    {
        return _properties.Keys;
    }

    /// <summary>
    /// Create from JSON
    /// </summary>
    public static DynamicEntity FromJson(string json, string entityType)
    {
        var entity = new DynamicEntity { EntityType = entityType };
        var doc = JsonDocument.Parse(json);

        foreach (var prop in doc.RootElement.EnumerateObject())
        {
            entity._properties[prop.Name] = ConvertJsonElement(prop.Value);
        }

        // Ensure ID exists
        if (!entity._properties.ContainsKey("id") || entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        return entity;
    }

    /// <summary>
    /// Create from dictionary
    /// </summary>
    public static DynamicEntity FromDictionary(IDictionary<string, object?> data, string entityType)
    {
        var entity = new DynamicEntity { EntityType = entityType };

        foreach (var kvp in data)
        {
            entity._properties[kvp.Key] = kvp.Value;
        }

        if (entity.Id == Guid.Empty)
        {
            entity.Id = Guid.NewGuid();
        }

        return entity;
    }

    /// <summary>
    /// Convert to JSON
    /// </summary>
    public string ToJson(JsonSerializerOptions? options = null)
    {
        options ??= new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        return JsonSerializer.Serialize(_properties, options);
    }

    /// <summary>
    /// Convert to dictionary for storage
    /// </summary>
    public Dictionary<string, object?> ToDictionary()
    {
        return new Dictionary<string, object?>(_properties, StringComparer.OrdinalIgnoreCase);
    }

    private static object? ConvertJsonElement(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String when Guid.TryParse(element.GetString(), out var guid) => guid,
            JsonValueKind.String when DateTime.TryParse(element.GetString(), out var dt) => dt,
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt32(out var i) => i,
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonElement).ToList(),
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            _ => element.GetRawText()
        };
    }
}
