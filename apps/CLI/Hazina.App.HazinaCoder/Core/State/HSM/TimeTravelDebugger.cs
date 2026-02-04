using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.State.HSM;

/// <summary>
/// Time-travel debugger - jump to any past state, explore alternate timelines
/// Enables: undo/redo, "what if" analysis, branching timelines
/// </summary>
public sealed class TimeTravelDebugger
{
    private readonly EventSourcedLedger _ledger;
    private readonly Dictionary<string, Timeline> _timelines = new();
    private string _currentTimeline = "main";

    public TimeTravelDebugger(EventSourcedLedger ledger)
    {
        _ledger = ledger;
        _timelines["main"] = new Timeline { Name = "main", BranchPoint = null };
    }

    /// <summary>
    /// Jump to state at specific timestamp (time-travel)
    /// </summary>
    public async Task<ImmutableStateSnapshot?> JumpToTimestampAsync(DateTime targetTime)
    {
        var state = _ledger.GetStateAt(targetTime);
        if (state == null)
        {
            throw new InvalidOperationException(
                $"No state found at {targetTime}. Valid range: " +
                $"{_ledger.GetStats().OldestState} - {_ledger.GetStats().NewestState}");
        }

        return state;
    }

    /// <summary>
    /// Jump to specific state by ID
    /// </summary>
    public ImmutableStateSnapshot? JumpToState(StateId stateId)
    {
        return _ledger.GetState(stateId);
    }

    /// <summary>
    /// Jump back N states from current
    /// </summary>
    public ImmutableStateSnapshot? JumpBack(int stepsBack, ImmutableStateSnapshot currentState)
    {
        var chain = _ledger.GetCausalChain(currentState.Id);
        var targetIndex = Math.Max(0, chain.Count - stepsBack - 1);
        return chain.ElementAtOrDefault(targetIndex);
    }

    /// <summary>
    /// Undo last N minutes of work
    /// </summary>
    public ImmutableStateSnapshot? UndoLastMinutes(int minutes)
    {
        var targetTime = DateTime.UtcNow.AddMinutes(-minutes);
        return _ledger.GetStateAt(targetTime);
    }

    /// <summary>
    /// Undo last N hours of work
    /// </summary>
    public ImmutableStateSnapshot? UndoLastHours(int hours)
    {
        return UndoLastMinutes(hours * 60);
    }

    /// <summary>
    /// Create alternate timeline by branching from past state
    /// Enables "what if" exploration without affecting main timeline
    /// </summary>
    public async Task<(string TimelineName, ImmutableStateSnapshot BranchState)> BranchFromAsync(
        StateId branchPointStateId,
        string timelineName)
    {
        if (_timelines.ContainsKey(timelineName))
        {
            throw new InvalidOperationException($"Timeline '{timelineName}' already exists");
        }

        var branchState = _ledger.GetState(branchPointStateId);
        if (branchState == null)
        {
            throw new InvalidOperationException($"State {branchPointStateId} not found");
        }

        _timelines[timelineName] = new Timeline
        {
            Name = timelineName,
            BranchPoint = branchPointStateId,
            CreatedAt = DateTime.UtcNow
        };

        return (timelineName, branchState);
    }

    /// <summary>
    /// Switch to different timeline
    /// </summary>
    public void SwitchTimeline(string timelineName)
    {
        if (!_timelines.ContainsKey(timelineName))
        {
            throw new InvalidOperationException($"Timeline '{timelineName}' does not exist");
        }

        _currentTimeline = timelineName;
    }

    /// <summary>
    /// List all available timelines
    /// </summary>
    public IEnumerable<Timeline> GetTimelines()
    {
        return _timelines.Values.OrderBy(t => t.CreatedAt);
    }

    /// <summary>
    /// Replay events with modifications (creates new timeline automatically)
    /// Example: "What if I had used Opus instead of Haiku?"
    /// </summary>
    public async Task<WhatIfResult> WhatIfAsync(
        StateId fromStateId,
        string hypothesisName,
        Func<StateEvent, StateEvent?> eventModifier)
    {
        var timelineName = $"whatif-{hypothesisName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";

        // Create branch
        await BranchFromAsync(fromStateId, timelineName);
        SwitchTimeline(timelineName);

        // Replay with modifications
        var resultState = await _ledger.ReplayWithModificationsAsync(fromStateId, eventModifier);

        // Analyze differences
        var originalState = _ledger.GetState(fromStateId);
        var differences = AnalyzeDifferences(originalState!, resultState);

        return new WhatIfResult
        {
            HypothesisName = hypothesisName,
            TimelineName = timelineName,
            OriginalState = originalState!,
            ModifiedState = resultState,
            Differences = differences
        };
    }

    /// <summary>
    /// Find states similar to current state (time-travel search)
    /// Useful for: "When did I last face a similar situation?"
    /// </summary>
    public IEnumerable<SimilarStateMatch> FindSimilarStates(
        ImmutableStateSnapshot referenceState,
        int topK = 5)
    {
        var allStates = _ledger.GetHistory().ToList();
        var matches = new List<SimilarStateMatch>();

        foreach (var state in allStates)
        {
            if (state.Id == referenceState.Id)
                continue; // Skip self

            var similarity = CalculateSimilarity(referenceState, state);
            matches.Add(new SimilarStateMatch
            {
                State = state,
                SimilarityScore = similarity,
                Differences = AnalyzeDifferences(referenceState, state)
            });
        }

        return matches
            .OrderByDescending(m => m.SimilarityScore)
            .Take(topK);
    }

    /// <summary>
    /// Get timeline visualization (ASCII art)
    /// </summary>
    public string VisualizeTimelines()
    {
        var viz = new System.Text.StringBuilder();
        viz.AppendLine("=== TIMELINE VISUALIZATION ===\n");

        viz.AppendLine("main:  o---o---o---o---o  (current)");

        foreach (var timeline in _timelines.Values.Where(t => t.Name != "main"))
        {
            viz.AppendLine($"         \\");
            viz.AppendLine($"          \\---o---o  {timeline.Name} (branch)");
        }

        return viz.ToString();
    }

    /// <summary>
    /// Calculate similarity between two states (0.0 - 1.0)
    /// </summary>
    private double CalculateSimilarity(ImmutableStateSnapshot a, ImmutableStateSnapshot b)
    {
        double score = 0.0;
        int factors = 0;

        // Compare mode
        if (a.Mode == b.Mode) score += 0.2;
        factors++;

        // Compare cognitive load
        if (a.Load == b.Load) score += 0.2;
        factors++;

        // Compare number of goals
        var goalDiff = Math.Abs(a.Goals.Count - b.Goals.Count);
        score += (1.0 - (goalDiff / 10.0)) * 0.2; // Max 10 goals diff
        factors++;

        // Compare focus area (string similarity)
        var focusSimilarity = StringSimilarity(a.Focus.Area, b.Focus.Area);
        score += focusSimilarity * 0.2;
        factors++;

        // Compare working memory overlap
        var memoryOverlap = a.WorkingMemory.Intersect(b.WorkingMemory).Count()
            / (double)Math.Max(a.WorkingMemory.Count, 1);
        score += memoryOverlap * 0.2;
        factors++;

        return score;
    }

    /// <summary>
    /// Simple string similarity (Levenshtein-based)
    /// </summary>
    private double StringSimilarity(string a, string b)
    {
        if (string.IsNullOrEmpty(a) && string.IsNullOrEmpty(b))
            return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
            return 0.0;

        var maxLen = Math.Max(a.Length, b.Length);
        var distance = LevenshteinDistance(a, b);
        return 1.0 - (distance / (double)maxLen);
    }

    /// <summary>
    /// Levenshtein distance algorithm
    /// </summary>
    private int LevenshteinDistance(string a, string b)
    {
        var m = a.Length;
        var n = b.Length;
        var d = new int[m + 1, n + 1];

        for (int i = 0; i <= m; i++) d[i, 0] = i;
        for (int j = 0; j <= n; j++) d[0, j] = j;

        for (int j = 1; j <= n; j++)
        {
            for (int i = 1; i <= m; i++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(
                    d[i - 1, j] + 1,    // deletion
                    d[i, j - 1] + 1),   // insertion
                    d[i - 1, j - 1] + cost); // substitution
            }
        }

        return d[m, n];
    }

    /// <summary>
    /// Analyze differences between two states
    /// </summary>
    private List<StateDifference> AnalyzeDifferences(
        ImmutableStateSnapshot a,
        ImmutableStateSnapshot b)
    {
        var diffs = new List<StateDifference>();

        if (a.Mode != b.Mode)
            diffs.Add(new StateDifference("Mode", a.Mode.ToString(), b.Mode.ToString()));

        if (a.Load != b.Load)
            diffs.Add(new StateDifference("CognitiveLoad", a.Load.ToString(), b.Load.ToString()));

        if (a.Goals.Count != b.Goals.Count)
            diffs.Add(new StateDifference("GoalCount", a.Goals.Count.ToString(), b.Goals.Count.ToString()));

        if (a.Focus.Area != b.Focus.Area)
            diffs.Add(new StateDifference("FocusArea", a.Focus.Area, b.Focus.Area));

        return diffs;
    }
}

/// <summary>
/// Represents an alternate timeline (parallel universe)
/// </summary>
public sealed record Timeline
{
    public required string Name { get; init; }
    public StateId? BranchPoint { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Result of "what if" analysis
/// </summary>
public sealed record WhatIfResult
{
    public required string HypothesisName { get; init; }
    public required string TimelineName { get; init; }
    public required ImmutableStateSnapshot OriginalState { get; init; }
    public required ImmutableStateSnapshot ModifiedState { get; init; }
    public required List<StateDifference> Differences { get; init; }
}

/// <summary>
/// Similar state match from time-travel search
/// </summary>
public sealed record SimilarStateMatch
{
    public required ImmutableStateSnapshot State { get; init; }
    public required double SimilarityScore { get; init; }
    public required List<StateDifference> Differences { get; init; }
}

/// <summary>
/// Difference between two states
/// </summary>
public sealed record StateDifference(string Property, string OldValue, string NewValue)
{
    public override string ToString() => $"{Property}: {OldValue} → {NewValue}";
}
