using Hazina.AI.Providers.Core;
using Hazina.CodeIntelligence.Core;
using Hazina.Neurochain.Core;
using System.Text;

namespace Hazina.CodeIntelligence.Analysis;

/// <summary>
/// Detects logical inconsistencies in code, documentation, and architecture
/// </summary>
public class LogicalInconsistencyDetector
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly NeuroChainOrchestrator? _neurochain;

    public LogicalInconsistencyDetector(
        IProviderOrchestrator orchestrator,
        NeuroChainOrchestrator? neurochain = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _neurochain = neurochain;
    }

    /// <summary>
    /// Detect inconsistencies in project
    /// </summary>
    public async Task<InconsistencyReport> DetectInconsistenciesAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var report = new InconsistencyReport
        {
            Timestamp = DateTime.UtcNow,
            ProjectName = context.Name
        };

        // Detect various types of inconsistencies
        var tasks = new List<Task>
        {
            Task.Run(async () =>
            {
                report.NamingInconsistencies = await DetectNamingInconsistenciesAsync(context, cancellationToken);
            }, cancellationToken),

            Task.Run(async () =>
            {
                report.LogicalInconsistencies = await DetectLogicalInconsistenciesAsync(context, cancellationToken);
            }, cancellationToken),

            Task.Run(async () =>
            {
                report.ArchitecturalInconsistencies = await DetectArchitecturalInconsistenciesAsync(context, cancellationToken);
            }, cancellationToken),

            Task.Run(async () =>
            {
                report.DocumentationInconsistencies = await DetectDocumentationInconsistenciesAsync(context, cancellationToken);
            }, cancellationToken)
        };

        await Task.WhenAll(tasks);

        // Calculate overall score
        report.ConsistencyScore = CalculateConsistencyScore(report);

        return report;
    }

    /// <summary>
    /// Analyze specific code section for inconsistencies
    /// </summary>
    public async Task<List<Inconsistency>> AnalyzeCodeAsync(
        string code,
        string context,
        CancellationToken cancellationToken = default)
    {
        var inconsistencies = new List<Inconsistency>();

        var prompt = $@"Analyze this code for logical inconsistencies, contradictions, or violations of best practices:

Context: {context}

Code:
```
{code}
```

Identify any:
1. Logical errors or contradictions
2. Code smell or anti-patterns
3. Naming inconsistencies
4. Documentation mismatches
5. Potential bugs

Format:
ISSUE: [type]
SEVERITY: [Low/Medium/High]
DESCRIPTION: [description]
LOCATION: [where in code]
SUGGESTION: [how to fix]
---";

        string response;
        if (_neurochain != null)
        {
            var result = await _neurochain.ReasonAsync(prompt, new ReasoningContext
            {
                MinConfidence = 0.85,
                Domain = "Software Engineering - Code Analysis"
            }, cancellationToken);
            response = result.FinalAnswer;
        }
        else
        {
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.System,
                    Text = "You are an expert code reviewer. Identify logical inconsistencies and issues."
                },
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.User,
                    Text = prompt
                }
            };

            var llmResponse = await _orchestrator.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);
            response = llmResponse.Result;
        }

        inconsistencies = ParseInconsistencies(response);

        return inconsistencies;
    }

    #region Private Methods

    private async Task<List<Inconsistency>> DetectNamingInconsistenciesAsync(
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var inconsistencies = new List<Inconsistency>();

        // Extract naming patterns
        var namingPatterns = ExtractNamingPatterns(context);

        // Find deviations from dominant patterns
        foreach (var pattern in namingPatterns)
        {
            if (pattern.Occurrences < namingPatterns.Max(p => p.Occurrences) * 0.3)
            {
                inconsistencies.Add(new Inconsistency
                {
                    Type = InconsistencyType.NamingConvention,
                    Severity = InconsistencySeverity.Low,
                    Description = $"Inconsistent {pattern.SymbolType} naming: {pattern.Pattern}",
                    AffectedFiles = pattern.Examples,
                    Suggestion = $"Consider using dominant pattern for consistency"
                });
            }
        }

        return inconsistencies;
    }

    private async Task<List<Inconsistency>> DetectLogicalInconsistenciesAsync(
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var inconsistencies = new List<Inconsistency>();

        // Check for contradictory logic patterns
        // This would require deeper semantic analysis

        return inconsistencies;
    }

    private async Task<List<Inconsistency>> DetectArchitecturalInconsistenciesAsync(
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var inconsistencies = new List<Inconsistency>();

        if (context.Architecture == null)
            return inconsistencies;

        // Check for layer violations
        foreach (var dep in context.Architecture.ComponentDependencies)
        {
            if (dep.Type == DependencyType.Circular)
            {
                inconsistencies.Add(new Inconsistency
                {
                    Type = InconsistencyType.Architecture,
                    Severity = InconsistencySeverity.High,
                    Description = $"Circular dependency between {dep.From} and {dep.To}",
                    Suggestion = "Break circular dependency by introducing interface or moving shared code"
                });
            }
        }

        return inconsistencies;
    }

    private async Task<List<Inconsistency>> DetectDocumentationInconsistenciesAsync(
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var inconsistencies = new List<Inconsistency>();

        // Check for missing or outdated documentation
        // This would require comparing code with comments/docs

        return inconsistencies;
    }

    private List<NamingPattern> ExtractNamingPatterns(ProjectContext context)
    {
        var patterns = new Dictionary<string, NamingPattern>();

        foreach (var symbol in context.Symbols.Values)
        {
            var pattern = IdentifyNamingPattern(symbol.Name, symbol.Type);
            var key = $"{symbol.Type}:{pattern}";

            if (!patterns.ContainsKey(key))
            {
                patterns[key] = new NamingPattern
                {
                    SymbolType = symbol.Type.ToString(),
                    Pattern = pattern,
                    Occurrences = 0,
                    Examples = new List<string>()
                };
            }

            patterns[key].Occurrences++;
            if (patterns[key].Examples.Count < 5)
            {
                patterns[key].Examples.Add($"{symbol.DefinedIn}:{symbol.Name}");
            }
        }

        return patterns.Values.ToList();
    }

    private string IdentifyNamingPattern(string name, SymbolType type)
    {
        if (string.IsNullOrEmpty(name))
            return "empty";

        // Identify case style
        if (char.IsUpper(name[0]))
        {
            if (name.Contains('_'))
                return "UPPER_SNAKE_CASE";
            else if (name.All(c => char.IsUpper(c) || char.IsDigit(c)))
                return "UPPERCASE";
            else
                return "PascalCase";
        }
        else
        {
            if (name.Contains('_'))
                return "snake_case";
            else
                return "camelCase";
        }
    }

    private List<Inconsistency> ParseInconsistencies(string response)
    {
        var inconsistencies = new List<Inconsistency>();
        var lines = response.Split('\n');

        Inconsistency? current = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed == "---")
            {
                if (current != null)
                {
                    inconsistencies.Add(current);
                    current = null;
                }
            }
            else if (trimmed.StartsWith("ISSUE:", StringComparison.OrdinalIgnoreCase))
            {
                current = new Inconsistency
                {
                    Type = ParseInconsistencyType(trimmed.Substring("ISSUE:".Length).Trim())
                };
            }
            else if (current != null)
            {
                if (trimmed.StartsWith("SEVERITY:", StringComparison.OrdinalIgnoreCase))
                {
                    var severity = trimmed.Substring("SEVERITY:".Length).Trim();
                    current.Severity = severity.ToLower() switch
                    {
                        "high" => InconsistencySeverity.High,
                        "medium" => InconsistencySeverity.Medium,
                        _ => InconsistencySeverity.Low
                    };
                }
                else if (trimmed.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
                {
                    current.Description = trimmed.Substring("DESCRIPTION:".Length).Trim();
                }
                else if (trimmed.StartsWith("LOCATION:", StringComparison.OrdinalIgnoreCase))
                {
                    current.Location = trimmed.Substring("LOCATION:".Length).Trim();
                }
                else if (trimmed.StartsWith("SUGGESTION:", StringComparison.OrdinalIgnoreCase))
                {
                    current.Suggestion = trimmed.Substring("SUGGESTION:".Length).Trim();
                }
            }
        }

        if (current != null)
        {
            inconsistencies.Add(current);
        }

        return inconsistencies;
    }

    private InconsistencyType ParseInconsistencyType(string type)
    {
        return type.ToLower() switch
        {
            "logical" => InconsistencyType.Logic,
            "naming" => InconsistencyType.NamingConvention,
            "architecture" => InconsistencyType.Architecture,
            "documentation" => InconsistencyType.Documentation,
            "code smell" => InconsistencyType.CodeSmell,
            _ => InconsistencyType.Other
        };
    }

    private double CalculateConsistencyScore(InconsistencyReport report)
    {
        var allInconsistencies = new List<Inconsistency>();
        allInconsistencies.AddRange(report.NamingInconsistencies);
        allInconsistencies.AddRange(report.LogicalInconsistencies);
        allInconsistencies.AddRange(report.ArchitecturalInconsistencies);
        allInconsistencies.AddRange(report.DocumentationInconsistencies);

        if (allInconsistencies.Count == 0)
            return 1.0;

        var severityScore = allInconsistencies.Sum(i => i.Severity switch
        {
            InconsistencySeverity.Low => 0.1,
            InconsistencySeverity.Medium => 0.3,
            InconsistencySeverity.High => 0.5,
            InconsistencySeverity.Critical => 1.0,
            _ => 0.1
        });

        // Score decreases with more issues
        return Math.Max(0, 1.0 - (severityScore / 10.0));
    }

    #endregion
}

/// <summary>
/// Naming pattern information
/// </summary>
public class NamingPattern
{
    public string SymbolType { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public int Occurrences { get; set; }
    public List<string> Examples { get; set; } = new();
}

/// <summary>
/// Inconsistency report
/// </summary>
public class InconsistencyReport
{
    public DateTime Timestamp { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public List<Inconsistency> NamingInconsistencies { get; set; } = new();
    public List<Inconsistency> LogicalInconsistencies { get; set; } = new();
    public List<Inconsistency> ArchitecturalInconsistencies { get; set; } = new();
    public List<Inconsistency> DocumentationInconsistencies { get; set; } = new();
    public double ConsistencyScore { get; set; } // 0-1, where 1 is perfect consistency
}

/// <summary>
/// Detected inconsistency
/// </summary>
public class Inconsistency
{
    public InconsistencyType Type { get; set; }
    public InconsistencySeverity Severity { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; }
    public List<string> AffectedFiles { get; set; } = new();
    public string Suggestion { get; set; } = string.Empty;
}

/// <summary>
/// Inconsistency types
/// </summary>
public enum InconsistencyType
{
    NamingConvention,
    Logic,
    Architecture,
    Documentation,
    CodeSmell,
    TypeMismatch,
    StateInconsistency,
    Other
}

/// <summary>
/// Inconsistency severity levels
/// </summary>
public enum InconsistencySeverity
{
    Low,
    Medium,
    High,
    Critical
}
