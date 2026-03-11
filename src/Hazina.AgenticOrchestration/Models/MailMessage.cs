namespace Hazina.AgenticOrchestration.Models;

/// <summary>
/// Inter-agent mail message (Overstory-inspired SQLite mail system)
/// </summary>
public class MailMessage
{
    public required string Id { get; set; } = Guid.NewGuid().ToString();
    public required string From { get; set; }
    public required string To { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }

    /// <summary>
    /// Message type - semantic (status, question, result, error)
    /// or protocol (worker_done, merge_ready, escalation, etc)
    /// </summary>
    public MailMessageType Type { get; set; } = MailMessageType.Status;

    /// <summary>
    /// Priority level - urgent/high trigger auto-nudge
    /// </summary>
    public MailPriority Priority { get; set; } = MailPriority.Normal;

    /// <summary>
    /// Optional JSON payload for structured data
    /// </summary>
    public string? Payload { get; set; }

    /// <summary>
    /// Whether message has been read
    /// </summary>
    public bool Read { get; set; }

    /// <summary>
    /// When message was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Thread ID for conversation tracking (optional)
    /// </summary>
    public string? ThreadId { get; set; }
}

public enum MailMessageType
{
    // Semantic types
    Status,
    Question,
    Result,
    Error,

    // Protocol types (auto-nudge)
    WorkerDone,
    MergeReady,
    Merged,
    MergeFailed,
    Escalation,
    HealthCheck,
    Dispatch,
    Assign
}

public enum MailPriority
{
    Low,
    Normal,
    High,
    Urgent
}

/// <summary>
/// Pending nudge marker (written instead of sending tmux keys during tool execution)
/// </summary>
public class PendingNudge
{
    public required string From { get; set; }
    public required string Reason { get; set; }
    public required string Subject { get; set; }
    public required string MessageId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
