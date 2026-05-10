/// <summary>
/// Configuration for a Hazina store with safety defaults.
/// By default, stores are read-only and require explicit write enablement.
/// </summary>
public class StoreConfig
{
    /// <summary>
    /// Unique name of the store.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the store's purpose.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// File system path to the store location.
    /// Can be absolute or relative to the working directory.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// File filter patterns (e.g., "*.cs", "*.txt").
    /// If empty, all files are included.
    /// </summary>
    public string[] FileFilters { get; set; } = [];

    /// <summary>
    /// Optional subdirectory within the path to restrict access.
    /// </summary>
    public string SubDirectory { get; set; } = "";

    /// <summary>
    /// Patterns to exclude from the store (e.g., "node_modules", ".git").
    /// </summary>
    public string[] ExcludePattern { get; set; } = [];

    /// <summary>
    /// Whether the store is read-only (safe default).
    /// Set to false and enable ExplicitWriteEnabled for write access.
    /// </summary>
    public bool IsReadOnly { get; set; } = true;

    /// <summary>
    /// Explicit confirmation that write access is intended.
    /// Required to be true if IsReadOnly is false.
    /// This double-check prevents accidental write access.
    /// </summary>
    public bool ExplicitWriteEnabled { get; set; } = false;

    /// <summary>
    /// Maximum file size in bytes that can be read/written.
    /// Prevents memory exhaustion from large files.
    /// Default: 10MB
    /// </summary>
    public long? MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Allowed file extensions for safety (e.g., [".txt", ".md", ".json"]).
    /// If null or empty, all extensions are allowed.
    /// </summary>
    public string[]? AllowedExtensions { get; set; }

    /// <summary>
    /// Whether to follow symbolic links when traversing directories.
    /// Default: false (for security).
    /// </summary>
    public bool FollowSymlinks { get; set; } = false;

    /// <summary>
    /// Maximum depth for directory traversal.
    /// Prevents infinite loops from circular symlinks.
    /// Default: 10
    /// </summary>
    public int MaxDirectoryDepth { get; set; } = 10;
}