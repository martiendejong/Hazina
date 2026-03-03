using System;
using System.Collections.Generic;

namespace Hazina.App.HazinaCoder.Core.Session;

/// <summary>
/// Predefined session templates for common workflows.
/// Provides starting context, tools, and guidance for specific tasks.
/// </summary>
/// <remarks>
/// Improvement #35: Session Templates
///
/// Templates:
/// - Debugging Session
/// - Feature Development
/// - Refactoring
/// - Code Review
/// - Performance Investigation
/// - Bug Triage
/// </remarks>
public class SessionTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> PreloadedTools { get; set; } = new();
    public List<string> SuggestedSkills { get; set; } = new();
    public string InitialPrompt { get; set; } = string.Empty;
    public Dictionary<string, string> Configuration { get; set; } = new();
}

/// <summary>
/// Manages session templates
/// </summary>
public class SessionTemplateManager
{
    private readonly Dictionary<string, SessionTemplate> _templates = new();

    public SessionTemplateManager()
    {
        InitializeDefaultTemplates();
    }

    /// <summary>
    /// Initialize built-in templates
    /// </summary>
    private void InitializeDefaultTemplates()
    {
        // Debugging template
        _templates["debugging"] = new SessionTemplate
        {
            Name = "Active Debugging",
            Description = "Fast turnaround debugging session with live feedback",
            PreloadedTools = new() { "build", "test", "debugger-attach" },
            SuggestedSkills = new() { "error-analysis", "log-reading", "quick-fix" },
            InitialPrompt = "I'm ready to help debug. What's the issue?",
            Configuration = new()
            {
                { "mode", "debug" },
                { "worktree", "false" },
                { "auto-build", "true" }
            }
        };

        // Feature development template
        _templates["feature"] = new SessionTemplate
        {
            Name = "Feature Development",
            Description = "Isolated worktree development with full workflow",
            PreloadedTools = new() { "worktree", "git", "test", "code-review" },
            SuggestedSkills = new() { "allocate-worktree", "pr-workflow", "testing" },
            InitialPrompt = "Let's build this feature. What are we implementing?",
            Configuration = new()
            {
                { "mode", "feature" },
                { "worktree", "true" },
                { "auto-pr", "true" }
            }
        };

        // Refactoring template
        _templates["refactor"] = new SessionTemplate
        {
            Name = "Code Refactoring",
            Description = "Safe refactoring with comprehensive testing",
            PreloadedTools = new() { "refactor", "test", "coverage", "static-analysis" },
            SuggestedSkills = new() { "safe-refactor", "test-coverage", "code-smell-detection" },
            InitialPrompt = "Ready to refactor. What needs improvement?",
            Configuration = new()
            {
                { "mode", "refactor" },
                { "safety-checks", "true" },
                { "require-tests", "true" }
            }
        };

        // Code review template
        _templates["review"] = new SessionTemplate
        {
            Name = "Code Review",
            Description = "Thorough PR review with best practices",
            PreloadedTools = new() { "pr-fetch", "diff-analysis", "security-scan" },
            SuggestedSkills = new() { "review-checklist", "security-review", "style-check" },
            InitialPrompt = "I'll review this PR. What's the PR number?",
            Configuration = new()
            {
                { "mode", "review" },
                { "verbose", "true" },
                { "checklist", "true" }
            }
        };

        Console.WriteLine($"[Templates] Initialized {_templates.Count} session templates");
    }

    /// <summary>
    /// Get a template by name
    /// </summary>
    public SessionTemplate? GetTemplate(string name)
    {
        return _templates.TryGetValue(name.ToLower(), out var template) ? template : null;
    }

    /// <summary>
    /// Get all available templates
    /// </summary>
    public IReadOnlyDictionary<string, SessionTemplate> GetAllTemplates() => _templates;

    /// <summary>
    /// Apply a template to current session
    /// </summary>
    public void ApplyTemplate(string name)
    {
        var template = GetTemplate(name);
        if (template == null)
        {
            Console.WriteLine($"[Templates] Template not found: {name}");
            return;
        }

        Console.WriteLine($"[Templates] Applying template: {template.Name}");
        Console.WriteLine($"  {template.Description}");
        Console.WriteLine($"  Tools: {string.Join(", ", template.PreloadedTools)}");
        Console.WriteLine($"  Skills: {string.Join(", ", template.SuggestedSkills)}");

        // TODO: Actually configure session with template settings
    }
}
