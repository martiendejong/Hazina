namespace Hazina.LLMs.Classes.ToolSets;

/// <summary>
/// Represents a named collection of tools that can be enabled/disabled as a group
/// </summary>
public class ToolSet
{
    /// <summary>
    /// Unique identifier for the tool set
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the tool set
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what this tool set provides
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Category/group for organizing tool sets
    /// </summary>
    public string Category { get; set; } = "General";

    /// <summary>
    /// Version of the tool set
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// Tags for searching and filtering
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Tool names included in this set
    /// </summary>
    public List<string> ToolNames { get; set; } = new();

    /// <summary>
    /// Whether this tool set is enabled by default
    /// </summary>
    public bool EnabledByDefault { get; set; } = false;

    /// <summary>
    /// Whether this tool set requires explicit opt-in
    /// </summary>
    public bool RequiresOptIn { get; set; } = true;

    /// <summary>
    /// Dependencies on other tool sets
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Minimum permission level required to use this tool set
    /// </summary>
    public string? RequiredPermission { get; set; }

    /// <summary>
    /// Metadata for the tool set
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// JSON schema defining the tool set structure
    /// </summary>
    public ToolSetSchema? Schema { get; set; }
}

/// <summary>
/// JSON schema definition for a tool set
/// </summary>
public class ToolSetSchema
{
    public string SchemaVersion { get; set; } = "1.0";
    public Dictionary<string, ToolSchemaDefinition> Tools { get; set; } = new();
}

/// <summary>
/// Schema definition for an individual tool
/// </summary>
public class ToolSchemaDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<ToolParameterSchema> Parameters { get; set; } = new();
    public ToolReturnSchema? Returns { get; set; }
    public List<string> Examples { get; set; } = new();
    public Dictionary<string, string> ErrorCodes { get; set; } = new();
}

/// <summary>
/// Schema for tool parameters
/// </summary>
public class ToolParameterSchema
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "string";
    public string Description { get; set; } = string.Empty;
    public bool Required { get; set; }
    public string? DefaultValue { get; set; }
    public List<string>? EnumValues { get; set; }
    public Dictionary<string, object>? Constraints { get; set; }
}

/// <summary>
/// Schema for tool return value
/// </summary>
public class ToolReturnSchema
{
    public string Type { get; set; } = "string";
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, object>? Schema { get; set; }
}
