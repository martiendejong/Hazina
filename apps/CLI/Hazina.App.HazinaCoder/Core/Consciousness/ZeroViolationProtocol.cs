using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.Consciousness;

/// <summary>
/// Zero-Violation Quality Protocol - Production-grade reliability rules.
/// Violations are CRITICAL FAILURES that must be prevented.
///
/// Improvement #23: Zero-Violation Protocol (Value: 9/10, Effort: 5/10, Ratio: 1.80)
///
/// Inspired by C:\scripts system zero-tolerance rules:
/// - NEVER edit code in base repos during feature development
/// - ALWAYS allocate worktrees for feature work
/// - ALWAYS release worktrees immediately after PR creation
/// - ALWAYS update reflection.log.md after every session
/// - ZERO regressions - all existing features must work
/// </summary>
public class ZeroViolationProtocol
{
    private readonly string _logPath;
    private readonly List<ViolationRule> _rules;
    private readonly List<ViolationEvent> _violations;

    public ZeroViolationProtocol(string basePath)
    {
        var logDir = Path.Combine(basePath, "data", "violations");
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        _logPath = Path.Combine(logDir, "violations.log.md");
        _rules = InitializeRules();
        _violations = new List<ViolationEvent>();
    }

    /// <summary>
    /// Initialize zero-tolerance rules
    /// </summary>
    private List<ViolationRule> InitializeRules()
    {
        return new List<ViolationRule>
        {
            // Worktree management
            new ViolationRule
            {
                RuleId = "WORKTREE-001",
                Category = "Worktree Management",
                Description = "NEVER edit code in base repository during feature development",
                Severity = ViolationSeverity.Critical,
                CheckFunction = (context) =>
                {
                    // Check if editing code in C:\Projects\<repo> instead of worktree
                    if (context.ContainsKey("filePath"))
                    {
                        var filePath = context["filePath"].ToString()!;
                        if (filePath.Contains(@"\Projects\") &&
                            !filePath.Contains(@"\worker-agents\") &&
                            (filePath.EndsWith(".cs") || filePath.EndsWith(".ts") || filePath.EndsWith(".tsx")))
                        {
                            return new ViolationResult
                            {
                                IsViolation = true,
                                Message = $"Attempted to edit code in base repo: {filePath}. MUST use allocated worktree."
                            };
                        }
                    }
                    return ViolationResult.NoViolation;
                }
            },

            new ViolationRule
            {
                RuleId = "WORKTREE-002",
                Category = "Worktree Management",
                Description = "ALWAYS release worktree immediately after PR creation",
                Severity = ViolationSeverity.Critical,
                CheckFunction = (context) =>
                {
                    // Check if PR created but worktree not released
                    if (context.ContainsKey("prCreated") && context.ContainsKey("worktreeReleased"))
                    {
                        var prCreated = (bool)context["prCreated"];
                        var worktreeReleased = (bool)context["worktreeReleased"];

                        if (prCreated && !worktreeReleased)
                        {
                            return new ViolationResult
                            {
                                IsViolation = true,
                                Message = "PR created but worktree NOT released. MUST release immediately."
                            };
                        }
                    }
                    return ViolationResult.NoViolation;
                }
            },

            // Session management
            new ViolationRule
            {
                RuleId = "SESSION-001",
                Category = "Session Management",
                Description = "ALWAYS update reflection.log.md at end of session",
                Severity = ViolationSeverity.High,
                CheckFunction = (context) =>
                {
                    // Check if session ending without reflection update
                    if (context.ContainsKey("sessionEnding") && context.ContainsKey("reflectionUpdated"))
                    {
                        var sessionEnding = (bool)context["sessionEnding"];
                        var reflectionUpdated = (bool)context["reflectionUpdated"];

                        if (sessionEnding && !reflectionUpdated)
                        {
                            return new ViolationResult
                            {
                                IsViolation = true,
                                Message = "Session ending without reflection log update. MANDATORY learning step."
                            };
                        }
                    }
                    return ViolationResult.NoViolation;
                }
            },

            // Code quality
            new ViolationRule
            {
                RuleId = "QUALITY-001",
                Category = "Code Quality",
                Description = "ZERO regressions - all existing tests must pass",
                Severity = ViolationSeverity.Critical,
                CheckFunction = (context) =>
                {
                    // Check if tests are failing
                    if (context.ContainsKey("testsPassing") && context.ContainsKey("wasPassingBefore"))
                    {
                        var testsPassing = (bool)context["testsPassing"];
                        var wasPassingBefore = (bool)context["wasPassingBefore"];

                        if (!testsPassing && wasPassingBefore)
                        {
                            return new ViolationResult
                            {
                                IsViolation = true,
                                Message = "Test regression detected. Previously passing tests now failing."
                            };
                        }
                    }
                    return ViolationResult.NoViolation;
                }
            },

            new ViolationRule
            {
                RuleId = "QUALITY-002",
                Category = "Code Quality",
                Description = "NEVER commit compilation errors",
                Severity = ViolationSeverity.Critical,
                CheckFunction = (context) =>
                {
                    if (context.ContainsKey("buildSuccessful") && context.ContainsKey("attemptingCommit"))
                    {
                        var buildSuccessful = (bool)context["buildSuccessful"];
                        var attemptingCommit = (bool)context["attemptingCommit"];

                        if (!buildSuccessful && attemptingCommit)
                        {
                            return new ViolationResult
                            {
                                IsViolation = true,
                                Message = "Attempting to commit with build errors. Code must compile."
                            };
                        }
                    }
                    return ViolationResult.NoViolation;
                }
            },

            // Documentation
            new ViolationRule
            {
                RuleId = "DOC-001",
                Category = "Documentation",
                Description = "ALWAYS document public APIs with XML comments",
                Severity = ViolationSeverity.Medium,
                CheckFunction = (context) =>
                {
                    if (context.ContainsKey("publicApiAdded") && context.ContainsKey("hasXmlDoc"))
                    {
                        var publicApiAdded = (bool)context["publicApiAdded"];
                        var hasXmlDoc = (bool)context["hasXmlDoc"];

                        if (publicApiAdded && !hasXmlDoc)
                        {
                            return new ViolationResult
                            {
                                IsViolation = true,
                                Message = "Public API added without XML documentation."
                            };
                        }
                    }
                    return ViolationResult.NoViolation;
                }
            }
        };
    }

    /// <summary>
    /// Check all rules against current context
    /// </summary>
    public ViolationCheckResult CheckAllRules(Dictionary<string, object> context)
    {
        var violations = new List<ViolationResult>();

        foreach (var rule in _rules)
        {
            try
            {
                var result = rule.CheckFunction(context);
                if (result.IsViolation)
                {
                    result.RuleId = rule.RuleId;
                    result.Category = rule.Category;
                    result.Severity = rule.Severity;
                    violations.Add(result);

                    // Log violation
                    LogViolationAsync(rule, result, context).Wait();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Rule check failed for {rule.RuleId}: {ex.Message}");
            }
        }

        return new ViolationCheckResult
        {
            HasViolations = violations.Any(),
            Violations = violations,
            CheckedAt = DateTime.UtcNow,
            CriticalViolations = violations.Count(v => v.Severity == ViolationSeverity.Critical)
        };
    }

    /// <summary>
    /// Check specific rule category
    /// </summary>
    public ViolationCheckResult CheckCategory(string category, Dictionary<string, object> context)
    {
        var relevantRules = _rules.Where(r => r.Category == category).ToList();
        var violations = new List<ViolationResult>();

        foreach (var rule in relevantRules)
        {
            var result = rule.CheckFunction(context);
            if (result.IsViolation)
            {
                result.RuleId = rule.RuleId;
                result.Category = rule.Category;
                result.Severity = rule.Severity;
                violations.Add(result);

                LogViolationAsync(rule, result, context).Wait();
            }
        }

        return new ViolationCheckResult
        {
            HasViolations = violations.Any(),
            Violations = violations,
            CheckedAt = DateTime.UtcNow,
            CriticalViolations = violations.Count(v => v.Severity == ViolationSeverity.Critical)
        };
    }

    /// <summary>
    /// Log violation to file
    /// </summary>
    private async Task LogViolationAsync(ViolationRule rule, ViolationResult result, Dictionary<string, object> context)
    {
        var entry = new ViolationEvent
        {
            Timestamp = DateTime.UtcNow,
            RuleId = rule.RuleId,
            Category = rule.Category,
            Severity = result.Severity,
            Message = result.Message,
            Context = context
        };

        _violations.Add(entry);

        var logEntry = FormatViolationEntry(entry);
        await File.AppendAllTextAsync(_logPath, logEntry);

        // Console output
        var severitySymbol = result.Severity switch
        {
            ViolationSeverity.Critical => "🚨",
            ViolationSeverity.High => "⚠️",
            ViolationSeverity.Medium => "⚡",
            ViolationSeverity.Low => "ℹ️",
            _ => "❓"
        };

        Console.WriteLine($"{severitySymbol} VIOLATION DETECTED [{rule.RuleId}] {result.Message}");
    }

    /// <summary>
    /// Format violation for log file
    /// </summary>
    private string FormatViolationEntry(ViolationEvent violation)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\n## ❌ VIOLATION: {violation.RuleId} - {violation.Timestamp:yyyy-MM-dd HH:mm:ss}\n");
        sb.AppendLine($"**Category:** {violation.Category}");
        sb.AppendLine($"**Severity:** {violation.Severity}");
        sb.AppendLine($"**Message:** {violation.Message}\n");

        if (violation.Context.Any())
        {
            sb.AppendLine("**Context:**");
            foreach (var kvp in violation.Context)
            {
                sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
            }
            sb.AppendLine();
        }

        sb.AppendLine("---\n");
        return sb.ToString();
    }

    /// <summary>
    /// Get violation statistics
    /// </summary>
    public ViolationStatistics GetStatistics()
    {
        return new ViolationStatistics
        {
            TotalViolations = _violations.Count,
            CriticalViolations = _violations.Count(v => v.Severity == ViolationSeverity.Critical),
            HighViolations = _violations.Count(v => v.Severity == ViolationSeverity.High),
            MediumViolations = _violations.Count(v => v.Severity == ViolationSeverity.Medium),
            LowViolations = _violations.Count(v => v.Severity == ViolationSeverity.Low),
            MostCommonViolation = _violations
                .GroupBy(v => v.RuleId)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key
        };
    }
}

/// <summary>
/// Violation rule definition
/// </summary>
public class ViolationRule
{
    public string RuleId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; }
    public Func<Dictionary<string, object>, ViolationResult> CheckFunction { get; set; } = _ => ViolationResult.NoViolation;
}

/// <summary>
/// Result of violation check
/// </summary>
public class ViolationResult
{
    public bool IsViolation { get; set; }
    public string Message { get; set; } = string.Empty;
    public string RuleId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; }

    public static ViolationResult NoViolation => new ViolationResult { IsViolation = false };
}

/// <summary>
/// Violation check result (multiple rules)
/// </summary>
public class ViolationCheckResult
{
    public bool HasViolations { get; set; }
    public List<ViolationResult> Violations { get; set; } = new();
    public DateTime CheckedAt { get; set; }
    public int CriticalViolations { get; set; }
}

/// <summary>
/// Logged violation event
/// </summary>
public class ViolationEvent
{
    public DateTime Timestamp { get; set; }
    public string RuleId { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ViolationSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Context { get; set; } = new();
}

/// <summary>
/// Violation severity levels
/// </summary>
public enum ViolationSeverity
{
    Low = 0,
    Medium = 1,
    High = 2,
    Critical = 3
}

/// <summary>
/// Violation statistics
/// </summary>
public class ViolationStatistics
{
    public int TotalViolations { get; set; }
    public int CriticalViolations { get; set; }
    public int HighViolations { get; set; }
    public int MediumViolations { get; set; }
    public int LowViolations { get; set; }
    public string? MostCommonViolation { get; set; }
}
