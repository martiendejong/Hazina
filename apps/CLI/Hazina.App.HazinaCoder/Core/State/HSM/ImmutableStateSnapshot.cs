using System;
using System.Collections.Immutable;
using System.Text.Json;
using Hazina.App.HazinaCoder.Core.Identity;

namespace Hazina.App.HazinaCoder.Core.State.HSM;

/// <summary>
/// Immutable state snapshot - foundation of the Hyperdimensional State Machine
/// Uses structural sharing for memory efficiency (persistent data structures)
/// Every state change creates a new snapshot that shares unchanged data
/// </summary>
public sealed record ImmutableStateSnapshot
{
    /// <summary>
    /// Unique identifier for this state snapshot
    /// </summary>
    public required StateId Id { get; init; }

    /// <summary>
    /// Timestamp when this state was created (UTC)
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    /// Reference to the previous state (null for initial state)
    /// Forms a linked list for time-travel
    /// </summary>
    public StateId? PreviousStateId { get; init; }

    /// <summary>
    /// The event that caused transition to this state
    /// </summary>
    public StateEvent? CausingEvent { get; init; }

    /// <summary>
    /// Session metadata (immutable)
    /// </summary>
    public required SessionMetadata Session { get; init; }

    /// <summary>
    /// Active goals (immutable list with structural sharing)
    /// </summary>
    public ImmutableList<Goal> Goals { get; init; } = ImmutableList<Goal>.Empty;

    /// <summary>
    /// Working memory - recent files accessed (bounded to 7±2 items)
    /// </summary>
    public ImmutableList<string> WorkingMemory { get; init; } = ImmutableList<string>.Empty;

    /// <summary>
    /// Recent decisions made by the agent
    /// </summary>
    public ImmutableList<Decision> Decisions { get; init; } = ImmutableList<Decision>.Empty;

    /// <summary>
    /// Current attention focus
    /// </summary>
    public required AttentionFocus Focus { get; init; }

    /// <summary>
    /// Cognitive load assessment
    /// </summary>
    public required CognitiveLoad Load { get; init; }

    /// <summary>
    /// Workflow mode (feature development vs active debugging)
    /// </summary>
    public required WorkflowMode Mode { get; init; }

    /// <summary>
    /// Custom state properties (immutable dictionary)
    /// </summary>
    public ImmutableDictionary<string, JsonElement> CustomState { get; init; }
        = ImmutableDictionary<string, JsonElement>.Empty;

    /// <summary>
    /// Computed hash of this state (for equality and deduplication)
    /// </summary>
    public string StateHash => ComputeHash();

    /// <summary>
    /// Create initial state snapshot
    /// </summary>
    public static ImmutableStateSnapshot CreateInitial(string sessionId, string environment)
    {
        return new ImmutableStateSnapshot
        {
            Id = StateId.Generate(),
            Timestamp = DateTime.UtcNow,
            PreviousStateId = null,
            CausingEvent = null,
            Session = new SessionMetadata
            {
                SessionId = sessionId,
                Environment = environment,
                StartTime = DateTime.UtcNow
            },
            Focus = new AttentionFocus
            {
                Area = "initialization",
                PriorityPercent = 100,
                FocusStarted = DateTime.UtcNow
            },
            Load = CognitiveLoad.Low,
            Mode = WorkflowMode.FeatureDevelopment
        };
    }

    /// <summary>
    /// Apply state change and create new snapshot (immutable transformation)
    /// Uses structural sharing - only changed parts are copied
    /// </summary>
    public ImmutableStateSnapshot ApplyChange(StateEvent stateEvent,
        Func<ImmutableStateSnapshot, ImmutableStateSnapshot> transformer)
    {
        // Create new snapshot via transformation
        var newState = transformer(this) with
        {
            Id = StateId.Generate(),
            Timestamp = DateTime.UtcNow,
            PreviousStateId = this.Id,
            CausingEvent = stateEvent
        };

        return newState;
    }

    /// <summary>
    /// Compute SHA256 hash of state for equality checking
    /// </summary>
    private string ComputeHash()
    {
        // Use record equality semantics + SHA256
        var json = JsonSerializer.Serialize(this);
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Serialize to JSON for persistence
    /// </summary>
    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    /// <summary>
    /// Deserialize from JSON
    /// </summary>
    public static ImmutableStateSnapshot FromJson(string json)
        => JsonSerializer.Deserialize<ImmutableStateSnapshot>(json)!;
}

/// <summary>
/// State identifier - globally unique, sortable by time
/// </summary>
public record struct StateId(Guid Id, long Sequence)
{
    private static long _sequenceCounter = 0;

    public static StateId Generate()
    {
        return new StateId(
            Guid.NewGuid(),
            Interlocked.Increment(ref _sequenceCounter)
        );
    }

    public override string ToString() => $"{Sequence:D10}_{Id:N}";
}

/// <summary>
/// Session metadata - immutable session info
/// </summary>
public sealed record SessionMetadata
{
    public required string SessionId { get; init; }
    public required string Environment { get; init; }
    public required DateTime StartTime { get; init; }
}

/// <summary>
/// State event - describes why state changed
/// </summary>
public sealed record StateEvent
{
    public required string EventType { get; init; }
    public required string Description { get; init; }
    public required DateTime Timestamp { get; init; }
    public ImmutableDictionary<string, JsonElement> Metadata { get; init; }
        = ImmutableDictionary<string, JsonElement>.Empty;

    public static StateEvent Create(string eventType, string description)
    {
        return new StateEvent
        {
            EventType = eventType,
            Description = description,
            Timestamp = DateTime.UtcNow
        };
    }
}
