using System;
using System.Collections.Generic;
using System.Linq;

namespace Hazina.App.HazinaCoder.Core.Skills;

/// <summary>
/// Chains multiple skills together into composite workflows.
/// Enables complex automation by composing simple skills.
/// </summary>
/// <remarks>
/// Improvement #40: Skill Composition
///
/// Example:
/// worktree → code → test → pr
/// becomes a single "feature-workflow" skill
/// </remarks>
public class SkillComposer
{
    private readonly Dictionary<string, CompositeSkill> _composites = new();

    /// <summary>
    /// Create a composite skill from a chain of skills
    /// </summary>
    public CompositeSkill CreateComposite(string name, string description, List<string> skillChain)
    {
        var composite = new CompositeSkill
        {
            Name = name,
            Description = description,
            SkillChain = skillChain,
            CreatedAt = DateTime.UtcNow
        };

        _composites[name] = composite;
        Console.WriteLine($"[SkillComposer] Created composite: {name}");
        Console.WriteLine($"  Chain: {string.Join(" → ", skillChain)}");

        return composite;
    }

    /// <summary>
    /// Execute a composite skill
    /// </summary>
    public CompositeResult ExecuteComposite(string name, Dictionary<string, object>? parameters = null)
    {
        if (!_composites.TryGetValue(name, out var composite))
        {
            return new CompositeResult
            {
                Success = false,
                ErrorMessage = $"Composite skill not found: {name}"
            };
        }

        var result = new CompositeResult
        {
            CompositeName = name,
            StartTime = DateTime.UtcNow
        };

        Console.WriteLine($"[SkillComposer] Executing composite: {name}");

        foreach (var skillName in composite.SkillChain)
        {
            var stepResult = new SkillStepResult
            {
                SkillName = skillName,
                StartTime = DateTime.UtcNow
            };

            try
            {
                // TODO: Actually execute the skill
                Console.WriteLine($"  → {skillName}");
                stepResult.Success = true;
                stepResult.Output = $"Executed {skillName}";
            }
            catch (Exception ex)
            {
                stepResult.Success = false;
                stepResult.ErrorMessage = ex.Message;
                result.Success = false;
                result.ErrorMessage = $"Failed at step: {skillName}";
                Console.WriteLine($"  ❌ {skillName}: {ex.Message}");
            }

            stepResult.EndTime = DateTime.UtcNow;
            stepResult.Duration = stepResult.EndTime - stepResult.StartTime;
            result.Steps.Add(stepResult);

            if (!stepResult.Success && !composite.ContinueOnError)
            {
                break;
            }
        }

        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;

        return result;
    }

    /// <summary>
    /// Get all composite skills
    /// </summary>
    public IReadOnlyDictionary<string, CompositeSkill> GetComposites() => _composites;

    /// <summary>
    /// Initialize default composite skills
    /// </summary>
    public void InitializeDefaults()
    {
        // Feature workflow
        CreateComposite(
            "feature-workflow",
            "Complete feature development workflow",
            new() { "allocate-worktree", "implement-feature", "run-tests", "create-pr", "release-worktree" }
        );

        // Quick fix workflow
        CreateComposite(
            "quick-fix",
            "Fast bugfix workflow",
            new() { "analyze-error", "apply-fix", "verify-fix", "commit" }
        );

        // Refactor workflow
        CreateComposite(
            "refactor-workflow",
            "Safe refactoring with tests",
            new() { "analyze-code", "plan-refactor", "apply-changes", "run-tests", "review-diff" }
        );

        Console.WriteLine($"[SkillComposer] Initialized {_composites.Count} default composites");
    }
}

/// <summary>
/// Composite skill definition
/// </summary>
public class CompositeSkill
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> SkillChain { get; set; } = new();
    public bool ContinueOnError { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Result of composite skill execution
/// </summary>
public class CompositeResult
{
    public string CompositeName { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public List<SkillStepResult> Steps { get; set; } = new();
}

/// <summary>
/// Result of a single skill step
/// </summary>
public class SkillStepResult
{
    public string SkillName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string Output { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
}
