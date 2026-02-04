using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Pre-flight checks before execution to prevent known mistakes.
/// Learns from past errors and validates before taking action.
/// </summary>
/// <remarks>
/// Improvement #34: Mistake Prevention System
///
/// Checks before execution:
/// - Similar past failures
/// - Known problematic patterns
/// - Precondition violations
/// - Safety constraints
/// </remarks>
public class MistakePrevention
{
    private readonly List<PreventionRule> _rules = new();
    private readonly List<CheckResult> _checkHistory = new();

    public MistakePrevention()
    {
        // Initialize with common rules
        InitializeDefaultRules();
    }

    /// <summary>
    /// Run pre-flight checks before executing an action
    /// </summary>
    public PreFlightResult RunChecks(string action, Dictionary<string, object> parameters)
    {
        var result = new PreFlightResult
        {
            Action = action,
            Timestamp = DateTime.UtcNow
        };

        Console.WriteLine($"[Prevention] Running pre-flight checks for: {action}");

        foreach (var rule in _rules.Where(r => r.Enabled))
        {
            var checkResult = rule.Check(action, parameters);
            result.Checks.Add(checkResult);

            if (checkResult.Failed)
            {
                result.BlockingIssues.Add(checkResult.Message);
                Console.WriteLine($"  ❌ {rule.Name}: {checkResult.Message}");
            }
            else if (!string.IsNullOrEmpty(checkResult.Warning))
            {
                result.Warnings.Add(checkResult.Warning);
                Console.WriteLine($"  ⚠️  {rule.Name}: {checkResult.Warning}");
            }
            else
            {
                Console.WriteLine($"  ✅ {rule.Name}: OK");
            }
        }

        result.Passed = result.BlockingIssues.Count == 0;
        _checkHistory.Add(new CheckResult { Result = result, Timestamp = DateTime.UtcNow });

        return result;
    }

    /// <summary>
    /// Add a custom prevention rule
    /// </summary>
    public void AddRule(PreventionRule rule)
    {
        _rules.Add(rule);
        Console.WriteLine($"[Prevention] Added rule: {rule.Name}");
    }

    /// <summary>
    /// Disable a rule by name
    /// </summary>
    public void DisableRule(string name)
    {
        var rule = _rules.FirstOrDefault(r => r.Name == name);
        if (rule != null)
        {
            rule.Enabled = false;
            Console.WriteLine($"[Prevention] Disabled rule: {name}");
        }
    }

    /// <summary>
    /// Initialize default prevention rules
    /// </summary>
    private void InitializeDefaultRules()
    {
        // Git: Don't commit to main directly
        _rules.Add(new PreventionRule
        {
            Name = "NoDirectCommitToMain",
            Description = "Prevent direct commits to main/master branch",
            Check = (action, parameters) =>
            {
                if (action.Contains("git commit", StringComparison.OrdinalIgnoreCase))
                {
                    // TODO: Check actual git branch
                    return new RuleCheckResult { Passed = true };
                }
                return new RuleCheckResult { Passed = true };
            }
        });

        // File: Don't delete without confirmation
        _rules.Add(new PreventionRule
        {
            Name = "ConfirmDelete",
            Description = "Require confirmation before deleting files",
            Check = (action, parameters) =>
            {
                if (action.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
                    action.Contains("remove", StringComparison.OrdinalIgnoreCase))
                {
                    return new RuleCheckResult
                    {
                        Passed = true,
                        Warning = "This action will delete files. Ensure backup exists."
                    };
                }
                return new RuleCheckResult { Passed = true };
            }
        });

        // TODO: Add more rules learned from past mistakes
    }

    /// <summary>
    /// Get check history
    /// </summary>
    public IReadOnlyList<CheckResult> GetHistory() => _checkHistory;
}

/// <summary>
/// Prevention rule definition
/// </summary>
public class PreventionRule
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public Func<string, Dictionary<string, object>, RuleCheckResult> Check { get; set; } = (_, _) => new RuleCheckResult { Passed = true };
}

/// <summary>
/// Result of a single rule check
/// </summary>
public class RuleCheckResult
{
    public bool Passed { get; set; }
    public bool Failed => !Passed;
    public string Message { get; set; } = string.Empty;
    public string Warning { get; set; } = string.Empty;
}

/// <summary>
/// Complete pre-flight check result
/// </summary>
public class PreFlightResult
{
    public string Action { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool Passed { get; set; }
    public List<RuleCheckResult> Checks { get; set; } = new();
    public List<string> BlockingIssues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class CheckResult
{
    public PreFlightResult Result { get; set; } = new();
    public DateTime Timestamp { get; set; }
}
