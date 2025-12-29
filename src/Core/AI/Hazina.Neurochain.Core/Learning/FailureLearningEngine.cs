using System.Text;
using Hazina.AI.Providers.Core;

namespace Hazina.Neurochain.Core.Learning;

/// <summary>
/// Self-improving failure analysis and learning engine
/// Tracks failures, learns patterns, and generates improvement recommendations
/// </summary>
public class FailureLearningEngine
{
    private readonly List<FailureRecord> _failures = new();
    private readonly List<FailurePattern> _patterns = new();
    private readonly IProviderOrchestrator? _orchestrator;
    private readonly FailureLearningConfig _config;

    public FailureLearningEngine(IProviderOrchestrator? orchestrator = null, FailureLearningConfig? config = null)
    {
        _orchestrator = orchestrator;
        _config = config ?? new FailureLearningConfig();
    }

    /// <summary>
    /// Record a failure for learning
    /// </summary>
    public void RecordFailure(FailureRecord failure)
    {
        _failures.Add(failure ?? throw new ArgumentNullException(nameof(failure)));

        // Analyze immediately if enabled
        if (_config.AutoAnalyze)
        {
            _ = AnalyzeFailureAsync(failure);
        }

        // Clean old failures if limit exceeded
        if (_failures.Count > _config.MaxFailureHistory)
        {
            var toRemove = _failures.Count - _config.MaxFailureHistory;
            _failures.RemoveRange(0, toRemove);
        }
    }

    /// <summary>
    /// Analyze a specific failure
    /// </summary>
    public async Task<FailureAnalysis> AnalyzeFailureAsync(FailureRecord failure, CancellationToken cancellationToken = default)
    {
        var analysis = new FailureAnalysis
        {
            FailureId = failure.Id,
            Timestamp = DateTime.UtcNow
        };

        // Categorize if not already done
        if (failure.Category == FailureCategory.Other && _orchestrator != null)
        {
            failure.Category = await CategorizeFailureAsync(failure, cancellationToken);
        }

        // Find root cause
        analysis.RootCause = await DetermineRootCauseAsync(failure, cancellationToken);
        failure.RootCause = analysis.RootCause;

        // Check for matching patterns
        var matchingPatterns = FindMatchingPatterns(failure);
        analysis.MatchingPatterns = matchingPatterns;

        // Generate recommendations
        analysis.Recommendations = GenerateRecommendations(failure, matchingPatterns);

        return analysis;
    }

    /// <summary>
    /// Learn patterns from accumulated failures
    /// </summary>
    public async Task<List<FailurePattern>> LearnPatternsAsync(CancellationToken cancellationToken = default)
    {
        var newPatterns = new List<FailurePattern>();

        // Group failures by category
        var grouped = _failures
            .Where(f => !f.Resolved)
            .GroupBy(f => f.Category);

        foreach (var group in grouped)
        {
            if (group.Count() < _config.MinOccurrencesForPattern)
                continue;

            // Look for common triggers
            var pattern = await DiscoverPatternAsync(group.ToList(), cancellationToken);
            if (pattern != null && pattern.Confidence >= _config.MinPatternConfidence)
            {
                // Check if pattern already exists
                var existing = _patterns.FirstOrDefault(p =>
                    p.Category == pattern.Category &&
                    p.Name.Equals(pattern.Name, StringComparison.OrdinalIgnoreCase));

                if (existing != null)
                {
                    // Update existing pattern
                    existing.Occurrences += pattern.Occurrences;
                    existing.LastObserved = DateTime.UtcNow;
                    existing.Confidence = Math.Max(existing.Confidence, pattern.Confidence);
                }
                else
                {
                    // Add new pattern
                    _patterns.Add(pattern);
                    newPatterns.Add(pattern);
                }
            }
        }

        return newPatterns;
    }

    /// <summary>
    /// Get improvement recommendations
    /// </summary>
    public List<ImprovementRecommendation> GetRecommendations(int maxRecommendations = 10)
    {
        var recommendations = new List<ImprovementRecommendation>();

        // Analyze active patterns
        var activePatterns = _patterns
            .Where(p => p.IsActive && p.Occurrences >= _config.MinOccurrencesForRecommendation)
            .OrderByDescending(p => p.Occurrences)
            .ThenByDescending(p => p.Confidence)
            .Take(maxRecommendations);

        foreach (var pattern in activePatterns)
        {
            var recommendation = new ImprovementRecommendation
            {
                Title = $"Prevent {pattern.Name} failures",
                Description = pattern.Description,
                PatternId = pattern.Id,
                Priority = CalculatePriority(pattern),
                ExpectedImpact = pattern.PreventionSuccessRate,
                Confidence = pattern.Confidence,
                RecommendedActions = pattern.PreventionStrategies
                    .OrderByDescending(s => s.SuccessRate)
                    .ToList(),
                Evidence = new List<string>
                {
                    $"Observed {pattern.Occurrences} times",
                    $"Success rate: {pattern.PreventionSuccessRate:P0}",
                    $"Pattern confidence: {pattern.Confidence:P0}"
                },
                AffectedScenarios = pattern.Triggers
            };

            recommendations.Add(recommendation);
        }

        return recommendations
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.ExpectedImpact)
            .ToList();
    }

    /// <summary>
    /// Get statistics about failures and learning
    /// </summary>
    public LearningStatistics GetStatistics()
    {
        var stats = new LearningStatistics
        {
            TotalFailures = _failures.Count,
            ResolvedFailures = _failures.Count(f => f.Resolved),
            UnresolvedFailures = _failures.Count(f => !f.Resolved),
            PatternsDiscovered = _patterns.Count,
            ActivePatterns = _patterns.Count(p => p.IsActive)
        };

        // Category breakdown
        stats.FailuresByCategory = _failures
            .GroupBy(f => f.Category)
            .ToDictionary(g => g.Key, g => g.Count());

        // Resolution method breakdown
        stats.ResolutionMethods = _failures
            .Where(f => f.Resolved && f.ResolutionMethod.HasValue)
            .GroupBy(f => f.ResolutionMethod!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        // Recent trends
        var recentFailures = _failures.Where(f => f.Timestamp > DateTime.UtcNow.AddDays(-7)).ToList();
        stats.FailuresLast7Days = recentFailures.Count;
        stats.ResolutionRateLast7Days = recentFailures.Count > 0
            ? (double)recentFailures.Count(f => f.Resolved) / recentFailures.Count
            : 0;

        // Most common patterns
        stats.TopPatterns = _patterns
            .OrderByDescending(p => p.Occurrences)
            .Take(5)
            .Select(p => new PatternSummary
            {
                Name = p.Name,
                Category = p.Category,
                Occurrences = p.Occurrences,
                PreventionSuccessRate = p.PreventionSuccessRate
            })
            .ToList();

        return stats;
    }

    /// <summary>
    /// Apply learned improvements to a reasoning context
    /// </summary>
    public void ApplyImprovements(ReasoningContext context, string prompt)
    {
        // Find relevant patterns based on prompt
        var relevantPatterns = _patterns
            .Where(p => p.IsActive &&
                        p.Triggers.Any(t => prompt.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(p => p.PreventionSuccessRate)
            .ToList();

        foreach (var pattern in relevantPatterns)
        {
            foreach (var strategy in pattern.PreventionStrategies.Where(s => s.SuccessRate > 0.5))
            {
                ApplyStrategy(context, strategy);
            }
        }
    }

    #region Private Methods

    private async Task<FailureCategory> CategorizeFailureAsync(FailureRecord failure, CancellationToken cancellationToken)
    {
        if (_orchestrator == null)
            return FailureCategory.Other;

        try
        {
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.System,
                    Text = "Categorize this AI reasoning failure. Respond with ONLY the category name: Hallucination, LogicalError, Contradiction, LowConfidence, FormatError, Performance, ProviderFailure, or Other."
                },
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.User,
                    Text = $"Prompt: {failure.Prompt}\nResponse: {failure.Response}\nIssues: {string.Join(", ", failure.ValidationIssues.Select(i => i.Description))}"
                }
            };

            var response = await _orchestrator.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);
            var category = response.Result.Trim();

            return Enum.TryParse<FailureCategory>(category, true, out var result) ? result : FailureCategory.Other;
        }
        catch
        {
            return FailureCategory.Other;
        }
    }

    private async Task<string> DetermineRootCauseAsync(FailureRecord failure, CancellationToken cancellationToken)
    {
        if (_orchestrator == null)
            return "Unable to determine root cause";

        try
        {
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.System,
                    Text = "Analyze this AI reasoning failure and identify the root cause in 1-2 sentences."
                },
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.User,
                    Text = $"Category: {failure.Category}\nPrompt: {failure.Prompt}\nResponse: {failure.Response}\nIssues: {string.Join(", ", failure.ValidationIssues.Select(i => i.Description))}"
                }
            };

            var response = await _orchestrator.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);
            return response.Result.Trim();
        }
        catch (Exception ex)
        {
            return $"Analysis failed: {ex.Message}";
        }
    }

    private List<FailurePattern> FindMatchingPatterns(FailureRecord failure)
    {
        var matches = new List<FailurePattern>();

        foreach (var pattern in _patterns.Where(p => p.IsActive && p.Category == failure.Category))
        {
            // Check if symptoms match
            var matchingSymptoms = pattern.Symptoms.Count(symptom =>
                failure.ValidationIssues.Any(issue =>
                    issue.Description.Contains(symptom, StringComparison.OrdinalIgnoreCase)));

            if (matchingSymptoms > 0)
            {
                matches.Add(pattern);
            }
        }

        return matches;
    }

    private List<ImprovementRecommendation> GenerateRecommendations(FailureRecord failure, List<FailurePattern> matchingPatterns)
    {
        var recommendations = new List<ImprovementRecommendation>();

        foreach (var pattern in matchingPatterns)
        {
            var rec = new ImprovementRecommendation
            {
                Title = $"Apply {pattern.Name} prevention",
                Description = $"This failure matches the '{pattern.Name}' pattern. Apply proven prevention strategies.",
                PatternId = pattern.Id,
                Priority = CalculatePriority(pattern),
                ExpectedImpact = pattern.PreventionSuccessRate,
                Confidence = pattern.Confidence,
                RecommendedActions = pattern.PreventionStrategies
                    .Where(s => s.SuccessRate > 0.5)
                    .OrderByDescending(s => s.SuccessRate)
                    .ToList()
            };

            recommendations.Add(rec);
        }

        // Add general recommendations based on category
        if (recommendations.Count == 0)
        {
            recommendations.Add(GenerateDefaultRecommendation(failure));
        }

        return recommendations;
    }

    private async Task<FailurePattern?> DiscoverPatternAsync(List<FailureRecord> failures, CancellationToken cancellationToken)
    {
        if (failures.Count < 2)
            return null;

        var category = failures.First().Category;

        // Extract common symptoms
        var allSymptoms = failures
            .SelectMany(f => f.ValidationIssues.Select(i => i.Type))
            .GroupBy(s => s)
            .Where(g => g.Count() >= failures.Count * 0.5) // Present in at least 50% of failures
            .Select(g => g.Key)
            .ToList();

        if (allSymptoms.Count == 0)
            return null;

        var pattern = new FailurePattern
        {
            Name = $"{category} Pattern #{_patterns.Count + 1}",
            Description = $"Recurring {category} pattern with {allSymptoms.Count} common symptoms",
            Category = category,
            Symptoms = allSymptoms,
            Occurrences = failures.Count,
            FirstObserved = failures.Min(f => f.Timestamp),
            LastObserved = failures.Max(f => f.Timestamp),
            ExampleFailureIds = failures.Take(5).Select(f => f.Id).ToList(),
            Confidence = Math.Min(0.9, 0.5 + (failures.Count * 0.05)) // Increase confidence with more observations
        };

        // Generate prevention strategies based on successful resolutions
        var resolvedFailures = failures.Where(f => f.Resolved && f.ResolutionMethod.HasValue).ToList();
        if (resolvedFailures.Count > 0)
        {
            var resolutionGroups = resolvedFailures
                .GroupBy(f => f.ResolutionMethod!.Value)
                .OrderByDescending(g => g.Count());

            foreach (var group in resolutionGroups)
            {
                var strategy = CreateStrategyFromResolution(group.Key, group.Count(), failures.Count);
                pattern.PreventionStrategies.Add(strategy);
            }

            pattern.PreventionSuccessRate = (double)resolvedFailures.Count / failures.Count;
        }

        return pattern;
    }

    private PreventionStrategy CreateStrategyFromResolution(ResolutionMethod method, int successes, int total)
    {
        var successRate = (double)successes / total;

        return method switch
        {
            ResolutionMethod.ProviderSwitch => new PreventionStrategy
            {
                Name = "Switch Provider",
                Description = "Use a different AI provider",
                Type = PreventionType.ProviderSwitch,
                Action = "SwitchProvider",
                SuccessRate = successRate,
                TimesApplied = total,
                TimesSucceeded = successes,
                CostImpact = 0.2,
                LatencyImpact = 0.1
            },
            ResolutionMethod.PromptRefinement => new PreventionStrategy
            {
                Name = "Refine Prompt",
                Description = "Add clarification to the prompt",
                Type = PreventionType.PromptModification,
                Action = "RefinePrompt",
                SuccessRate = successRate,
                TimesApplied = total,
                TimesSucceeded = successes,
                CostImpact = 0.1,
                LatencyImpact = 0.2
            },
            ResolutionMethod.CrossValidation => new PreventionStrategy
            {
                Name = "Enable Cross-Validation",
                Description = "Use multiple layers for validation",
                Type = PreventionType.CrossValidationEnable,
                Action = "EnableCrossValidation",
                SuccessRate = successRate,
                TimesApplied = total,
                TimesSucceeded = successes,
                CostImpact = 0.7,
                LatencyImpact = 0.6
            },
            _ => new PreventionStrategy
            {
                Name = "Generic Strategy",
                Description = "Apply standard error handling",
                Type = PreventionType.ValidationEnhancement,
                Action = "EnhanceValidation",
                SuccessRate = successRate,
                TimesApplied = total,
                TimesSucceeded = successes,
                CostImpact = 0.3,
                LatencyImpact = 0.3
            }
        };
    }

    private int CalculatePriority(FailurePattern pattern)
    {
        // Priority 1-5 based on occurrences and severity
        if (pattern.Occurrences >= 20) return 5;
        if (pattern.Occurrences >= 10) return 4;
        if (pattern.Occurrences >= 5) return 3;
        if (pattern.Occurrences >= 3) return 2;
        return 1;
    }

    private ImprovementRecommendation GenerateDefaultRecommendation(FailureRecord failure)
    {
        return new ImprovementRecommendation
        {
            Title = $"Address {failure.Category} failure",
            Description = failure.RootCause ?? "Review and address this failure",
            Priority = 2,
            ExpectedImpact = 0.5,
            Confidence = 0.6,
            RecommendedActions = new List<PreventionStrategy>
            {
                new PreventionStrategy
                {
                    Name = "Increase Validation",
                    Description = "Add more validation checks",
                    Type = PreventionType.ValidationEnhancement,
                    Action = "EnhanceValidation",
                    SuccessRate = 0.7,
                    CostImpact = 0.3,
                    LatencyImpact = 0.2
                }
            }
        };
    }

    private void ApplyStrategy(ReasoningContext context, PreventionStrategy strategy)
    {
        switch (strategy.Type)
        {
            case PreventionType.ConfidenceAdjustment:
                context.MinConfidence = Math.Min(0.95, context.MinConfidence + 0.1);
                break;

            case PreventionType.ReasoningDepthIncrease:
                context.MaxSteps = Math.Min(20, context.MaxSteps + 5);
                break;

            case PreventionType.GroundTruthAddition:
                // Would need access to ground truth database
                break;

            case PreventionType.ContextEnhancement:
                if (strategy.Parameters.TryGetValue("domain", out var domain) && domain is string domainStr)
                {
                    context.Domain = domainStr;
                }
                break;
        }
    }

    #endregion
}

/// <summary>
/// Configuration for failure learning
/// </summary>
public class FailureLearningConfig
{
    public bool AutoAnalyze { get; set; } = true;
    public int MaxFailureHistory { get; set; } = 1000;
    public int MinOccurrencesForPattern { get; set; } = 3;
    public int MinOccurrencesForRecommendation { get; set; } = 5;
    public double MinPatternConfidence { get; set; } = 0.6;
}

/// <summary>
/// Analysis result for a failure
/// </summary>
public class FailureAnalysis
{
    public string FailureId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string RootCause { get; set; } = string.Empty;
    public List<FailurePattern> MatchingPatterns { get; set; } = new();
    public List<ImprovementRecommendation> Recommendations { get; set; } = new();
}

/// <summary>
/// Learning statistics
/// </summary>
public class LearningStatistics
{
    public int TotalFailures { get; set; }
    public int ResolvedFailures { get; set; }
    public int UnresolvedFailures { get; set; }
    public int PatternsDiscovered { get; set; }
    public int ActivePatterns { get; set; }
    public Dictionary<FailureCategory, int> FailuresByCategory { get; set; } = new();
    public Dictionary<ResolutionMethod, int> ResolutionMethods { get; set; } = new();
    public int FailuresLast7Days { get; set; }
    public double ResolutionRateLast7Days { get; set; }
    public List<PatternSummary> TopPatterns { get; set; } = new();
}

/// <summary>
/// Pattern summary for statistics
/// </summary>
public class PatternSummary
{
    public string Name { get; set; } = string.Empty;
    public FailureCategory Category { get; set; }
    public int Occurrences { get; set; }
    public double PreventionSuccessRate { get; set; }
}
