namespace Hazina.Indexing.Models;

/// <summary>
/// Phase A indexation result: File/directory structural metadata.
/// Fast scan capturing paths, sizes, timestamps without reading file content.
/// </summary>
public record StructuralIndex
{
    /// <summary>
    /// Root directory path that was indexed.
    /// </summary>
    public required string RootPath { get; init; }

    /// <summary>
    /// All files discovered during indexation.
    /// </summary>
    public List<FileMetadata> Files { get; init; } = new();

    /// <summary>
    /// Total number of directories indexed.
    /// </summary>
    public int DirectoryCount { get; init; }

    /// <summary>
    /// Total number of files indexed.
    /// </summary>
    public int FileCount { get; init; }

    /// <summary>
    /// Total size of all indexed files (bytes).
    /// </summary>
    public long TotalSizeBytes { get; init; }

    /// <summary>
    /// When indexation was performed (UTC).
    /// </summary>
    public DateTime IndexedAt { get; init; }

    /// <summary>
    /// How long indexation took.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// File extension statistics.
    /// </summary>
    public Dictionary<string, int> ExtensionCounts { get; init; } = new();
}

/// <summary>
/// Metadata for a single file.
/// </summary>
public record FileMetadata
{
    /// <summary>
    /// Full path to file.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// File name only (no directory).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// File extension (including dot, e.g., ".cs", ".json").
    /// </summary>
    public required string Extension { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    /// Last modification time (UTC).
    /// </summary>
    public DateTime Modified { get; init; }

    /// <summary>
    /// MIME type (e.g., "text/plain", "application/json").
    /// </summary>
    public string? MimeType { get; init; }

    /// <summary>
    /// Relative path from root (for display purposes).
    /// </summary>
    public string? RelativePath { get; init; }
}

/// <summary>
/// Index of running system processes.
/// </summary>
public record ProcessIndex
{
    /// <summary>
    /// All discovered processes.
    /// </summary>
    public List<ProcessMetadata> Processes { get; init; } = new();

    /// <summary>
    /// Total number of processes.
    /// </summary>
    public int TotalProcesses { get; init; }

    /// <summary>
    /// When process snapshot was taken (UTC).
    /// </summary>
    public DateTime IndexedAt { get; init; }
}

/// <summary>
/// Metadata for a running process.
/// </summary>
public record ProcessMetadata
{
    /// <summary>
    /// Process ID.
    /// </summary>
    public int Pid { get; init; }

    /// <summary>
    /// Process name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Process start time.
    /// </summary>
    public DateTime StartTime { get; init; }

    /// <summary>
    /// CPU usage percentage (approximate).
    /// </summary>
    public double CpuPercent { get; init; }

    /// <summary>
    /// Memory usage in megabytes.
    /// </summary>
    public long MemoryMB { get; init; }

    /// <summary>
    /// Command line arguments (if accessible).
    /// </summary>
    public string? CommandLine { get; init; }
}

/// <summary>
/// Indexation options controlling scope and exclusions.
/// </summary>
public record IndexOptions
{
    /// <summary>
    /// Glob patterns for files/directories to exclude.
    /// </summary>
    public List<string> ExcludePatterns { get; init; } = new();

    /// <summary>
    /// Maximum file size to index (bytes).
    /// Files larger than this are skipped.
    /// </summary>
    public int MaxFileSizeBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>
    /// Whether to follow symbolic links.
    /// </summary>
    public bool FollowSymlinks { get; init; } = false;

    /// <summary>
    /// Maximum directory depth to traverse.
    /// 0 = unlimited.
    /// </summary>
    public int MaxDepth { get; init; } = 0;
}
