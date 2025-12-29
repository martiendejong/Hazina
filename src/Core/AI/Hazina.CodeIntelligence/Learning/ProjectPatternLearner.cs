using Hazina.AI.Providers.Core;
using Hazina.CodeIntelligence.Core;
using System.Text;

namespace Hazina.CodeIntelligence.Learning;

/// <summary>
/// Learns project-specific patterns and conventions
/// </summary>
public class ProjectPatternLearner
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly List<LearnedPattern> _patterns = new();

    public ProjectPatternLearner(IProviderOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    /// <summary>
    /// Learn patterns from project context
    /// </summary>
    public async Task<LearningResult> LearnPatternsAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new LearningResult
        {
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // Learn various pattern types
            var namingPatterns = await LearnNamingPatternsAsync(context, cancellationToken);
            var codingPatterns = await LearnCodingPatternsAsync(context, cancellationToken);
            var architecturalPatterns = await LearnArchitecturalPatternsAsync(context, cancellationToken);

            result.PatternsLearned.AddRange(namingPatterns);
            result.PatternsLearned.AddRange(codingPatterns);
            result.PatternsLearned.AddRange(architecturalPatterns);

            // Store learned patterns
            _patterns.AddRange(result.PatternsLearned);

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Apply learned patterns to suggest improvements
    /// </summary>
    public List<PatternSuggestion> SuggestImprovements(CodeFile file, ProjectContext context)
    {
        var suggestions = new List<PatternSuggestion>();

        foreach (var pattern in _patterns.Where(p => p.IsActive && p.Confidence > 0.7))
        {
            var violations = DetectPatternViolations(file, pattern, context);
            suggestions.AddRange(violations);
        }

        return suggestions
            .OrderByDescending(s => s.Priority)
            .ToList();
    }

    /// <summary>
    /// Get all learned patterns
    /// </summary>
    public List<LearnedPattern> GetPatterns() => _patterns.ToList();

    /// <summary>
    /// Get patterns by category
    /// </summary>
    public List<LearnedPattern> GetPatternsByCategory(PatternCategory category)
    {
        return _patterns.Where(p => p.Category == category).ToList();
    }

    #region Private Methods

    private async Task<List<LearnedPattern>> LearnNamingPatternsAsync(
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var patterns = new List<LearnedPattern>();

        // Group symbols by type
        var symbolsByType = context.Symbols.Values
            .GroupBy(s => s.Type)
            .Where(g => g.Count() >= 5); // Need at least 5 examples

        foreach (var group in symbolsByType)
        {
            var names = group.Select(s => s.Name).ToList();

            // Detect naming pattern
            var pattern = DetectNamingConvention(names, group.Key.ToString());
            if (pattern != null)
            {
                patterns.Add(pattern);
            }
        }

        return patterns;
    }

    private async Task<List<LearnedPattern>> LearnCodingPatternsAsync(
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var patterns = new List<LearnedPattern>();

        // Sample files for analysis
        var sampleFiles = context.Files
            .Where(f => f.Size < 100000) // Skip very large files
            .Take(10)
            .ToList();

        if (sampleFiles.Count == 0)
            return patterns;

        // Build prompt for AI pattern detection
        var prompt = BuildPatternDetectionPrompt(sampleFiles);

        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = "You are an expert at identifying coding patterns and conventions. Analyze the code samples and identify consistent patterns."
            },
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = prompt
            }
        };

        var response = await _orchestrator.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);
        patterns = ParseLearnedPatterns(response.Result, PatternCategory.CodingStyle);

        return patterns;
    }

    private async Task<List<LearnedPattern>> LearnArchitecturalPatternsAsync(
        ProjectContext context,
        CancellationToken cancellationToken)
    {
        var patterns = new List<LearnedPattern>();

        if (context.Architecture != null && !string.IsNullOrEmpty(context.Architecture.ArchitecturalPattern))
        {
            patterns.Add(new LearnedPattern
            {
                Name = context.Architecture.ArchitecturalPattern,
                Description = context.Architecture.StructureDescription ?? "",
                Category = PatternCategory.ArchitecturalPattern,
                Examples = context.Architecture.Layers,
                Confidence = 0.9,
                IsActive = true
            });
        }

        return patterns;
    }

    private LearnedPattern? DetectNamingConvention(List<string> names, string symbolType)
    {
        if (names.Count < 5)
            return null;

        // Detect dominant case style
        var caseStyles = names.Select(n => DetectCaseStyle(n)).ToList();
        var dominantStyle = caseStyles
            .GroupBy(s => s)
            .OrderByDescending(g => g.Count())
            .First();

        var confidence = (double)dominantStyle.Count() / names.Count;

        if (confidence < 0.7) // At least 70% consistency
            return null;

        return new LearnedPattern
        {
            Name = $"{symbolType} Naming Convention",
            Description = $"{symbolType}s use {dominantStyle.Key} naming",
            Category = PatternCategory.NamingConvention,
            Examples = names.Take(5).ToList(),
            Confidence = confidence,
            Rule = $"Use {dominantStyle.Key} for {symbolType}s",
            IsActive = true
        };
    }

    private string DetectCaseStyle(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "unknown";

        if (name.Contains('_'))
        {
            return char.IsUpper(name[0]) ? "UPPER_SNAKE_CASE" : "snake_case";
        }
        else if (char.IsUpper(name[0]))
        {
            return name.All(c => char.IsUpper(c) || char.IsDigit(c)) ? "UPPERCASE" : "PascalCase";
        }
        else
        {
            return "camelCase";
        }
    }

    private string BuildPatternDetectionPrompt(List<CodeFile> files)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze these code files and identify consistent patterns:");
        sb.AppendLine();

        foreach (var file in files.Take(5))
        {
            sb.AppendLine($"File: {file.Path}");
            sb.AppendLine("```");
            sb.AppendLine(file.Content.Length > 1000
                ? file.Content.Substring(0, 1000) + "\n... (truncated)"
                : file.Content);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("Identify patterns in:");
        sb.AppendLine("1. Error handling (try-catch patterns, error return values, etc.)");
        sb.AppendLine("2. Logging (how and when logging is done)");
        sb.AppendLine("3. Code organization (file structure, class organization)");
        sb.AppendLine("4. Common utilities or helpers used");
        sb.AppendLine();
        sb.AppendLine("Format:");
        sb.AppendLine("PATTERN: [name]");
        sb.AppendLine("CATEGORY: [ErrorHandling/Logging/Organization/Other]");
        sb.AppendLine("DESCRIPTION: [what the pattern is]");
        sb.AppendLine("RULE: [how to apply it]");
        sb.AppendLine("CONFIDENCE: [0.0-1.0]");
        sb.AppendLine("---");

        return sb.ToString();
    }

    private List<LearnedPattern> ParseLearnedPatterns(string response, PatternCategory defaultCategory)
    {
        var patterns = new List<LearnedPattern>();
        var lines = response.Split('\n');

        LearnedPattern? current = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed == "---")
            {
                if (current != null)
                {
                    patterns.Add(current);
                    current = null;
                }
            }
            else if (trimmed.StartsWith("PATTERN:", StringComparison.OrdinalIgnoreCase))
            {
                current = new LearnedPattern
                {
                    Name = trimmed.Substring("PATTERN:".Length).Trim(),
                    Category = defaultCategory,
                    IsActive = true
                };
            }
            else if (current != null)
            {
                if (trimmed.StartsWith("CATEGORY:", StringComparison.OrdinalIgnoreCase))
                {
                    var category = trimmed.Substring("CATEGORY:".Length).Trim();
                    current.Category = Enum.TryParse<PatternCategory>(category, true, out var cat)
                        ? cat
                        : defaultCategory;
                }
                else if (trimmed.StartsWith("DESCRIPTION:", StringComparison.OrdinalIgnoreCase))
                {
                    current.Description = trimmed.Substring("DESCRIPTION:".Length).Trim();
                }
                else if (trimmed.StartsWith("RULE:", StringComparison.OrdinalIgnoreCase))
                {
                    current.Rule = trimmed.Substring("RULE:".Length).Trim();
                }
                else if (trimmed.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
                {
                    var confStr = trimmed.Substring("CONFIDENCE:".Length).Trim();
                    if (double.TryParse(confStr, out var conf))
                    {
                        current.Confidence = conf;
                    }
                }
            }
        }

        if (current != null)
        {
            patterns.Add(current);
        }

        return patterns;
    }

    private List<PatternSuggestion> DetectPatternViolations(
        CodeFile file,
        LearnedPattern pattern,
        ProjectContext context)
    {
        var suggestions = new List<PatternSuggestion>();

        // Simple pattern matching for naming conventions
        if (pattern.Category == PatternCategory.NamingConvention)
        {
            foreach (var symbol in file.Symbols)
            {
                var expectedStyle = ExtractExpectedStyle(pattern.Description);
                var actualStyle = DetectCaseStyle(symbol.Name);

                if (expectedStyle != null && actualStyle != expectedStyle)
                {
                    suggestions.Add(new PatternSuggestion
                    {
                        Pattern = pattern.Name,
                        File = file.Path,
                        Location = $"Line {symbol.StartLine}",
                        Issue = $"Symbol '{symbol.Name}' uses {actualStyle} but project convention is {expectedStyle}",
                        Suggestion = $"Rename to follow {expectedStyle} convention",
                        Priority = 2
                    });
                }
            }
        }

        return suggestions;
    }

    private string? ExtractExpectedStyle(string description)
    {
        var styles = new[] { "PascalCase", "camelCase", "snake_case", "UPPER_SNAKE_CASE", "UPPERCASE" };
        return styles.FirstOrDefault(s => description.Contains(s, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}

/// <summary>
/// Learned pattern
/// </summary>
public class LearnedPattern
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public PatternCategory Category { get; set; }
    public List<string> Examples { get; set; } = new();
    public double Confidence { get; set; }
    public string Rule { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime LearnedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Pattern learning result
/// </summary>
public class LearningResult
{
    public DateTime Timestamp { get; set; }
    public List<LearnedPattern> PatternsLearned { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Pattern suggestion
/// </summary>
public class PatternSuggestion
{
    public string Pattern { get; set; } = string.Empty;
    public string File { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
    public int Priority { get; set; } // 1-5
}
