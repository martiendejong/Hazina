using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Detects when agent actions diverge from the stated goal.
/// Monitors action sequences and flags potential drift.
/// </summary>
/// <remarks>
/// Improvement #27: Goal Drift Detector
///
/// Prevents:
/// - Working on unrelated tasks
/// - Over-engineering simple requests
/// - Scope creep during implementation
/// - Losing track of original user intent
/// </remarks>
public class GoalDriftDetector
{
    private readonly List<ActionRecord> _actions = new();
    private string _originalGoal = string.Empty;
    private string _currentPhase = "initialization";
    private int _unrelatedActionCount = 0;

    /// <summary>
    /// Set the original goal at session start
    /// </summary>
    public void SetGoal(string goal)
    {
        _originalGoal = goal;
        _actions.Clear();
        _unrelatedActionCount = 0;
        _currentPhase = "initialization";

        Console.WriteLine($"[GoalDrift] Goal set: {goal}");
    }

    /// <summary>
    /// Record an action taken by the agent
    /// </summary>
    public void RecordAction(string action, string reasoning = "")
    {
        var record = new ActionRecord
        {
            Action = action,
            Reasoning = reasoning,
            Timestamp = DateTime.UtcNow,
            Phase = _currentPhase
        };

        _actions.Add(record);

        // TODO: Use LLM to assess if action relates to goal
        // For now, simple heuristic
        var isRelated = AssessRelevance(action);
        record.IsRelatedToGoal = isRelated;

        if (!isRelated)
        {
            _unrelatedActionCount++;
            Console.WriteLine($"[GoalDrift] UNRELATED ACTION: {action}");

            if (_unrelatedActionCount >= 3)
            {
                Console.WriteLine($"[GoalDrift] WARNING: {_unrelatedActionCount} unrelated actions detected!");
                Console.WriteLine($"[GoalDrift] Original goal: {_originalGoal}");
            }
        }
    }

    /// <summary>
    /// Set current execution phase
    /// </summary>
    public void SetPhase(string phase)
    {
        _currentPhase = phase;
        Console.WriteLine($"[GoalDrift] Phase → {phase}");
    }

    /// <summary>
    /// Check if significant drift has occurred
    /// </summary>
    public DriftAssessment AssessDrift()
    {
        var assessment = new DriftAssessment
        {
            TotalActions = _actions.Count,
            UnrelatedActions = _unrelatedActionCount,
            DriftScore = CalculateDriftScore(),
            CurrentPhase = _currentPhase,
            OriginalGoal = _originalGoal
        };

        // Drift levels
        if (assessment.DriftScore < 0.2)
            assessment.Level = DriftLevel.None;
        else if (assessment.DriftScore < 0.4)
            assessment.Level = DriftLevel.Minor;
        else if (assessment.DriftScore < 0.6)
            assessment.Level = DriftLevel.Moderate;
        else
            assessment.Level = DriftLevel.Severe;

        return assessment;
    }

    /// <summary>
    /// Calculate drift score (0.0 = perfect, 1.0 = complete drift)
    /// </summary>
    private double CalculateDriftScore()
    {
        if (_actions.Count == 0)
            return 0.0;

        // Simple formula: unrelated actions / total actions
        var score = (double)_unrelatedActionCount / _actions.Count;

        // TODO: More sophisticated scoring:
        // - Weight recent actions more heavily
        // - Consider phase transitions
        // - Use LLM for semantic similarity to goal

        return Math.Min(1.0, score);
    }

    /// <summary>
    /// Simple heuristic to assess if action relates to goal
    /// </summary>
    private bool AssessRelevance(string action)
    {
        // TODO: Use LLM for semantic assessment
        // For now, always return true (no false positives)
        return true;
    }

    /// <summary>
    /// Get action history
    /// </summary>
    public IReadOnlyList<ActionRecord> GetActions() => _actions;
}

/// <summary>
/// Record of a single action
/// </summary>
public class ActionRecord
{
    public string Action { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Phase { get; set; } = string.Empty;
    public bool IsRelatedToGoal { get; set; } = true;
}

/// <summary>
/// Assessment of goal drift
/// </summary>
public class DriftAssessment
{
    public int TotalActions { get; set; }
    public int UnrelatedActions { get; set; }
    public double DriftScore { get; set; }
    public DriftLevel Level { get; set; }
    public string CurrentPhase { get; set; } = string.Empty;
    public string OriginalGoal { get; set; } = string.Empty;
}

public enum DriftLevel
{
    None,
    Minor,
    Moderate,
    Severe
}
