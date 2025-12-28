using System.Text.Json;

namespace Hazina.LLMs.GoogleADK.Core;

/// <summary>
/// Represents the state of an agent during execution.
/// Stores configuration, runtime data, and session information.
/// </summary>
public class AgentState
{
    private readonly Dictionary<string, object> _state = new();
    private readonly object _lock = new();

    /// <summary>
    /// Unique identifier for this agent instance
    /// </summary>
    public string AgentId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of the agent
    /// </summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>
    /// Current session ID
    /// </summary>
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    /// Agent status
    /// </summary>
    public AgentStatus Status { get; set; } = AgentStatus.Idle;

    /// <summary>
    /// When the agent was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the agent was last active
    /// </summary>
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Configuration parameters for this agent
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();

    /// <summary>
    /// Get a value from agent state
    /// </summary>
    public T? Get<T>(string key)
    {
        lock (_lock)
        {
            if (_state.TryGetValue(key, out var value))
            {
                if (value is JsonElement jsonElement)
                {
                    return JsonSerializer.Deserialize<T>(jsonElement.GetRawText());
                }
                return (T)value;
            }
            return default;
        }
    }

    /// <summary>
    /// Set a value in agent state
    /// </summary>
    public void Set<T>(string key, T value)
    {
        lock (_lock)
        {
            _state[key] = value!;
            LastActiveAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Check if a key exists in state
    /// </summary>
    public bool Has(string key)
    {
        lock (_lock)
        {
            return _state.ContainsKey(key);
        }
    }

    /// <summary>
    /// Remove a key from state
    /// </summary>
    public bool Remove(string key)
    {
        lock (_lock)
        {
            return _state.Remove(key);
        }
    }

    /// <summary>
    /// Clear all state data
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _state.Clear();
        }
    }

    /// <summary>
    /// Get all state keys
    /// </summary>
    public IEnumerable<string> Keys
    {
        get
        {
            lock (_lock)
            {
                return _state.Keys.ToList();
            }
        }
    }

    /// <summary>
    /// Get snapshot of current state
    /// </summary>
    public Dictionary<string, object> GetSnapshot()
    {
        lock (_lock)
        {
            return new Dictionary<string, object>(_state);
        }
    }

    /// <summary>
    /// Restore state from snapshot
    /// </summary>
    public void RestoreSnapshot(Dictionary<string, object> snapshot)
    {
        lock (_lock)
        {
            _state.Clear();
            foreach (var kvp in snapshot)
            {
                _state[kvp.Key] = kvp.Value;
            }
        }
    }
}

/// <summary>
/// Agent execution status
/// </summary>
public enum AgentStatus
{
    /// <summary>Agent is idle and not executing</summary>
    Idle,

    /// <summary>Agent is initializing</summary>
    Initializing,

    /// <summary>Agent is actively executing</summary>
    Running,

    /// <summary>Agent is waiting for external input</summary>
    Waiting,

    /// <summary>Agent execution is paused</summary>
    Paused,

    /// <summary>Agent has completed execution</summary>
    Completed,

    /// <summary>Agent encountered an error</summary>
    Error,

    /// <summary>Agent was cancelled</summary>
    Cancelled
}
