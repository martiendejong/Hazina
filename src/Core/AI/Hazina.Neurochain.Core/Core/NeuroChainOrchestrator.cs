using System.Diagnostics;
using Hazina.Neurochain.Core.Layers;

namespace Hazina.Neurochain.Core;

/// <summary>
/// Orchestrates multi-layer reasoning with the Neurochain architecture
/// Coordinates Fast, Deep, and Verification layers for robust reasoning
/// </summary>
public class NeuroChainOrchestrator
{
    private readonly List<IReasoningLayer> _layers = new();
    private readonly NeuroChainConfig _config;

    public NeuroChainOrchestrator(NeuroChainConfig? config = null)
    {
        _config = config ?? new NeuroChainConfig();
    }

    /// <summary>
    /// Add a reasoning layer
    /// </summary>
    public void AddLayer(IReasoningLayer layer)
    {
        _layers.Add(layer ?? throw new ArgumentNullException(nameof(layer)));
    }

    /// <summary>
    /// Remove all layers
    /// </summary>
    public void ClearLayers()
    {
        _layers.Clear();
    }

    /// <summary>
    /// Execute multi-layer reasoning
    /// </summary>
    public async Task<NeuroChainResult> ReasonAsync(
        string prompt,
        ReasoningContext? context = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        context ??= new ReasoningContext();

        if (_layers.Count == 0)
        {
            throw new InvalidOperationException("No reasoning layers configured. Add at least one layer.");
        }

        var result = new NeuroChainResult
        {
            Prompt = prompt,
            LayerResults = new List<ReasoningResult>()
        };

        try
        {
            // Phase 1: Execute all layers in parallel (if enabled) or sequentially
            if (_config.ParallelExecution && _layers.Count > 1)
            {
                var tasks = _layers.Select(layer => ExecuteLayerAsync(layer, prompt, context, cancellationToken));
                result.LayerResults = (await Task.WhenAll(tasks)).ToList();
            }
            else
            {
                foreach (var layer in _layers)
                {
                    var layerResult = await ExecuteLayerAsync(layer, prompt, context, cancellationToken);
                    result.LayerResults.Add(layerResult);

                    // Early stopping if result is highly confident and valid
                    if (_config.EnableEarlyStop &&
                        layerResult.IsValid &&
                        layerResult.Confidence >= _config.EarlyStopConfidenceThreshold)
                    {
                        result.EarlyStopped = true;
                        result.EarlyStopReason = $"Layer '{layer.Name}' achieved {layerResult.Confidence:P0} confidence";
                        break;
                    }
                }
            }

            // Phase 2: Cross-validation
            if (!result.EarlyStopped && _config.EnableCrossValidation && result.LayerResults.Count > 1)
            {
                var verificationLayer = _layers.OfType<VerificationLayer>().FirstOrDefault();
                if (verificationLayer != null)
                {
                    result.CrossValidation = await verificationLayer.CrossValidateAsync(
                        result.LayerResults,
                        context,
                        cancellationToken
                    );
                }
                else
                {
                    // Fallback: simple consensus
                    result.CrossValidation = PerformSimpleCrossValidation(result.LayerResults, context);
                }
            }

            // Phase 3: Determine final answer
            result.FinalAnswer = DetermineFinalAnswer(result, context);
            result.FinalConfidence = DetermineFinalConfidence(result, context);

            sw.Stop();
            result.TotalDurationMs = sw.ElapsedMilliseconds;
            result.TotalCost = result.LayerResults.Sum(r => r.Cost);
            result.IsSuccessful = true;
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.IsSuccessful = false;
            result.Error = ex.Message;
            result.TotalDurationMs = sw.ElapsedMilliseconds;
        }

        return result;
    }

    #region Private Methods

    private async Task<ReasoningResult> ExecuteLayerAsync(
        IReasoningLayer layer,
        string prompt,
        ReasoningContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            return await layer.ReasonAsync(prompt, context, cancellationToken);
        }
        catch (Exception ex)
        {
            return new ReasoningResult
            {
                Response = $"Layer '{layer.Name}' failed: {ex.Message}",
                Confidence = 0,
                IsValid = false,
                ValidationIssues = new List<string> { ex.Message }
            };
        }
    }

    private CrossValidationResult PerformSimpleCrossValidation(
        List<ReasoningResult> results,
        ReasoningContext context)
    {
        var issues = new List<ValidationIssue>();
        var agreements = new List<string>();
        var disagreements = new List<string>();

        // Check consensus
        var responses = results.Select(r => r.Response.ToLower().Trim()).ToList();
        var uniqueCount = responses.Distinct().Count();

        if (uniqueCount == 1)
        {
            agreements.Add("Perfect consensus - all layers agree");
        }
        else if (uniqueCount == responses.Count)
        {
            disagreements.Add("No consensus - all layers disagree");
            issues.Add(new ValidationIssue
            {
                Type = "NoConsensus",
                Description = "All layers provided different answers",
                Severity = 0.9
            });
        }
        else
        {
            var grouped = responses.GroupBy(r => r).OrderByDescending(g => g.Count());
            var majority = grouped.First();
            var majorityCount = majority.Count();
            var totalCount = responses.Count;

            agreements.Add($"{majorityCount}/{totalCount} layers agree on primary answer");

            if (majorityCount < totalCount * 0.67) // Less than 2/3 agreement
            {
                issues.Add(new ValidationIssue
                {
                    Type = "WeakConsensus",
                    Description = $"Only {majorityCount}/{totalCount} layers agree",
                    Severity = 0.6
                });
            }
        }

        // Confidence variance check
        var confidences = results.Select(r => r.Confidence).ToList();
        var avgConf = confidences.Average();
        var variance = confidences.Select(c => Math.Pow(c - avgConf, 2)).Average();
        var stdDev = Math.Sqrt(variance);

        if (stdDev > 0.2)
        {
            issues.Add(new ValidationIssue
            {
                Type = "HighConfidenceVariance",
                Description = $"High variance in confidence scores (σ={stdDev:F2})",
                Severity = 0.4
            });
        }

        return new CrossValidationResult
        {
            IsValid = issues.Count == 0 || issues.All(i => i.Severity < 0.7),
            Confidence = avgConf,
            Issues = issues,
            Agreements = agreements,
            Disagreements = disagreements,
            ConsensusAnswer = DetermineConsensusAnswer(results),
            LayerResults = results
        };
    }

    private string DetermineConsensusAnswer(List<ReasoningResult> results)
    {
        // Weighted voting based on confidence
        var grouped = results
            .GroupBy(r => r.Response.ToLower().Trim())
            .Select(g => new
            {
                Answer = g.First().Response,
                Count = g.Count(),
                TotalConfidence = g.Sum(r => r.Confidence)
            })
            .OrderByDescending(x => x.TotalConfidence)
            .ThenByDescending(x => x.Count)
            .FirstOrDefault();

        return grouped?.Answer ?? results.First().Response;
    }

    private string DetermineFinalAnswer(NeuroChainResult result, ReasoningContext context)
    {
        // If we have cross-validation, use its consensus
        if (result.CrossValidation != null)
        {
            return result.CrossValidation.ConsensusAnswer;
        }

        // If early stopped, use that result
        if (result.EarlyStopped && result.LayerResults.Count > 0)
        {
            return result.LayerResults.Last().Response;
        }

        // Otherwise, use highest confidence result
        var best = result.LayerResults
            .Where(r => r.IsValid)
            .OrderByDescending(r => r.Confidence)
            .ThenBy(r => r.DurationMs)  // Prefer faster if same confidence
            .FirstOrDefault();

        return best?.Response ?? result.LayerResults.FirstOrDefault()?.Response ?? string.Empty;
    }

    private double DetermineFinalConfidence(NeuroChainResult result, ReasoningContext context)
    {
        if (result.CrossValidation != null)
        {
            return result.CrossValidation.Confidence;
        }

        if (result.EarlyStopped && result.LayerResults.Count > 0)
        {
            return result.LayerResults.Last().Confidence;
        }

        // Weighted average of valid results
        var validResults = result.LayerResults.Where(r => r.IsValid).ToList();
        if (validResults.Count == 0)
        {
            return 0;
        }

        return validResults.Average(r => r.Confidence);
    }

    #endregion
}

/// <summary>
/// Configuration for NeuroChain orchestration
/// </summary>
public class NeuroChainConfig
{
    /// <summary>
    /// Enable parallel execution of layers
    /// </summary>
    public bool ParallelExecution { get; set; } = false;

    /// <summary>
    /// Enable cross-validation between layers
    /// </summary>
    public bool EnableCrossValidation { get; set; } = true;

    /// <summary>
    /// Enable early stopping when confidence threshold is met
    /// </summary>
    public bool EnableEarlyStop { get; set; } = false;

    /// <summary>
    /// Confidence threshold for early stopping (0-1)
    /// </summary>
    public double EarlyStopConfidenceThreshold { get; set; } = 0.95;

    /// <summary>
    /// Minimum number of layers to execute before allowing early stop
    /// </summary>
    public int MinLayersBeforeEarlyStop { get; set; } = 1;
}

/// <summary>
/// Result from NeuroChain multi-layer reasoning
/// </summary>
public class NeuroChainResult
{
    /// <summary>
    /// Original prompt
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Results from each layer
    /// </summary>
    public List<ReasoningResult> LayerResults { get; set; } = new();

    /// <summary>
    /// Cross-validation result
    /// </summary>
    public CrossValidationResult? CrossValidation { get; set; }

    /// <summary>
    /// Final consensus answer
    /// </summary>
    public string FinalAnswer { get; set; } = string.Empty;

    /// <summary>
    /// Final confidence score (0-1)
    /// </summary>
    public double FinalConfidence { get; set; }

    /// <summary>
    /// Whether execution was successful
    /// </summary>
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Error message if unsuccessful
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Total execution time (milliseconds)
    /// </summary>
    public long TotalDurationMs { get; set; }

    /// <summary>
    /// Total cost across all layers
    /// </summary>
    public decimal TotalCost { get; set; }

    /// <summary>
    /// Whether early stopping was triggered
    /// </summary>
    public bool EarlyStopped { get; set; }

    /// <summary>
    /// Reason for early stop
    /// </summary>
    public string? EarlyStopReason { get; set; }

    /// <summary>
    /// Get detailed reasoning breakdown
    /// </summary>
    public string GetDetailedBreakdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Prompt: {Prompt}");
        sb.AppendLine($"Final Answer: {FinalAnswer}");
        sb.AppendLine($"Confidence: {FinalConfidence:P0}");
        sb.AppendLine($"Duration: {TotalDurationMs}ms");
        sb.AppendLine($"Cost: ${TotalCost:F6}");
        sb.AppendLine();

        for (int i = 0; i < LayerResults.Count; i++)
        {
            var layer = LayerResults[i];
            sb.AppendLine($"Layer {i + 1}: {layer.Provider}");
            sb.AppendLine($"  Answer: {layer.Response}");
            sb.AppendLine($"  Confidence: {layer.Confidence:P0}");
            sb.AppendLine($"  Steps: {layer.ReasoningChain.Count}");
            sb.AppendLine($"  Duration: {layer.DurationMs}ms");
            sb.AppendLine($"  Cost: ${layer.Cost:F6}");

            if (layer.Assumptions.Count > 0)
            {
                sb.AppendLine($"  Assumptions: {string.Join(", ", layer.Assumptions.Take(3))}");
            }

            if (layer.Weaknesses.Count > 0)
            {
                sb.AppendLine($"  Weaknesses: {string.Join(", ", layer.Weaknesses.Take(3))}");
            }

            sb.AppendLine();
        }

        if (CrossValidation != null)
        {
            sb.AppendLine("Cross-Validation:");
            sb.AppendLine($"  Valid: {CrossValidation.IsValid}");
            sb.AppendLine($"  Confidence: {CrossValidation.Confidence:P0}");
            sb.AppendLine($"  Issues: {CrossValidation.Issues.Count}");
            if (CrossValidation.Agreements.Count > 0)
            {
                sb.AppendLine($"  Agreements: {string.Join("; ", CrossValidation.Agreements)}");
            }
            if (CrossValidation.Disagreements.Count > 0)
            {
                sb.AppendLine($"  Disagreements: {string.Join("; ", CrossValidation.Disagreements)}");
            }
        }

        return sb.ToString();
    }
}
