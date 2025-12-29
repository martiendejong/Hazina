using Hazina.Neurochain.Core.Learning;
using Hazina.AI.Providers.Core;

namespace Hazina.Neurochain.Core.Analysis;

/// <summary>
/// Adaptive Behavior Engine that adjusts reasoning strategy based on task complexity,
/// context, and learned patterns
/// </summary>
public class AdaptiveBehaviorEngine
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly FailureLearningEngine? _learningEngine;
    private readonly AdaptiveBehaviorConfig _config;

    public AdaptiveBehaviorEngine(
        IProviderOrchestrator orchestrator,
        FailureLearningEngine? learningEngine = null,
        AdaptiveBehaviorConfig? config = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _learningEngine = learningEngine;
        _config = config ?? new AdaptiveBehaviorConfig();
    }

    /// <summary>
    /// Analyze task and determine optimal configuration
    /// </summary>
    public async Task<AdaptiveConfiguration> AnalyzeAndConfigureAsync(
        string prompt,
        ReasoningContext? context = null,
        CancellationToken cancellationToken = default)
    {
        context ??= new ReasoningContext();

        // Analyze task complexity
        var complexity = await AnalyzeComplexityAsync(prompt, cancellationToken);

        // Create adaptive configuration
        var config = new AdaptiveConfiguration
        {
            TaskComplexity = complexity.Level,
            ComplexityScore = complexity.Score,
            RecommendedLayers = DetermineOptimalLayers(complexity),
            RecommendedConfig = DetermineNeuroChainConfig(complexity),
            ReasoningContext = EnhanceContext(context, complexity),
            Justification = complexity.Reasoning
        };

        // Apply learned improvements
        if (_learningEngine != null)
        {
            _learningEngine.ApplyImprovements(config.ReasoningContext, prompt);
        }

        return config;
    }

    /// <summary>
    /// Analyze task complexity
    /// </summary>
    public async Task<ComplexityAnalysis> AnalyzeComplexityAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        var analysis = new ComplexityAnalysis
        {
            Prompt = prompt
        };

        // Quick heuristic-based analysis
        var heuristicScore = CalculateHeuristicComplexity(prompt);

        // AI-based analysis if available and prompt is ambiguous
        if (_config.UseAIComplexityAnalysis && heuristicScore > 0.3 && heuristicScore < 0.7)
        {
            var aiAnalysis = await PerformAIComplexityAnalysisAsync(prompt, cancellationToken);
            analysis.Score = (heuristicScore + aiAnalysis.Score) / 2.0; // Average
            analysis.Reasoning = aiAnalysis.Reasoning;
            analysis.Factors = aiAnalysis.Factors;
        }
        else
        {
            analysis.Score = heuristicScore;
            analysis.Reasoning = "Heuristic-based complexity analysis";
            analysis.Factors = GetHeuristicFactors(prompt);
        }

        // Determine complexity level
        analysis.Level = analysis.Score switch
        {
            < 0.3 => ComplexityLevel.Simple,
            < 0.6 => ComplexityLevel.Moderate,
            < 0.8 => ComplexityLevel.Complex,
            _ => ComplexityLevel.VeryComplex
        };

        return analysis;
    }

    /// <summary>
    /// Adapt configuration based on feedback
    /// </summary>
    public void AdaptBasedOnFeedback(NeuroChainResult result, bool successful, string? feedback = null)
    {
        // Record metrics for learning
        var complexity = result.FinalConfidence < 0.7 ? ComplexityLevel.Complex : ComplexityLevel.Moderate;

        if (!successful && _learningEngine != null)
        {
            // Create failure record
            var failure = new FailureRecord
            {
                Prompt = result.Prompt,
                Response = result.FinalAnswer,
                Category = DetermineFailureCategory(result),
                Severity = 1.0 - result.FinalConfidence,
                ValidationIssues = result.CrossValidation?.Issues ?? new List<ValidationIssue>(),
                Context = new Dictionary<string, object>
                {
                    ["complexity"] = complexity.ToString(),
                    ["layers_used"] = result.LayerResults.Count,
                    ["total_cost"] = result.TotalCost,
                    ["total_duration_ms"] = result.TotalDurationMs
                }
            };

            _learningEngine.RecordFailure(failure);
        }
    }

    #region Private Methods

    private double CalculateHeuristicComplexity(string prompt)
    {
        double score = 0.0;
        int factors = 0;

        // Length-based complexity
        if (prompt.Length < 50)
        {
            score += 0.1;
        }
        else if (prompt.Length < 150)
        {
            score += 0.3;
        }
        else if (prompt.Length < 500)
        {
            score += 0.6;
        }
        else
        {
            score += 0.8;
        }
        factors++;

        // Question complexity
        var questionWords = new[] { "why", "how", "explain", "analyze", "compare", "evaluate" };
        if (questionWords.Any(q => prompt.Contains(q, StringComparison.OrdinalIgnoreCase)))
        {
            score += 0.4;
            factors++;
        }

        // Code-related complexity
        if (prompt.Contains("code", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("function", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("refactor", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.5;
            factors++;
        }

        // Mathematical complexity
        if (System.Text.RegularExpressions.Regex.IsMatch(prompt, @"\d+\s*[+\-*/]\s*\d+") ||
            prompt.Contains("calculate", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("solve", StringComparison.OrdinalIgnoreCase))
        {
            score += 0.3;
            factors++;
        }

        // Multi-step indicators
        var multiStepWords = new[] { "first", "then", "finally", "next", "step" };
        var multiStepCount = multiStepWords.Count(w => prompt.Contains(w, StringComparison.OrdinalIgnoreCase));
        if (multiStepCount >= 2)
        {
            score += 0.6;
            factors++;
        }

        // Domain-specific complexity
        var domains = new[] { "quantum", "neural", "algorithm", "architecture", "distributed", "concurrent" };
        if (domains.Any(d => prompt.Contains(d, StringComparison.OrdinalIgnoreCase)))
        {
            score += 0.5;
            factors++;
        }

        return factors > 0 ? Math.Min(1.0, score / factors) : 0.5;
    }

    private List<string> GetHeuristicFactors(string prompt)
    {
        var factors = new List<string>();

        if (prompt.Length > 200) factors.Add("Long prompt");
        if (prompt.Contains("why", StringComparison.OrdinalIgnoreCase)) factors.Add("Explanatory question");
        if (prompt.Contains("code", StringComparison.OrdinalIgnoreCase)) factors.Add("Code-related");
        if (prompt.Contains("compare", StringComparison.OrdinalIgnoreCase)) factors.Add("Comparative analysis");
        if (System.Text.RegularExpressions.Regex.IsMatch(prompt, @"\d+\s*[+\-*/]\s*\d+")) factors.Add("Mathematical");

        return factors;
    }

    private async Task<ComplexityAnalysis> PerformAIComplexityAnalysisAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        try
        {
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.System,
                    Text = @"Analyze the complexity of this task and respond in this format:
SCORE: [0.0-1.0]
FACTORS:
- [factor 1]
- [factor 2]
...
REASONING: [brief explanation]"
                },
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.User,
                    Text = $"Task: {prompt}"
                }
            };

            var response = await _orchestrator.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);
            return ParseComplexityResponse(response.Result);
        }
        catch
        {
            return new ComplexityAnalysis
            {
                Score = 0.5,
                Level = ComplexityLevel.Moderate,
                Reasoning = "AI analysis failed, using default",
                Factors = new List<string> { "Analysis unavailable" }
            };
        }
    }

    private ComplexityAnalysis ParseComplexityResponse(string response)
    {
        var analysis = new ComplexityAnalysis { Score = 0.5 };
        var lines = response.Split('\n');

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("SCORE:", StringComparison.OrdinalIgnoreCase))
            {
                var scoreStr = trimmed.Substring("SCORE:".Length).Trim();
                if (double.TryParse(scoreStr, out var score))
                {
                    analysis.Score = Math.Clamp(score, 0, 1);
                }
            }
            else if (trimmed.StartsWith("-") && !string.IsNullOrWhiteSpace(trimmed.Substring(1)))
            {
                analysis.Factors.Add(trimmed.Substring(1).Trim());
            }
            else if (trimmed.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase))
            {
                analysis.Reasoning = trimmed.Substring("REASONING:".Length).Trim();
            }
        }

        return analysis;
    }

    private List<Type> DetermineOptimalLayers(ComplexityAnalysis complexity)
    {
        return complexity.Level switch
        {
            ComplexityLevel.Simple => new List<Type>
            {
                typeof(Layers.FastReasoningLayer)
            },
            ComplexityLevel.Moderate => new List<Type>
            {
                typeof(Layers.FastReasoningLayer),
                typeof(Layers.DeepReasoningLayer)
            },
            ComplexityLevel.Complex => new List<Type>
            {
                typeof(Layers.FastReasoningLayer),
                typeof(Layers.DeepReasoningLayer),
                typeof(Layers.VerificationLayer)
            },
            ComplexityLevel.VeryComplex => new List<Type>
            {
                typeof(Layers.DeepReasoningLayer),
                typeof(Layers.VerificationLayer)
            },
            _ => new List<Type> { typeof(Layers.FastReasoningLayer) }
        };
    }

    private NeuroChainConfig DetermineNeuroChainConfig(ComplexityAnalysis complexity)
    {
        return complexity.Level switch
        {
            ComplexityLevel.Simple => new NeuroChainConfig
            {
                ParallelExecution = false,
                EnableCrossValidation = false,
                EnableEarlyStop = true,
                EarlyStopConfidenceThreshold = 0.8
            },
            ComplexityLevel.Moderate => new NeuroChainConfig
            {
                ParallelExecution = true,
                EnableCrossValidation = true,
                EnableEarlyStop = true,
                EarlyStopConfidenceThreshold = 0.9
            },
            ComplexityLevel.Complex => new NeuroChainConfig
            {
                ParallelExecution = true,
                EnableCrossValidation = true,
                EnableEarlyStop = false
            },
            ComplexityLevel.VeryComplex => new NeuroChainConfig
            {
                ParallelExecution = false, // Sequential for very complex to build on previous layers
                EnableCrossValidation = true,
                EnableEarlyStop = false
            },
            _ => new NeuroChainConfig()
        };
    }

    private ReasoningContext EnhanceContext(ReasoningContext context, ComplexityAnalysis complexity)
    {
        // Adjust confidence threshold based on complexity
        context.MinConfidence = complexity.Level switch
        {
            ComplexityLevel.Simple => 0.7,
            ComplexityLevel.Moderate => 0.8,
            ComplexityLevel.Complex => 0.9,
            ComplexityLevel.VeryComplex => 0.95,
            _ => 0.8
        };

        // Adjust max steps
        context.MaxSteps = complexity.Level switch
        {
            ComplexityLevel.Simple => 5,
            ComplexityLevel.Moderate => 10,
            ComplexityLevel.Complex => 15,
            ComplexityLevel.VeryComplex => 20,
            _ => 10
        };

        return context;
    }

    private FailureCategory DetermineFailureCategory(NeuroChainResult result)
    {
        if (result.CrossValidation?.Issues.Any(i => i.Type == "NoConsensus") == true)
            return FailureCategory.Contradiction;

        if (result.FinalConfidence < 0.5)
            return FailureCategory.LowConfidence;

        if (result.Error != null)
            return FailureCategory.ProviderFailure;

        return FailureCategory.LogicalError;
    }

    #endregion
}

/// <summary>
/// Configuration for adaptive behavior
/// </summary>
public class AdaptiveBehaviorConfig
{
    /// <summary>
    /// Use AI to analyze task complexity
    /// </summary>
    public bool UseAIComplexityAnalysis { get; set; } = true;

    /// <summary>
    /// Automatically adjust configuration based on results
    /// </summary>
    public bool AutoAdjust { get; set; } = true;

    /// <summary>
    /// Enable learning from feedback
    /// </summary>
    public bool EnableLearning { get; set; } = true;
}

/// <summary>
/// Adaptive configuration result
/// </summary>
public class AdaptiveConfiguration
{
    public ComplexityLevel TaskComplexity { get; set; }
    public double ComplexityScore { get; set; }
    public List<Type> RecommendedLayers { get; set; } = new();
    public NeuroChainConfig RecommendedConfig { get; set; } = new();
    public ReasoningContext ReasoningContext { get; set; } = new();
    public string Justification { get; set; } = string.Empty;
}

/// <summary>
/// Complexity analysis result
/// </summary>
public class ComplexityAnalysis
{
    public string Prompt { get; set; } = string.Empty;
    public double Score { get; set; }
    public ComplexityLevel Level { get; set; }
    public List<string> Factors { get; set; } = new();
    public string Reasoning { get; set; } = string.Empty;
}

/// <summary>
/// Task complexity levels
/// </summary>
public enum ComplexityLevel
{
    Simple,
    Moderate,
    Complex,
    VeryComplex
}
