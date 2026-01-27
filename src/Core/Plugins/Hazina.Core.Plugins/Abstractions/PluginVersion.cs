namespace Hazina.Core.Plugins.Abstractions;

/// <summary>
/// Represents a version of a plugin (for update history and rollback)
/// </summary>
public class PluginVersion
{
    /// <summary>
    /// Unique version identifier
    /// </summary>
    public required string VersionId { get; init; }

    /// <summary>
    /// Plugin ID this version belongs to
    /// </summary>
    public required string PluginId { get; init; }

    /// <summary>
    /// Version number (incremental)
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// C# source code for this version
    /// </summary>
    public required string SourceCode { get; init; }

    /// <summary>
    /// Optional description of what changed
    /// </summary>
    public string? ChangeDescription { get; init; }

    /// <summary>
    /// Who created this version (user ID or 'AI')
    /// </summary>
    public string? CreatedBy { get; init; }

    /// <summary>
    /// When this version was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether this is the active version
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Metadata about this version
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
}
