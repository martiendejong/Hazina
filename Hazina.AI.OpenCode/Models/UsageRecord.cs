namespace Hazina.AI.OpenCode.Models;

/// <summary>
/// Represents a single usage record for OpenCode API calls
/// </summary>
public class UsageRecord
{
    /// <summary>
    /// Unique identifier for this usage record
    /// </summary>
    public required string Id { get; set; }

    /// <summary>
    /// UTC timestamp when the call was made
    /// </summary>
    public required DateTime Timestamp { get; set; }

    /// <summary>
    /// Agent that executed the task
    /// </summary>
    public required string AgentName { get; set; }

    /// <summary>
    /// Task description or query
    /// </summary>
    public required string Task { get; set; }

    /// <summary>
    /// Type of operation (Execute, Stream, SmartRoute, etc.)
    /// </summary>
    public required string OperationType { get; set; }

    /// <summary>
    /// Whether the operation succeeded
    /// </summary>
    public required bool Success { get; set; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Duration of the operation in milliseconds
    /// </summary>
    public long DurationMs { get; set; }

    /// <summary>
    /// Project hint if provided (for smart routing)
    /// </summary>
    public string? ProjectHint { get; set; }
}
