using System;
using System.Collections.Generic;
using Hazina.App.HazinaCoder.Core.Identity;

namespace Hazina.App.HazinaCoder.Core.State;

/// <summary>
/// State manager - tracks current session context, goals, attention
/// Real-time state that persists across sessions
/// </summary>
public class StateManager
{
    // Session metadata
    public DateTime StartTime { get; set; } = DateTime.Now;
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string Environment { get; set; } = "development";
    public WorkflowMode Mode { get; set; } = WorkflowMode.FeatureDevelopment;

    // Active goals
    public List<Goal> ActiveGoals { get; set; } = new();
    public Goal? PrimaryGoal { get; set; }

    // Working memory snapshot
    public List<string> RecentFiles { get; set; } = new();
    public List<Decision> RecentDecisions { get; set; } = new();

    // Attention management
    public AttentionFocus CurrentFocus { get; set; } = new();
    public CognitiveLoad Load { get; set; } = CognitiveLoad.Moderate;

    // Context preservation
    public Dictionary<string, object> CustomState { get; set; } = new();

    /// <summary>
    /// Add new goal to active list
    /// </summary>
    public void AddGoal(Goal goal)
    {
        ActiveGoals.Add(goal);
        if (PrimaryGoal == null)
            PrimaryGoal = goal;
    }

    /// <summary>
    /// Complete a goal
    /// </summary>
    public void CompleteGoal(string goalId)
    {
        var goal = ActiveGoals.Find(g => g.Id == goalId);
        if (goal != null)
        {
            goal.Status = GoalStatus.Completed;
        }

        // If completed primary goal, set next as primary
        if (PrimaryGoal?.Id == goalId)
        {
            PrimaryGoal = ActiveGoals.FirstOrDefault(g => g.Status == GoalStatus.InProgress);
        }
    }

    /// <summary>
    /// Get all in-progress goals
    /// </summary>
    public List<Goal> GetActiveGoals()
    {
        return ActiveGoals.Where(g => g.Status == GoalStatus.InProgress).ToList();
    }

    /// <summary>
    /// Set current attention focus
    /// </summary>
    public void SetFocus(string focusArea, int priorityPercent)
    {
        CurrentFocus.Area = focusArea;
        CurrentFocus.PriorityPercent = priorityPercent;
    }

    /// <summary>
    /// Assess cognitive load
    /// </summary>
    public CognitiveLoad AssessLoad()
    {
        var activeCount = ActiveGoals.Count(g => g.Status == GoalStatus.InProgress);
        var recentDecisions = RecentDecisions.Count(d => d.Timestamp > DateTime.Now.AddMinutes(-10));

        return (activeCount, recentDecisions) switch
        {
            ( <= 1, <= 3) => CognitiveLoad.Low,
            ( <= 3, <= 7) => CognitiveLoad.Moderate,
            ( <= 5, <= 12) => CognitiveLoad.High,
            _ => CognitiveLoad.Overload
        };
    }
}

public enum WorkflowMode
{
    FeatureDevelopment,  // New features → use worktrees
    ActiveDebugging      // User debugging → work in base repo
}

/// <summary>
/// Attention focus - what matters right now
/// </summary>
public class AttentionFocus
{
    public string Area { get; set; } = string.Empty;
    public int PriorityPercent { get; set; } = 100;
    public DateTime FocusStarted { get; set; } = DateTime.Now;
}

/// <summary>
/// Cognitive load assessment
/// </summary>
public enum CognitiveLoad
{
    Low,        // Can explore, optimize, learn
    Moderate,   // Optimal execution zone
    High,       // Simplify and focus
    Overload    // Stop and reassess
}
