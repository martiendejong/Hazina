using System.Diagnostics;
using Hazina.AI.Providers.Core;

namespace Hazina.Neurochain.Core.Layers;

/// <summary>
/// Fast reasoning layer using efficient models (e.g., GPT-4o-mini, Claude Haiku)
/// Provides quick initial reasoning at low cost
/// </summary>
public class FastReasoningLayer : IReasoningLayer
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly string? _preferredProvider;

    public string Name => "Fast Reasoning";
    public LayerType Type => LayerType.Fast;
    public ResponseSpeed Speed => ResponseSpeed.Fast;
    public CostLevel Cost => CostLevel.Low;

    public FastReasoningLayer(IProviderOrchestrator orchestrator, string? preferredProvider = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _preferredProvider = preferredProvider;
    }

    public async Task<ReasoningResult> ReasonAsync(
        string prompt,
        ReasoningContext context,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Build prompt for fast reasoning
            var messages = BuildMessages(prompt, context, isFast: true);

            // Get response
            var costBefore = _orchestrator.GetTotalCost();
            var response = await _orchestrator.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                null,
                null,
                cancellationToken
            );
            var costAfter = _orchestrator.GetTotalCost();

            sw.Stop();

            // Parse reasoning chain from response
            var reasoningChain = ExtractReasoningChain(response.Result);

            return new ReasoningResult
            {
                Response = ExtractFinalAnswer(response.Result),
                Confidence = EstimateConfidence(response.Result),
                ReasoningChain = reasoningChain,
                DurationMs = sw.ElapsedMilliseconds,
                Provider = _preferredProvider ?? "auto",
                Cost = costAfter - costBefore,
                IsValid = true
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ReasoningResult
            {
                Response = $"Error in fast reasoning: {ex.Message}",
                Confidence = 0,
                DurationMs = sw.ElapsedMilliseconds,
                IsValid = false,
                ValidationIssues = new List<string> { ex.Message }
            };
        }
    }

    public async Task<ValidationResult> ValidateAsync(
        ReasoningResult result,
        ReasoningContext context,
        CancellationToken cancellationToken = default)
    {
        // Fast layer can do basic validation
        var issues = new List<ValidationIssue>();

        // Check confidence
        if (result.Confidence < context.MinConfidence)
        {
            issues.Add(new ValidationIssue
            {
                Type = "LowConfidence",
                Description = $"Confidence {result.Confidence:P0} below threshold {context.MinConfidence:P0}",
                Severity = 0.5
            });
        }

        // Check for empty response
        if (string.IsNullOrWhiteSpace(result.Response))
        {
            issues.Add(new ValidationIssue
            {
                Type = "EmptyResponse",
                Description = "Reasoning produced empty response",
                Severity = 1.0
            });
        }

        // Check against ground truth
        foreach (var (key, expectedValue) in context.GroundTruth)
        {
            if (!result.Response.Contains(expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue
                {
                    Type = "GroundTruthMismatch",
                    Description = $"Response does not match ground truth for {key}",
                    Severity = 0.8
                });
            }
        }

        return new ValidationResult
        {
            IsValid = issues.Count == 0 || issues.All(i => i.Severity < 0.7),
            Confidence = issues.Count == 0 ? 0.9 : 0.5,
            Issues = issues
        };
    }

    #region Private Methods

    private List<HazinaChatMessage> BuildMessages(string prompt, ReasoningContext context, bool isFast)
    {
        var messages = new List<HazinaChatMessage>();

        // Add system message
        messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.System,
            Text = isFast
                ? "You are a fast, efficient reasoning system. Provide quick but accurate answers with brief step-by-step reasoning."
                : "You are a thorough reasoning system. Provide detailed step-by-step reasoning."
        });

        // Add history
        messages.AddRange(context.History);

        // Add main prompt with reasoning instructions
        var reasoningPrompt = isFast
            ? $"{prompt}\n\nProvide your answer with brief reasoning steps. Format:\nStep 1: [reasoning]\nStep 2: [reasoning]\n...\nAnswer: [final answer]"
            : $"{prompt}\n\nProvide detailed step-by-step reasoning. Show your work. Format:\nStep 1: [detailed reasoning]\nStep 2: [detailed reasoning]\n...\nAnswer: [final answer with confidence]";

        messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.User,
            Text = reasoningPrompt
        });

        return messages;
    }

    private List<string> ExtractReasoningChain(string response)
    {
        var chain = new List<string>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            // Look for "Step N:" pattern
            if (line.TrimStart().StartsWith("Step ", StringComparison.OrdinalIgnoreCase))
            {
                chain.Add(line.Trim());
            }
        }

        return chain;
    }

    private string ExtractFinalAnswer(string response)
    {
        // Look for "Answer:" pattern
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("Answer:", StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring(line.IndexOf(':') + 1).Trim();
            }
        }

        // If no "Answer:" found, return last non-empty line
        return lines.LastOrDefault()?.Trim() ?? response;
    }

    private double EstimateConfidence(string response)
    {
        // Simple heuristic-based confidence estimation
        double confidence = 0.7; // Base confidence

        // Increase if multiple reasoning steps
        var stepCount = response.Split("Step ", StringSplitOptions.None).Length - 1;
        if (stepCount >= 3) confidence += 0.1;

        // Decrease if hedging language
        if (response.Contains("might", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("possibly", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("not sure", StringComparison.OrdinalIgnoreCase))
        {
            confidence -= 0.2;
        }

        // Increase if confident language
        if (response.Contains("certainly", StringComparison.OrdinalIgnoreCase) ||
            response.Contains("definitely", StringComparison.OrdinalIgnoreCase))
        {
            confidence += 0.1;
        }

        return Math.Clamp(confidence, 0, 1);
    }

    #endregion
}
