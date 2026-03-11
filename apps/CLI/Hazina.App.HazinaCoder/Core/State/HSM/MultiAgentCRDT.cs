using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.State.HSM;

/// <summary>
/// Multi-Agent CRDT Synchronizer - Conflict-Free Replicated Data Type
///
/// Enables N agents to work on the same state simultaneously WITHOUT locks or conflicts
/// Based on CRDT (Conflict-Free Replicated Data Type) research
///
/// Key properties:
/// - **Convergence**: All agents eventually reach the same state
/// - **Commutativity**: Operations can be applied in any order
/// - **Idempotence**: Applying the same operation twice = applying once
/// - **Partition Tolerance**: Works offline, syncs when reconnected
///
/// Algorithm: Last-Write-Wins with Vector Clocks
/// </summary>
public sealed class MultiAgentCRDT
{
    private readonly string _agentId;
    private readonly ConcurrentDictionary<string, CRDTEntry> _state = new();
    private readonly VectorClock _vectorClock;
    private readonly ConcurrentQueue<CRDTOperation> _operationLog = new();
    private readonly Dictionary<string, VectorClock> _peerClocks = new();

    public string AgentId => _agentId;
    public VectorClock CurrentClock => _vectorClock;

    public MultiAgentCRDT(string agentId)
    {
        _agentId = agentId;
        _vectorClock = new VectorClock(agentId);
    }

    /// <summary>
    /// Apply local change (will be synchronized with other agents)
    /// </summary>
    public async Task<CRDTOperationResult> ApplyLocalChangeAsync(
        string key,
        JsonElement value,
        CRDTOperationType operationType = CRDTOperationType.Set)
    {
        // Increment local clock
        _vectorClock.Increment();

        // Create operation
        var operation = new CRDTOperation
        {
            OperationId = Guid.NewGuid(),
            AgentId = _agentId,
            Key = key,
            Value = value,
            OperationType = operationType,
            VectorClock = _vectorClock.Clone(),
            Timestamp = DateTime.UtcNow
        };

        // Apply operation
        var result = ApplyOperation(operation);

        // Log operation for synchronization
        _operationLog.Enqueue(operation);

        return await Task.FromResult(result);
    }

    /// <summary>
    /// Apply operation from another agent (during sync)
    /// </summary>
    public CRDTOperationResult ApplyRemoteOperation(CRDTOperation operation)
    {
        // Update peer clock
        if (!_peerClocks.ContainsKey(operation.AgentId))
        {
            _peerClocks[operation.AgentId] = operation.VectorClock.Clone();
        }
        else
        {
            _peerClocks[operation.AgentId] = VectorClock.Merge(
                _peerClocks[operation.AgentId],
                operation.VectorClock
            );
        }

        // Apply operation
        return ApplyOperation(operation);
    }

    /// <summary>
    /// Apply CRDT operation (conflict resolution via Last-Write-Wins + Vector Clock)
    /// </summary>
    private CRDTOperationResult ApplyOperation(CRDTOperation operation)
    {
        var result = new CRDTOperationResult
        {
            Success = false,
            ConflictDetected = false,
            WinningOperation = operation
        };

        // Check if key exists
        if (_state.TryGetValue(operation.Key, out var existingEntry))
        {
            // Conflict resolution: Compare vector clocks
            var comparison = VectorClock.Compare(
                operation.VectorClock,
                existingEntry.Operation.VectorClock
            );

            switch (comparison)
            {
                case VectorClockComparison.Before:
                    // Incoming operation is older - discard it
                    result.Success = false;
                    result.ConflictDetected = true;
                    result.WinningOperation = existingEntry.Operation;
                    return result;

                case VectorClockComparison.After:
                    // Incoming operation is newer - accept it
                    break;

                case VectorClockComparison.Concurrent:
                    // Concurrent operations - use Last-Write-Wins with tie-breaker
                    result.ConflictDetected = true;

                    // Tie-breaker: Timestamp, then AgentId (lexicographic)
                    if (operation.Timestamp > existingEntry.Operation.Timestamp)
                    {
                        // Incoming wins
                        break;
                    }
                    else if (operation.Timestamp == existingEntry.Operation.Timestamp)
                    {
                        // Same timestamp - use AgentId as tie-breaker
                        if (string.Compare(operation.AgentId, existingEntry.Operation.AgentId,
                            StringComparison.Ordinal) > 0)
                        {
                            // Incoming wins
                            break;
                        }
                        else
                        {
                            // Existing wins
                            result.Success = false;
                            result.WinningOperation = existingEntry.Operation;
                            return result;
                        }
                    }
                    else
                    {
                        // Existing wins
                        result.Success = false;
                        result.WinningOperation = existingEntry.Operation;
                        return result;
                    }
                    break;
            }
        }

        // Apply operation
        switch (operation.OperationType)
        {
            case CRDTOperationType.Set:
                _state[operation.Key] = new CRDTEntry
                {
                    Key = operation.Key,
                    Value = operation.Value,
                    Operation = operation
                };
                result.Success = true;
                break;

            case CRDTOperationType.Delete:
                _state.TryRemove(operation.Key, out _);
                result.Success = true;
                break;
        }

        return result;
    }

    /// <summary>
    /// Get current value for key
    /// </summary>
    public JsonElement? GetValue(string key)
    {
        if (_state.TryGetValue(key, out var entry))
        {
            return entry.Value;
        }
        return null;
    }

    /// <summary>
    /// Get all keys
    /// </summary>
    public IEnumerable<string> GetAllKeys()
    {
        return _state.Keys;
    }

    /// <summary>
    /// Synchronize with another agent (merge states)
    /// </summary>
    public async Task<SyncResult> SyncWithAgentAsync(MultiAgentCRDT otherAgent)
    {
        var result = new SyncResult
        {
            OperationsReceived = 0,
            OperationsSent = 0,
            ConflictsResolved = 0
        };

        // Get operations from other agent that we haven't seen
        var theirOperations = otherAgent._operationLog
            .Where(op => !HasSeenOperation(op))
            .ToList();

        // Apply their operations
        foreach (var operation in theirOperations)
        {
            var opResult = ApplyRemoteOperation(operation);
            result.OperationsReceived++;

            if (opResult.ConflictDetected)
            {
                result.ConflictsResolved++;
            }
        }

        // Send our operations to them
        var ourOperations = _operationLog
            .Where(op => !otherAgent.HasSeenOperation(op))
            .ToList();

        foreach (var operation in ourOperations)
        {
            otherAgent.ApplyRemoteOperation(operation);
            result.OperationsSent++;
        }

        return await Task.FromResult(result);
    }

    /// <summary>
    /// Check if we've seen this operation before (via vector clock)
    /// </summary>
    private bool HasSeenOperation(CRDTOperation operation)
    {
        // Check if operation's vector clock is dominated by our current clock
        var comparison = VectorClock.Compare(operation.VectorClock, _vectorClock);
        return comparison == VectorClockComparison.Before;
    }

    /// <summary>
    /// Get sync status with all known agents
    /// </summary>
    public Dictionary<string, SyncStatus> GetSyncStatus()
    {
        var status = new Dictionary<string, SyncStatus>();

        foreach (var kvp in _peerClocks)
        {
            var agentId = kvp.Key;
            var peerClock = kvp.Value;

            var comparison = VectorClock.Compare(_vectorClock, peerClock);
            var isSynced = comparison == VectorClockComparison.Concurrent ||
                          comparison == VectorClockComparison.Equal;

            status[agentId] = new SyncStatus
            {
                AgentId = agentId,
                IsSynced = isSynced,
                TheirClock = peerClock,
                OurClock = _vectorClock
            };
        }

        return status;
    }

    /// <summary>
    /// Export state as JSON (for persistence or network transfer)
    /// </summary>
    public string ExportState()
    {
        var export = new CRDTStateExport
        {
            AgentId = _agentId,
            VectorClock = _vectorClock,
            State = _state.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            OperationLog = _operationLog.ToList()
        };

        return JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    /// <summary>
    /// Import state from JSON
    /// </summary>
    public void ImportState(string json)
    {
        var import = JsonSerializer.Deserialize<CRDTStateExport>(json);
        if (import == null)
            return;

        // Import operations
        foreach (var operation in import.OperationLog)
        {
            ApplyRemoteOperation(operation);
        }
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    public CRDTStatistics GetStatistics()
    {
        return new CRDTStatistics
        {
            AgentId = _agentId,
            TotalKeys = _state.Count,
            TotalOperations = _operationLog.Count,
            VectorClockSize = _vectorClock.GetSize(),
            KnownPeers = _peerClocks.Count,
            StateSize = _state.Sum(kvp => kvp.Value.Value.GetRawText().Length)
        };
    }
}

/// <summary>
/// Vector Clock - Tracks causal relationships between events
/// </summary>
public sealed class VectorClock
{
    private readonly ConcurrentDictionary<string, long> _clock = new();
    private readonly string _ownerId;

    public VectorClock(string ownerId)
    {
        _ownerId = ownerId;
        _clock[ownerId] = 0;
    }

    /// <summary>
    /// Increment local clock
    /// </summary>
    public void Increment()
    {
        _clock.AddOrUpdate(_ownerId, 1, (_, current) => current + 1);
    }

    /// <summary>
    /// Get clock value for agent
    /// </summary>
    public long Get(string agentId)
    {
        return _clock.TryGetValue(agentId, out var value) ? value : 0;
    }

    /// <summary>
    /// Update clock value for agent
    /// </summary>
    public void Set(string agentId, long value)
    {
        _clock[agentId] = value;
    }

    /// <summary>
    /// Merge two vector clocks (take maximum of each component)
    /// </summary>
    public static VectorClock Merge(VectorClock a, VectorClock b)
    {
        var merged = new VectorClock(a._ownerId);

        var allAgents = a._clock.Keys.Union(b._clock.Keys);
        foreach (var agent in allAgents)
        {
            merged.Set(agent, Math.Max(a.Get(agent), b.Get(agent)));
        }

        return merged;
    }

    /// <summary>
    /// Compare two vector clocks
    /// </summary>
    public static VectorClockComparison Compare(VectorClock a, VectorClock b)
    {
        var allAgents = a._clock.Keys.Union(b._clock.Keys).ToList();

        bool aBeforeB = false;
        bool bBeforeA = false;

        foreach (var agent in allAgents)
        {
            var aValue = a.Get(agent);
            var bValue = b.Get(agent);

            if (aValue < bValue)
                aBeforeB = true;
            else if (aValue > bValue)
                bBeforeA = true;
        }

        if (aBeforeB && !bBeforeA)
            return VectorClockComparison.Before;
        else if (!aBeforeB && bBeforeA)
            return VectorClockComparison.After;
        else if (!aBeforeB && !bBeforeA)
            return VectorClockComparison.Equal;
        else
            return VectorClockComparison.Concurrent;
    }

    /// <summary>
    /// Clone vector clock
    /// </summary>
    public VectorClock Clone()
    {
        var clone = new VectorClock(_ownerId);
        foreach (var kvp in _clock)
        {
            clone.Set(kvp.Key, kvp.Value);
        }
        return clone;
    }

    /// <summary>
    /// Get size (number of agents tracked)
    /// </summary>
    public int GetSize() => _clock.Count;

    public override string ToString()
    {
        return string.Join(", ", _clock.OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key}:{kvp.Value}"));
    }
}

/// <summary>
/// Vector clock comparison result
/// </summary>
public enum VectorClockComparison
{
    Before,      // A happened before B
    After,       // A happened after B
    Equal,       // A and B are the same
    Concurrent   // A and B are concurrent (no causal relationship)
}

/// <summary>
/// CRDT operation
/// </summary>
public sealed class CRDTOperation
{
    public required Guid OperationId { get; init; }
    public required string AgentId { get; init; }
    public required string Key { get; init; }
    public required JsonElement Value { get; init; }
    public required CRDTOperationType OperationType { get; init; }
    public required VectorClock VectorClock { get; init; }
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// CRDT operation type
/// </summary>
public enum CRDTOperationType
{
    Set,     // Set value
    Delete   // Delete key
}

/// <summary>
/// CRDT entry (key-value with metadata)
/// </summary>
public sealed class CRDTEntry
{
    public required string Key { get; init; }
    public required JsonElement Value { get; init; }
    public required CRDTOperation Operation { get; init; }
}

/// <summary>
/// CRDT operation result
/// </summary>
public sealed class CRDTOperationResult
{
    public bool Success { get; set; }
    public bool ConflictDetected { get; set; }
    public required CRDTOperation WinningOperation { get; set; }
}

/// <summary>
/// Sync result
/// </summary>
public sealed class SyncResult
{
    public int OperationsReceived { get; set; }
    public int OperationsSent { get; set; }
    public int ConflictsResolved { get; set; }
}

/// <summary>
/// Sync status
/// </summary>
public sealed class SyncStatus
{
    public required string AgentId { get; init; }
    public bool IsSynced { get; set; }
    public required VectorClock TheirClock { get; init; }
    public required VectorClock OurClock { get; init; }
}

/// <summary>
/// CRDT state export (for persistence or network transfer)
/// </summary>
public sealed class CRDTStateExport
{
    public required string AgentId { get; init; }
    public required VectorClock VectorClock { get; init; }
    public required Dictionary<string, CRDTEntry> State { get; init; }
    public required List<CRDTOperation> OperationLog { get; init; }
}

/// <summary>
/// CRDT statistics
/// </summary>
public sealed class CRDTStatistics
{
    public required string AgentId { get; init; }
    public int TotalKeys { get; init; }
    public int TotalOperations { get; init; }
    public int VectorClockSize { get; init; }
    public int KnownPeers { get; init; }
    public long StateSize { get; init; }
}
