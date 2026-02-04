using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.State.HSM;

/// <summary>
/// Cognitive Load Balancer - Brain-inspired working memory management
///
/// Based on cognitive science research:
/// - Working memory capacity: 7±2 items (Miller's Law)
/// - Attention spotlight: 1 primary focus + 3 peripheral items
/// - Context switching cost: ~20 minutes to recover full focus
/// - Mental fatigue: Performance degrades after 90 minutes intense work
///
/// Automatically manages cognitive load to prevent overload and maintain optimal performance
/// </summary>
public sealed class CognitiveLoadBalancer
{
    private const int MaxWorkingMemoryCapacity = 9;  // 7 + 2
    private const int OptimalWorkingMemoryCapacity = 7;
    private const int MinWorkingMemoryCapacity = 5;  // 7 - 2

    private const int MaxAttentionSpotlight = 1;     // Primary focus
    private const int MaxPeripheralAwareness = 3;    // Secondary tasks

    private readonly Queue<WorkingMemoryItem> _workingMemory = new();
    private readonly Dictionary<string, WorkingMemoryItem> _workingMemoryIndex = new();

    private AttentionState _attentionState = new();
    private CognitiveMetrics _metrics = new();
    private DateTime _lastContextSwitch = DateTime.UtcNow;
    private DateTime _lastIntenseWorkStart = DateTime.UtcNow;

    /// <summary>
    /// Current cognitive load level
    /// </summary>
    public CognitiveLoad CurrentLoad { get; private set; } = CognitiveLoad.Low;

    /// <summary>
    /// Current working memory items (bounded to 7±2)
    /// </summary>
    public IReadOnlyList<WorkingMemoryItem> WorkingMemory => _workingMemory.ToList();

    /// <summary>
    /// Current attention state
    /// </summary>
    public AttentionState AttentionState => _attentionState;

    /// <summary>
    /// Cognitive metrics for monitoring
    /// </summary>
    public CognitiveMetrics Metrics => _metrics;

    /// <summary>
    /// Add item to working memory (automatic eviction if full)
    /// </summary>
    public WorkingMemoryEviction AddToWorkingMemory(string itemId, string content, WorkingMemoryType type)
    {
        var eviction = new WorkingMemoryEviction { EvictionOccurred = false };

        // If already exists, refresh (move to front)
        if (_workingMemoryIndex.ContainsKey(itemId))
        {
            RefreshWorkingMemory(itemId);
            return eviction;
        }

        // Create new item
        var item = new WorkingMemoryItem
        {
            Id = itemId,
            Content = content,
            Type = type,
            AddedAt = DateTime.UtcNow,
            LastAccessedAt = DateTime.UtcNow,
            AccessCount = 1
        };

        // Check capacity
        if (_workingMemory.Count >= MaxWorkingMemoryCapacity)
        {
            // Evict LRU item
            var evicted = EvictLRU();
            eviction.EvictionOccurred = true;
            eviction.EvictedItem = evicted;

            _metrics.TotalEvictions++;
        }

        // Add to working memory
        _workingMemory.Enqueue(item);
        _workingMemoryIndex[itemId] = item;

        // Update load
        UpdateCognitiveLoad();

        return eviction;
    }

    /// <summary>
    /// Access item in working memory (refreshes last accessed time)
    /// </summary>
    public void AccessWorkingMemory(string itemId)
    {
        if (_workingMemoryIndex.TryGetValue(itemId, out var item))
        {
            item.LastAccessedAt = DateTime.UtcNow;
            item.AccessCount++;
        }
    }

    /// <summary>
    /// Refresh item (move to front of LRU queue)
    /// </summary>
    private void RefreshWorkingMemory(string itemId)
    {
        if (!_workingMemoryIndex.TryGetValue(itemId, out var item))
            return;

        // Remove and re-add to move to back of queue
        var tempList = _workingMemory.ToList();
        tempList.Remove(item);
        _workingMemory.Clear();
        foreach (var i in tempList)
            _workingMemory.Enqueue(i);

        item.LastAccessedAt = DateTime.UtcNow;
        item.AccessCount++;
        _workingMemory.Enqueue(item);
    }

    /// <summary>
    /// Evict least recently used item
    /// </summary>
    private WorkingMemoryItem EvictLRU()
    {
        var evicted = _workingMemory.Dequeue();
        _workingMemoryIndex.Remove(evicted.Id);
        return evicted;
    }

    /// <summary>
    /// Set primary focus (attention spotlight)
    /// </summary>
    public void SetPrimaryFocus(string taskId, string description)
    {
        // Check context switch cost
        if (_attentionState.PrimaryFocus != null &&
            _attentionState.PrimaryFocus.TaskId != taskId)
        {
            var timeSinceLastSwitch = DateTime.UtcNow - _lastContextSwitch;
            _lastContextSwitch = DateTime.UtcNow;

            if (timeSinceLastSwitch < TimeSpan.FromMinutes(20))
            {
                _metrics.FrequentContextSwitches++;
            }
        }

        _attentionState.PrimaryFocus = new AttentionItem
        {
            TaskId = taskId,
            Description = description,
            StartedAt = DateTime.UtcNow
        };

        UpdateCognitiveLoad();
    }

    /// <summary>
    /// Add peripheral awareness item (max 3)
    /// </summary>
    public bool AddPeripheralAwareness(string taskId, string description)
    {
        if (_attentionState.PeripheralAwareness.Count >= MaxPeripheralAwareness)
            return false;

        _attentionState.PeripheralAwareness.Add(new AttentionItem
        {
            TaskId = taskId,
            Description = description,
            StartedAt = DateTime.UtcNow
        });

        UpdateCognitiveLoad();
        return true;
    }

    /// <summary>
    /// Remove peripheral awareness item
    /// </summary>
    public void RemovePeripheralAwareness(string taskId)
    {
        _attentionState.PeripheralAwareness.RemoveAll(a => a.TaskId == taskId);
        UpdateCognitiveLoad();
    }

    /// <summary>
    /// Clear all peripheral awareness (when overloaded)
    /// </summary>
    public void ClearPeripheralAwareness()
    {
        _attentionState.PeripheralAwareness.Clear();
        UpdateCognitiveLoad();
    }

    /// <summary>
    /// Assess current cognitive load and update state
    /// </summary>
    public CognitiveLoad AssessCognitiveLoad()
    {
        var factors = new List<double>();

        // Factor 1: Working memory utilization (0.0 - 1.0)
        var memoryUtilization = _workingMemory.Count / (double)MaxWorkingMemoryCapacity;
        factors.Add(memoryUtilization);

        // Factor 2: Attention complexity (0.0 - 1.0)
        var attentionCount = (_attentionState.PrimaryFocus != null ? 1 : 0)
            + _attentionState.PeripheralAwareness.Count;
        var attentionComplexity = attentionCount / (double)(MaxAttentionSpotlight + MaxPeripheralAwareness);
        factors.Add(attentionComplexity);

        // Factor 3: Context switching frequency (0.0 - 1.0)
        var contextSwitchPenalty = Math.Min(1.0, _metrics.FrequentContextSwitches / 10.0);
        factors.Add(contextSwitchPenalty);

        // Factor 4: Time in intense work (mental fatigue) (0.0 - 1.0)
        var intenseWorkDuration = DateTime.UtcNow - _lastIntenseWorkStart;
        var mentalFatigue = Math.Min(1.0, intenseWorkDuration.TotalMinutes / 90.0);
        factors.Add(mentalFatigue);

        // Calculate aggregate load (weighted average)
        var aggregateLoad = (memoryUtilization * 0.3) +
                           (attentionComplexity * 0.3) +
                           (contextSwitchPenalty * 0.2) +
                           (mentalFatigue * 0.2);

        // Map to cognitive load level
        var load = aggregateLoad switch
        {
            < 0.3 => CognitiveLoad.Low,
            < 0.5 => CognitiveLoad.Moderate,
            < 0.7 => CognitiveLoad.High,
            _ => CognitiveLoad.Overload
        };

        CurrentLoad = load;
        return load;
    }

    /// <summary>
    /// Update cognitive load (called after state changes)
    /// </summary>
    private void UpdateCognitiveLoad()
    {
        var previousLoad = CurrentLoad;
        var newLoad = AssessCognitiveLoad();

        // Track load changes
        if (newLoad != previousLoad)
        {
            _metrics.LoadChangeCount++;
            _metrics.LastLoadChange = DateTime.UtcNow;

            if (newLoad == CognitiveLoad.Overload)
            {
                _metrics.OverloadCount++;
            }
        }
    }

    /// <summary>
    /// Compress working memory (remove least important items)
    /// Used when load is high/overload
    /// </summary>
    public List<WorkingMemoryItem> CompressWorkingMemory(int targetCapacity = OptimalWorkingMemoryCapacity)
    {
        var removed = new List<WorkingMemoryItem>();

        while (_workingMemory.Count > targetCapacity)
        {
            // Find least important item (lowest access count + oldest)
            var leastImportant = _workingMemory
                .OrderBy(i => i.AccessCount)
                .ThenBy(i => i.LastAccessedAt)
                .First();

            _workingMemory.ToList().Remove(leastImportant);
            _workingMemoryIndex.Remove(leastImportant.Id);
            removed.Add(leastImportant);

            // Rebuild queue
            var temp = _workingMemory.ToList();
            _workingMemory.Clear();
            foreach (var item in temp)
            {
                if (item.Id != leastImportant.Id)
                    _workingMemory.Enqueue(item);
            }
        }

        _metrics.CompressionCount++;
        UpdateCognitiveLoad();

        return removed;
    }

    /// <summary>
    /// Check if should take a mental break (prevent burnout)
    /// </summary>
    public BreakRecommendation ShouldTakeBreak()
    {
        var intenseWorkDuration = DateTime.UtcNow - _lastIntenseWorkStart;

        var recommendation = new BreakRecommendation
        {
            ShouldBreak = false,
            Urgency = BreakUrgency.None,
            Reason = string.Empty,
            RecommendedDurationMinutes = 0
        };

        // Check overload
        if (CurrentLoad == CognitiveLoad.Overload)
        {
            recommendation.ShouldBreak = true;
            recommendation.Urgency = BreakUrgency.Immediate;
            recommendation.Reason = "Cognitive overload detected - immediate break needed";
            recommendation.RecommendedDurationMinutes = 5;
            return recommendation;
        }

        // Check mental fatigue (90+ minutes)
        if (intenseWorkDuration >= TimeSpan.FromMinutes(90))
        {
            recommendation.ShouldBreak = true;
            recommendation.Urgency = BreakUrgency.High;
            recommendation.Reason = "Mental fatigue - 90+ minutes of intense work";
            recommendation.RecommendedDurationMinutes = 15;
            return recommendation;
        }

        // Check frequent context switching
        if (_metrics.FrequentContextSwitches >= 10)
        {
            recommendation.ShouldBreak = true;
            recommendation.Urgency = BreakUrgency.Medium;
            recommendation.Reason = "Frequent context switching - consolidation needed";
            recommendation.RecommendedDurationMinutes = 10;
            return recommendation;
        }

        return recommendation;
    }

    /// <summary>
    /// Mark break taken (reset fatigue counters)
    /// </summary>
    public void BreakTaken(int durationMinutes)
    {
        _lastIntenseWorkStart = DateTime.UtcNow;
        _metrics.FrequentContextSwitches = 0;
        _metrics.TotalBreaksTaken++;
        _metrics.TotalBreakMinutes += durationMinutes;

        // If break was long enough, reduce load
        if (durationMinutes >= 10)
        {
            UpdateCognitiveLoad();
        }
    }

    /// <summary>
    /// Save interruption point (for context restoration)
    /// </summary>
    public InterruptionPoint SaveInterruptionPoint(string reason)
    {
        return new InterruptionPoint
        {
            Timestamp = DateTime.UtcNow,
            Reason = reason,
            WorkingMemorySnapshot = _workingMemory.ToList(),
            AttentionStateSnapshot = new AttentionState
            {
                PrimaryFocus = _attentionState.PrimaryFocus,
                PeripheralAwareness = new List<AttentionItem>(_attentionState.PeripheralAwareness)
            },
            CognitiveLoadSnapshot = CurrentLoad
        };
    }

    /// <summary>
    /// Restore from interruption point (instant context recovery)
    /// </summary>
    public void RestoreFromInterruptionPoint(InterruptionPoint interruptionPoint)
    {
        // Restore working memory
        _workingMemory.Clear();
        _workingMemoryIndex.Clear();
        foreach (var item in interruptionPoint.WorkingMemorySnapshot)
        {
            _workingMemory.Enqueue(item);
            _workingMemoryIndex[item.Id] = item;
        }

        // Restore attention state
        _attentionState = new AttentionState
        {
            PrimaryFocus = interruptionPoint.AttentionStateSnapshot.PrimaryFocus,
            PeripheralAwareness = new List<AttentionItem>(
                interruptionPoint.AttentionStateSnapshot.PeripheralAwareness)
        };

        CurrentLoad = interruptionPoint.CognitiveLoadSnapshot;

        _metrics.InterruptionRestorations++;
    }

    /// <summary>
    /// Get cognitive status report
    /// </summary>
    public string GetStatusReport()
    {
        var report = new System.Text.StringBuilder();

        report.AppendLine("╔════════════════════════════════════════════════════════════╗");
        report.AppendLine("║         COGNITIVE LOAD BALANCER - STATUS REPORT           ║");
        report.AppendLine("╚════════════════════════════════════════════════════════════╝");
        report.AppendLine();
        report.AppendLine($"Cognitive Load:    {CurrentLoad} {GetLoadEmoji(CurrentLoad)}");
        report.AppendLine($"Working Memory:    {_workingMemory.Count}/{MaxWorkingMemoryCapacity} items");
        report.AppendLine($"Primary Focus:     {_attentionState.PrimaryFocus?.Description ?? "None"}");
        report.AppendLine($"Peripheral Tasks:  {_attentionState.PeripheralAwareness.Count}/{MaxPeripheralAwareness}");
        report.AppendLine();

        var intenseWorkDuration = DateTime.UtcNow - _lastIntenseWorkStart;
        report.AppendLine($"Intense Work Time: {intenseWorkDuration.TotalMinutes:F0} minutes");
        report.AppendLine($"Context Switches:  {_metrics.FrequentContextSwitches}");
        report.AppendLine($"Total Evictions:   {_metrics.TotalEvictions}");
        report.AppendLine($"Compressions:      {_metrics.CompressionCount}");
        report.AppendLine($"Overload Events:   {_metrics.OverloadCount}");
        report.AppendLine($"Breaks Taken:      {_metrics.TotalBreaksTaken} ({_metrics.TotalBreakMinutes}min total)");
        report.AppendLine();

        var breakRec = ShouldTakeBreak();
        if (breakRec.ShouldBreak)
        {
            report.AppendLine($"⚠️  BREAK RECOMMENDED: {breakRec.Reason}");
            report.AppendLine($"    Urgency: {breakRec.Urgency}");
            report.AppendLine($"    Duration: {breakRec.RecommendedDurationMinutes} minutes");
        }
        else
        {
            report.AppendLine("✅ Cognitive state: Healthy");
        }

        return report.ToString();
    }

    private string GetLoadEmoji(CognitiveLoad load) => load switch
    {
        CognitiveLoad.Low => "🟢",
        CognitiveLoad.Moderate => "🟡",
        CognitiveLoad.High => "🟠",
        CognitiveLoad.Overload => "🔴",
        _ => ""
    };
}

/// <summary>
/// Working memory item (7±2 capacity)
/// </summary>
public sealed class WorkingMemoryItem
{
    public required string Id { get; init; }
    public required string Content { get; init; }
    public required WorkingMemoryType Type { get; init; }
    public required DateTime AddedAt { get; init; }
    public DateTime LastAccessedAt { get; set; }
    public int AccessCount { get; set; }
}

/// <summary>
/// Type of working memory item
/// </summary>
public enum WorkingMemoryType
{
    File,           // File path
    Goal,           // Active goal
    Decision,       // Recent decision
    Concept,        // Abstract concept
    Context         // Contextual information
}

/// <summary>
/// Attention state (spotlight + peripheral)
/// </summary>
public sealed class AttentionState
{
    public AttentionItem? PrimaryFocus { get; set; }
    public List<AttentionItem> PeripheralAwareness { get; set; } = new();
}

/// <summary>
/// Attention item
/// </summary>
public sealed class AttentionItem
{
    public required string TaskId { get; init; }
    public required string Description { get; init; }
    public required DateTime StartedAt { get; init; }
}

/// <summary>
/// Cognitive metrics for monitoring
/// </summary>
public sealed class CognitiveMetrics
{
    public int TotalEvictions { get; set; }
    public int CompressionCount { get; set; }
    public int FrequentContextSwitches { get; set; }
    public int LoadChangeCount { get; set; }
    public DateTime? LastLoadChange { get; set; }
    public int OverloadCount { get; set; }
    public int InterruptionRestorations { get; set; }
    public int TotalBreaksTaken { get; set; }
    public int TotalBreakMinutes { get; set; }
}

/// <summary>
/// Working memory eviction result
/// </summary>
public sealed class WorkingMemoryEviction
{
    public bool EvictionOccurred { get; set; }
    public WorkingMemoryItem? EvictedItem { get; set; }
}

/// <summary>
/// Break recommendation
/// </summary>
public sealed class BreakRecommendation
{
    public bool ShouldBreak { get; set; }
    public BreakUrgency Urgency { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int RecommendedDurationMinutes { get; set; }
}

/// <summary>
/// Break urgency level
/// </summary>
public enum BreakUrgency
{
    None,
    Low,
    Medium,
    High,
    Immediate
}

/// <summary>
/// Interruption point (for context restoration)
/// </summary>
public sealed class InterruptionPoint
{
    public required DateTime Timestamp { get; init; }
    public required string Reason { get; init; }
    public required List<WorkingMemoryItem> WorkingMemorySnapshot { get; init; }
    public required AttentionState AttentionStateSnapshot { get; init; }
    public required CognitiveLoad CognitiveLoadSnapshot { get; init; }
}
