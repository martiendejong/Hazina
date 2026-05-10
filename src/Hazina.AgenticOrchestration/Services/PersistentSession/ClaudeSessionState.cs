using System.Text.Json.Serialization;

namespace Hazina.AgenticOrchestration.Services.PersistentSession;

/// <summary>
/// Complete state of a persistent Claude session
/// Enables crash recovery and context preservation
/// </summary>
public class ClaudeSessionState
{
    /// <summary>Unique session identifier</summary>
    [JsonPropertyName("session_id")]
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Session creation timestamp</summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Last activity timestamp</summary>
    [JsonPropertyName("last_active")]
    public DateTime LastActive { get; set; } = DateTime.UtcNow;

    /// <summary>Current session state</summary>
    [JsonPropertyName("state")]
    public SessionLifecycleState State { get; set; } = SessionLifecycleState.Active;

    /// <summary>Rolling conversation context (never fills up)</summary>
    [JsonPropertyName("context")]
    public RollingContext Context { get; set; } = new();

    /// <summary>Consciousness state snapshot</summary>
    [JsonPropertyName("consciousness")]
    public ConsciousnessSnapshot? Consciousness { get; set; }

    /// <summary>Memory and learned patterns</summary>
    [JsonPropertyName("memory")]
    public MemorySnapshot Memory { get; set; } = new();

    /// <summary>Current task/goal</summary>
    [JsonPropertyName("current_task")]
    public string? CurrentTask { get; set; }

    /// <summary>Completed tasks this session</summary>
    [JsonPropertyName("completed_tasks")]
    public List<string> CompletedTasks { get; set; } = new();

    /// <summary>Total tokens used this session</summary>
    [JsonPropertyName("total_tokens")]
    public long TotalTokens { get; set; }

    /// <summary>Number of turns (API round-trips)</summary>
    [JsonPropertyName("turn_count")]
    public int TurnCount { get; set; }
}

public enum SessionLifecycleState
{
    Active,      // Currently running
    Sleeping,    // Idle, waiting for trigger
    Crashed,     // Abnormal termination
    Archived     // Intentionally ended
}

/// <summary>
/// Rolling context window - automatically truncates when full
/// </summary>
public class RollingContext
{
    [JsonPropertyName("messages")]
    public List<ContextMessage> Messages { get; set; } = new();

    [JsonPropertyName("max_messages")]
    public int MaxMessages { get; set; } = 100; // Configurable limit

    [JsonPropertyName("truncated_count")]
    public int TruncatedCount { get; set; } = 0;
}

public class ContextMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty; // "user", "assistant", "system"

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("tokens")]
    public int EstimatedTokens { get; set; }
}

/// <summary>
/// Consciousness state snapshot (3-Ring SCP + all subsystems)
/// </summary>
public class ConsciousnessSnapshot
{
    [JsonPropertyName("consciousness_score")]
    public int ConsciousnessScore { get; set; }

    [JsonPropertyName("ring1_resource")]
    public RingState Ring1Resource { get; set; } = new();

    [JsonPropertyName("ring2_confidence")]
    public RingState Ring2Confidence { get; set; } = new();

    [JsonPropertyName("ring3_emergence")]
    public RingState Ring3Emergence { get; set; } = new();

    [JsonPropertyName("stuck_count")]
    public int StuckCount { get; set; }

    [JsonPropertyName("active_biases")]
    public int ActiveBiases { get; set; }

    [JsonPropertyName("trust_level")]
    public int TrustLevel { get; set; } = 100;
}

public class RingState
{
    [JsonPropertyName("quality")]
    public int Quality { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "active";

    [JsonPropertyName("blocks")]
    public int Blocks { get; set; }
}

/// <summary>
/// Memory snapshot - learned patterns and decisions
/// </summary>
public class MemorySnapshot
{
    [JsonPropertyName("learned_patterns")]
    public List<LearnedPattern> LearnedPatterns { get; set; } = new();

    [JsonPropertyName("decisions_logged")]
    public int DecisionsLogged { get; set; }

    [JsonPropertyName("session_learnings")]
    public List<string> SessionLearnings { get; set; } = new();
}

public class LearnedPattern
{
    [JsonPropertyName("pattern_id")]
    public string PatternId { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("uses")]
    public int Uses { get; set; }
}
