using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Tools;

/// <summary>
/// Provides dry-run capability for all tools.
/// Simulates tool execution without making actual changes.
/// </summary>
/// <remarks>
/// Improvement #23: Tool Dry-Run Mode
///
/// Usage: hazinacoder run-tool --tool git-commit --dry-run
/// Output: Shows what would happen without executing
/// </remarks>
public class ToolDryRunner
{
    private readonly List<DryRunAction> _plannedActions = new();

    /// <summary>
    /// Record an action that would be performed
    /// </summary>
    public void RecordAction(string toolName, string action, Dictionary<string, object>? parameters = null)
    {
        _plannedActions.Add(new DryRunAction
        {
            ToolName = toolName,
            Action = action,
            Parameters = parameters ?? new(),
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get all planned actions
    /// </summary>
    public IReadOnlyList<DryRunAction> GetPlannedActions() => _plannedActions;

    /// <summary>
    /// Display dry-run results to console
    /// </summary>
    public void DisplayResults()
    {
        Console.WriteLine("\n[DRY RUN] The following actions would be performed:");
        Console.WriteLine("─────────────────────────────────────────────────\n");

        for (int i = 0; i < _plannedActions.Count; i++)
        {
            var action = _plannedActions[i];
            Console.WriteLine($"{i + 1}. [{action.ToolName}] {action.Action}");

            if (action.Parameters.Count > 0)
            {
                Console.WriteLine($"   Parameters: {JsonSerializer.Serialize(action.Parameters, new JsonSerializerOptions { WriteIndented = true })}");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"Total: {_plannedActions.Count} action(s)");
        Console.WriteLine("\nNo changes were made (dry-run mode).");
    }

    /// <summary>
    /// Clear recorded actions
    /// </summary>
    public void Clear() => _plannedActions.Clear();
}

/// <summary>
/// Represents a single action in a dry run
/// </summary>
public class DryRunAction
{
    public string ToolName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
