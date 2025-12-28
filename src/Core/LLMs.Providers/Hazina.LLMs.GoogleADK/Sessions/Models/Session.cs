namespace Hazina.LLMs.GoogleADK.Sessions.Models;

/// <summary>
/// Represents an agent session with state and metadata
/// </summary>
public class Session
{
    /// <summary>
    /// Unique session identifier
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the agent this session belongs to
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Current session status
    /// </summary>
    public SessionStatus Status { get; set; } = SessionStatus.Created;

    /// <summary>
    /// Session creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Last activity timestamp
    /// </summary>
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Session expiration timestamp (if applicable)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Session metadata (user-defined key-value pairs)
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Agent state snapshot
    /// </summary>
    public Dictionary<string, object> State { get; set; } = new();

    /// <summary>
    /// Conversation history for the session
    /// </summary>
    public List<SessionMessage> Messages { get; set; } = new();

    /// <summary>
    /// Session configuration
    /// </summary>
    public SessionConfiguration Configuration { get; set; } = new();

    /// <summary>
    /// Session tags for categorization
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// User ID associated with this session (optional)
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Check if session is expired
    /// </summary>
    public bool IsExpired()
    {
        return ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt.Value;
    }

    /// <summary>
    /// Check if session is active
    /// </summary>
    public bool IsActive()
    {
        return Status == SessionStatus.Active && !IsExpired();
    }

    /// <summary>
    /// Update last activity timestamp
    /// </summary>
    public void Touch()
    {
        LastActiveAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Session status enumeration
/// </summary>
public enum SessionStatus
{
    Created,
    Active,
    Paused,
    Completed,
    Expired,
    Terminated
}

/// <summary>
/// Message in a session
/// </summary>
public class SessionMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Session configuration
/// </summary>
public class SessionConfiguration
{
    /// <summary>
    /// Maximum number of messages to retain
    /// </summary>
    public int MaxMessages { get; set; } = 100;

    /// <summary>
    /// Session timeout in minutes (0 = no timeout)
    /// </summary>
    public int TimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Whether to persist session to storage
    /// </summary>
    public bool PersistToStorage { get; set; } = true;

    /// <summary>
    /// Auto-save interval in seconds (0 = manual save only)
    /// </summary>
    public int AutoSaveIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to enable session recovery
    /// </summary>
    public bool EnableRecovery { get; set; } = true;
}
