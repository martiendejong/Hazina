namespace Hazina.Core.Plugins.Abstractions;

/// <summary>
/// Metadata about a plugin
/// </summary>
public class PluginMetadata
{
    /// <summary>
    /// Unique identifier for the plugin
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Name of the plugin
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Version of the plugin
    /// </summary>
    public string Version { get; init; } = "1.0.0";

    /// <summary>
    /// Description of what the plugin does
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// C# source code of the plugin
    /// </summary>
    public required string SourceCode { get; init; }

    /// <summary>
    /// Who created this plugin (user ID or 'AI')
    /// </summary>
    public string? CreatedBy { get; init; }

    /// <summary>
    /// When the plugin was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the plugin is enabled
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Tags for categorization
    /// </summary>
    public List<string> Tags { get; init; } = new();

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> AdditionalMetadata { get; init; } = new();
}
