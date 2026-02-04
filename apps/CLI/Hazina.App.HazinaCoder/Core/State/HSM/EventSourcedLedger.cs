using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IO;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.State.HSM;

/// <summary>
/// Event-sourced ledger - blockchain-like immutable state history
/// Every state transition is recorded as an append-only log
/// Enables: time-travel, audit trail, replay, causality analysis
/// </summary>
public sealed class EventSourcedLedger
{
    private readonly ConcurrentDictionary<StateId, ImmutableStateSnapshot> _stateCache = new();
    private readonly ConcurrentQueue<LedgerEntry> _eventLog = new();
    private readonly string _persistencePath;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public EventSourcedLedger(string persistencePath)
    {
        _persistencePath = persistencePath;
        Directory.CreateDirectory(Path.GetDirectoryName(persistencePath)!);

        // Load from disk if exists
        if (File.Exists(persistencePath))
        {
            LoadFromDisk();
        }
    }

    /// <summary>
    /// Append new state to ledger (immutable, append-only)
    /// </summary>
    public async Task<StateId> AppendAsync(ImmutableStateSnapshot state)
    {
        var entry = new LedgerEntry
        {
            StateId = state.Id,
            Timestamp = state.Timestamp,
            PreviousStateId = state.PreviousStateId,
            EventType = state.CausingEvent?.EventType ?? "initial",
            EventDescription = state.CausingEvent?.Description ?? "Initial state",
            StateSnapshot = state
        };

        // Add to in-memory cache
        _stateCache[state.Id] = state;
        _eventLog.Enqueue(entry);

        // Persist to disk (append-only)
        await PersistToDiskAsync(entry);

        return state.Id;
    }

    /// <summary>
    /// Get state by ID (from cache or rebuild from events)
    /// </summary>
    public ImmutableStateSnapshot? GetState(StateId stateId)
    {
        if (_stateCache.TryGetValue(stateId, out var cached))
        {
            return cached;
        }

        // Rebuild from event log if not in cache
        return RebuildState(stateId);
    }

    /// <summary>
    /// Get all states in chronological order
    /// </summary>
    public IEnumerable<ImmutableStateSnapshot> GetHistory()
    {
        return _eventLog
            .OrderBy(e => e.Timestamp)
            .Select(e => e.StateSnapshot);
    }

    /// <summary>
    /// Get states within time range
    /// </summary>
    public IEnumerable<ImmutableStateSnapshot> GetStatesBetween(DateTime start, DateTime end)
    {
        return _eventLog
            .Where(e => e.Timestamp >= start && e.Timestamp <= end)
            .OrderBy(e => e.Timestamp)
            .Select(e => e.StateSnapshot);
    }

    /// <summary>
    /// Get causal chain from initial state to target state
    /// Returns: [InitialState] → [State1] → [State2] → ... → [TargetState]
    /// </summary>
    public List<ImmutableStateSnapshot> GetCausalChain(StateId targetStateId)
    {
        var chain = new List<ImmutableStateSnapshot>();
        var currentState = GetState(targetStateId);

        while (currentState != null)
        {
            chain.Insert(0, currentState); // Prepend to maintain chronological order

            if (currentState.PreviousStateId == null)
                break;

            currentState = GetState(currentState.PreviousStateId.Value);
        }

        return chain;
    }

    /// <summary>
    /// Explain causality: "Why are we in this state?"
    /// Returns human-readable causal explanation
    /// </summary>
    public string ExplainCausality(StateId targetStateId)
    {
        var chain = GetCausalChain(targetStateId);
        var explanation = new System.Text.StringBuilder();

        explanation.AppendLine($"=== CAUSAL CHAIN TO STATE {targetStateId} ===\n");

        for (int i = 0; i < chain.Count; i++)
        {
            var state = chain[i];
            var arrow = i < chain.Count - 1 ? " → " : "";

            explanation.AppendLine($"[{i + 1}] {state.Timestamp:HH:mm:ss.fff}");
            explanation.AppendLine($"    Event: {state.CausingEvent?.EventType ?? "INITIAL"}");
            explanation.AppendLine($"    Reason: {state.CausingEvent?.Description ?? "Session start"}");
            explanation.AppendLine($"    State: {state.Id}");
            if (i < chain.Count - 1)
                explanation.AppendLine("      ↓");
        }

        return explanation.ToString();
    }

    /// <summary>
    /// Replay events from a specific state with modifications
    /// Enables "what if" analysis - create alternate timeline
    /// </summary>
    public async Task<ImmutableStateSnapshot> ReplayWithModificationsAsync(
        StateId fromStateId,
        Func<StateEvent, StateEvent?> eventModifier)
    {
        var chain = GetCausalChain(fromStateId);
        if (chain.Count == 0)
            throw new InvalidOperationException($"State {fromStateId} not found");

        // Start from the state before the modification point
        var baseState = chain[^1];

        // Replay subsequent events with modifications
        var currentState = baseState;
        var subsequentStates = GetHistory()
            .SkipWhile(s => s.Id != fromStateId)
            .Skip(1); // Skip the fromState itself

        foreach (var originalState in subsequentStates)
        {
            if (originalState.CausingEvent == null)
                continue;

            // Allow modifier to change or skip events
            var modifiedEvent = eventModifier(originalState.CausingEvent);
            if (modifiedEvent == null)
                continue; // Skip this event

            // Apply modified event
            currentState = currentState.ApplyChange(modifiedEvent, state => state);
            await AppendAsync(currentState);
        }

        return currentState;
    }

    /// <summary>
    /// Get state at specific timestamp (temporal query)
    /// </summary>
    public ImmutableStateSnapshot? GetStateAt(DateTime timestamp)
    {
        return _eventLog
            .Where(e => e.Timestamp <= timestamp)
            .OrderByDescending(e => e.Timestamp)
            .Select(e => e.StateSnapshot)
            .FirstOrDefault();
    }

    /// <summary>
    /// Persist ledger entry to disk (append-only log)
    /// </summary>
    private async Task PersistToDiskAsync(LedgerEntry entry)
    {
        await _writeLock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions
            {
                WriteIndented = false // Compact for append-only log
            });

            await File.AppendAllLinesAsync(_persistencePath, new[] { json });
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>
    /// Load ledger from disk on startup
    /// </summary>
    private void LoadFromDisk()
    {
        var lines = File.ReadAllLines(_persistencePath);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var entry = JsonSerializer.Deserialize<LedgerEntry>(line);
                if (entry != null)
                {
                    _eventLog.Enqueue(entry);
                    _stateCache[entry.StateId] = entry.StateSnapshot;
                }
            }
            catch (JsonException ex)
            {
                // Log corruption but continue loading
                Console.WriteLine($"Warning: Corrupted ledger entry: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Rebuild state from event log (event sourcing)
    /// </summary>
    private ImmutableStateSnapshot? RebuildState(StateId stateId)
    {
        var chain = GetCausalChain(stateId);
        return chain.LastOrDefault();
    }

    /// <summary>
    /// Get ledger statistics
    /// </summary>
    public LedgerStats GetStats()
    {
        return new LedgerStats
        {
            TotalStates = _stateCache.Count,
            TotalEvents = _eventLog.Count,
            OldestState = _eventLog.MinBy(e => e.Timestamp)?.Timestamp,
            NewestState = _eventLog.MaxBy(e => e.Timestamp)?.Timestamp,
            MemoryUsageBytes = _stateCache.Sum(kvp =>
                System.Text.Encoding.UTF8.GetByteCount(kvp.Value.ToJson()))
        };
    }
}

/// <summary>
/// Ledger entry - single row in the event log
/// </summary>
internal sealed record LedgerEntry
{
    public required StateId StateId { get; init; }
    public required DateTime Timestamp { get; init; }
    public StateId? PreviousStateId { get; init; }
    public required string EventType { get; init; }
    public required string EventDescription { get; init; }
    public required ImmutableStateSnapshot StateSnapshot { get; init; }
}

/// <summary>
/// Ledger statistics
/// </summary>
public sealed record LedgerStats
{
    public int TotalStates { get; init; }
    public int TotalEvents { get; init; }
    public DateTime? OldestState { get; init; }
    public DateTime? NewestState { get; init; }
    public long MemoryUsageBytes { get; init; }

    public string MemoryUsageMB => $"{MemoryUsageBytes / 1024.0 / 1024.0:F2} MB";
}
