using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Monitors what the agent pays attention to during execution.
/// Tracks focus patterns, context switches, and attention drift.
/// </summary>
/// <remarks>
/// Improvement #26: Attention Monitor
///
/// Helps detect:
/// - When agent loses focus on the main goal
/// - Excessive context switching
/// - Attention to irrelevant details
/// - Missing critical information
/// </remarks>
public class AttentionMonitor
{
    private readonly List<AttentionEvent> _events = new();
    private readonly Stack<string> _focusStack = new();
    private string? _currentFocus;
    private DateTime _focusStartTime;

    /// <summary>
    /// Record when attention shifts to a new focus
    /// </summary>
    public void ShiftFocus(string newFocus, string reason = "")
    {
        if (_currentFocus != null)
        {
            // Record duration of previous focus
            _events.Add(new AttentionEvent
            {
                Focus = _currentFocus,
                StartTime = _focusStartTime,
                EndTime = DateTime.UtcNow,
                Reason = reason,
                EventType = AttentionEventType.FocusEnd
            });
        }

        _currentFocus = newFocus;
        _focusStartTime = DateTime.UtcNow;

        _events.Add(new AttentionEvent
        {
            Focus = newFocus,
            StartTime = _focusStartTime,
            Reason = reason,
            EventType = AttentionEventType.FocusStart
        });

        Console.WriteLine($"[Attention] Focus → {newFocus} ({reason})");
    }

    /// <summary>
    /// Push current focus onto stack and start new focus (for nested attention)
    /// </summary>
    public void PushFocus(string newFocus, string reason = "")
    {
        if (_currentFocus != null)
        {
            _focusStack.Push(_currentFocus);
        }
        ShiftFocus(newFocus, reason);
    }

    /// <summary>
    /// Pop focus stack and return to previous focus
    /// </summary>
    public void PopFocus(string reason = "")
    {
        if (_focusStack.Count > 0)
        {
            var previousFocus = _focusStack.Pop();
            ShiftFocus(previousFocus, $"Returning from nested focus: {reason}");
        }
    }

    /// <summary>
    /// Get all attention events
    /// </summary>
    public IReadOnlyList<AttentionEvent> GetEvents() => _events;

    /// <summary>
    /// Detect if agent has drifted from original goal
    /// </summary>
    public bool DetectDrift(string originalGoal, int maxContextSwitches = 10)
    {
        // Count context switches in last N events
        var recentEvents = _events.TakeLast(20).ToList();
        var switches = recentEvents.Count(e => e.EventType == AttentionEventType.FocusStart);

        if (switches > maxContextSwitches)
        {
            Console.WriteLine($"[Attention] DRIFT DETECTED: {switches} context switches (max {maxContextSwitches})");
            return true;
        }

        // TODO: Use LLM to detect semantic drift from original goal
        return false;
    }

    /// <summary>
    /// Get attention statistics
    /// </summary>
    public AttentionStats GetStats()
    {
        var stats = new AttentionStats
        {
            TotalEvents = _events.Count,
            TotalContextSwitches = _events.Count(e => e.EventType == AttentionEventType.FocusStart),
            CurrentFocus = _currentFocus ?? "None"
        };

        // Calculate time spent per focus area
        var focusDurations = new Dictionary<string, TimeSpan>();
        foreach (var evt in _events.Where(e => e.EventType == AttentionEventType.FocusEnd))
        {
            var duration = evt.EndTime - evt.StartTime;
            if (!focusDurations.ContainsKey(evt.Focus))
                focusDurations[evt.Focus] = TimeSpan.Zero;
            focusDurations[evt.Focus] += duration;
        }

        stats.TopFocusAreas = focusDurations
            .OrderByDescending(kvp => kvp.Value)
            .Take(5)
            .Select(kvp => new FocusArea
            {
                Name = kvp.Key,
                TotalTime = kvp.Value
            })
            .ToList();

        return stats;
    }
}

/// <summary>
/// Single attention event
/// </summary>
public class AttentionEvent
{
    public string Focus { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AttentionEventType EventType { get; set; }
}

public enum AttentionEventType
{
    FocusStart,
    FocusEnd
}

/// <summary>
/// Attention statistics
/// </summary>
public class AttentionStats
{
    public int TotalEvents { get; set; }
    public int TotalContextSwitches { get; set; }
    public string CurrentFocus { get; set; } = string.Empty;
    public List<FocusArea> TopFocusAreas { get; set; } = new();
}

public class FocusArea
{
    public string Name { get; set; } = string.Empty;
    public TimeSpan TotalTime { get; set; }
}
