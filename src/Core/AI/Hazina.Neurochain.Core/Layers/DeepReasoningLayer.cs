using System.Diagnostics;
using System.Text.RegularExpressions;
using Hazina.AI.Providers.Core;

namespace Hazina.Neurochain.Core.Layers;

/// <summary>
/// Deep reasoning layer using powerful models (e.g., GPT-4, Claude Opus)
/// Provides thorough, detailed reasoning with explicit assumptions and evidence
/// </summary>
public class DeepReasoningLayer : IReasoningLayer
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly string? _preferredProvider;

    public string Name => "Deep Reasoning";
    public LayerType Type => LayerType.Deep;
    public ResponseSpeed Speed => ResponseSpeed.Medium;
    public CostLevel Cost => CostLevel.Medium;

    public DeepReasoningLayer(IProviderOrchestrator orchestrator, string? preferredProvider = null)
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
            // Build prompt for deep reasoning
            var messages = BuildDeepReasoningMessages(prompt, context);

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

            // Parse structured reasoning from response
            var (reasoningChain, evidence, assumptions, weaknesses, finalAnswer, confidence) = ParseDeepReasoning(response.Result);

            return new ReasoningResult
            {
                Response = finalAnswer,
                Confidence = confidence,
                ReasoningChain = reasoningChain,
                Evidence = evidence,
                Assumptions = assumptions,
                Weaknesses = weaknesses,
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
                Response = $"Error in deep reasoning: {ex.Message}",
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
        var issues = new List<ValidationIssue>();

        // Check confidence threshold
        if (result.Confidence < context.MinConfidence)
        {
            issues.Add(new ValidationIssue
            {
                Type = "LowConfidence",
                Description = $"Confidence {result.Confidence:P0} below threshold {context.MinConfidence:P0}",
                Severity = 0.7
            });
        }

        // Check reasoning depth
        if (result.ReasoningChain.Count < 3)
        {
            issues.Add(new ValidationIssue
            {
                Type = "ShallowReasoning",
                Description = "Reasoning chain has fewer than 3 steps",
                Severity = 0.6
            });
        }

        // Check for assumptions
        if (result.Assumptions.Count == 0)
        {
            issues.Add(new ValidationIssue
            {
                Type = "NoAssumptions",
                Description = "No assumptions identified (may be overlooked)",
                Severity = 0.3
            });
        }

        // Check for evidence
        if (result.Evidence.Count == 0)
        {
            issues.Add(new ValidationIssue
            {
                Type = "NoEvidence",
                Description = "No supporting evidence provided",
                Severity = 0.5
            });
        }

        // Check ground truth
        foreach (var (key, expectedValue) in context.GroundTruth)
        {
            if (!result.Response.Contains(expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(new ValidationIssue
                {
                    Type = "GroundTruthMismatch",
                    Description = $"Response contradicts ground truth for {key}",
                    Severity = 0.9
                });
            }
        }

        // Check for logical consistency
        var consistencyIssues = CheckLogicalConsistency(result);
        issues.AddRange(consistencyIssues);

        var highSeverityIssues = issues.Where(i => i.Severity >= 0.7).ToList();

        return new ValidationResult
        {
            IsValid = highSeverityIssues.Count == 0,
            Confidence = highSeverityIssues.Count == 0 ? 0.85 : 0.4,
            Issues = issues,
            Suggestions = GenerateSuggestions(issues)
        };
    }

    #region Private Methods

    private List<HazinaChatMessage> BuildDeepReasoningMessages(string prompt, ReasoningContext context)
    {
        var messages = new List<HazinaChatMessage>();

        // Add system message for deep reasoning
        messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.System,
            Text = @"You are an expert reasoning system that provides thorough, structured analysis.

For each problem:
1. Break it down into clear reasoning steps
2. Identify and state your assumptions
3. Provide supporting evidence
4. Acknowledge potential weaknesses or limitations
5. Give a final answer with confidence level

Format your response as:

REASONING:
Step 1: [detailed reasoning]
Step 2: [detailed reasoning]
...

ASSUMPTIONS:
- [assumption 1]
- [assumption 2]
...

EVIDENCE:
- [evidence 1]
- [evidence 2]
...

WEAKNESSES:
- [potential weakness 1]
- [potential weakness 2]
...

ANSWER: [final answer]
CONFIDENCE: [0-100%]"
        });

        // Add history
        messages.AddRange(context.History);

        // Add domain context if provided
        if (!string.IsNullOrEmpty(context.Domain))
        {
            messages.Add(new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = $"Domain context: {context.Domain}"
            });
        }

        // Add main prompt
        messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.User,
            Text = prompt
        });

        return messages;
    }

    private (List<string> reasoningChain, List<string> evidence, List<string> assumptions, List<string> weaknesses, string finalAnswer, double confidence) ParseDeepReasoning(string response)
    {
        var reasoningChain = new List<string>();
        var evidence = new List<string>();
        var assumptions = new List<string>();
        var weaknesses = new List<string>();
        var finalAnswer = "";
        var confidence = 0.8; // Default

        var lines = response.Split('\n');
        string? currentSection = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Detect sections
            if (trimmedLine.StartsWith("REASONING:", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "reasoning";
                continue;
            }
            else if (trimmedLine.StartsWith("ASSUMPTIONS:", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "assumptions";
                continue;
            }
            else if (trimmedLine.StartsWith("EVIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "evidence";
                continue;
            }
            else if (trimmedLine.StartsWith("WEAKNESSES:", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "weaknesses";
                continue;
            }
            else if (trimmedLine.StartsWith("ANSWER:", StringComparison.OrdinalIgnoreCase))
            {
                finalAnswer = trimmedLine.Substring("ANSWER:".Length).Trim();
                currentSection = null;
                continue;
            }
            else if (trimmedLine.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                var confStr = trimmedLine.Substring("CONFIDENCE:".Length).Trim().TrimEnd('%');
                if (double.TryParse(confStr, out var conf))
                {
                    confidence = conf / 100.0; // Convert percentage to 0-1
                }
                currentSection = null;
                continue;
            }

            // Add to current section
            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

            switch (currentSection)
            {
                case "reasoning":
                    if (trimmedLine.StartsWith("Step ", StringComparison.OrdinalIgnoreCase) || trimmedLine.StartsWith("-"))
                    {
                        reasoningChain.Add(trimmedLine.TrimStart('-', ' '));
                    }
                    break;
                case "assumptions":
                    if (trimmedLine.StartsWith("-"))
                    {
                        assumptions.Add(trimmedLine.TrimStart('-', ' '));
                    }
                    break;
                case "evidence":
                    if (trimmedLine.StartsWith("-"))
                    {
                        evidence.Add(trimmedLine.TrimStart('-', ' '));
                    }
                    break;
                case "weaknesses":
                    if (trimmedLine.StartsWith("-"))
                    {
                        weaknesses.Add(trimmedLine.TrimStart('-', ' '));
                    }
                    break;
            }
        }

        // Fallback: if no structured format, extract what we can
        if (reasoningChain.Count == 0)
        {
            reasoningChain = ExtractSteps(response);
        }
        if (string.IsNullOrEmpty(finalAnswer))
        {
            finalAnswer = ExtractAnswer(response);
        }

        return (reasoningChain, evidence, assumptions, weaknesses, finalAnswer, confidence);
    }

    private List<string> ExtractSteps(string response)
    {
        var steps = new List<string>();
        var matches = Regex.Matches(response, @"Step \d+:.*", RegexOptions.Multiline);
        foreach (Match match in matches)
        {
            steps.Add(match.Value.Trim());
        }
        return steps;
    }

    private string ExtractAnswer(string response)
    {
        // Look for "Answer:" or last substantial line
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("Answer:", StringComparison.OrdinalIgnoreCase) ||
                line.TrimStart().StartsWith("ANSWER:", StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring(line.IndexOf(':') + 1).Trim();
            }
        }
        return lines.LastOrDefault()?.Trim() ?? response;
    }

    private List<ValidationIssue> CheckLogicalConsistency(ReasoningResult result)
    {
        var issues = new List<ValidationIssue>();

        // Check for contradictions in reasoning chain
        for (int i = 0; i < result.ReasoningChain.Count; i++)
        {
            for (int j = i + 1; j < result.ReasoningChain.Count; j++)
            {
                if (HasContradiction(result.ReasoningChain[i], result.ReasoningChain[j]))
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = "Contradiction",
                        Description = $"Step {i + 1} may contradict Step {j + 1}",
                        Severity = 0.8,
                        StepIndex = i
                    });
                }
            }
        }

        return issues;
    }

    private bool HasContradiction(string step1, string step2)
    {
        // Simple contradiction detection using negation words
        var negationWords = new[] { "not", "never", "no", "cannot", "impossible", "false" };

        var words1 = step1.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = step2.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Check if one has negation and the other doesn't for same concepts
        var hasNeg1 = words1.Any(w => negationWords.Contains(w));
        var hasNeg2 = words2.Any(w => negationWords.Contains(w));

        if (hasNeg1 != hasNeg2)
        {
            // Check for common significant words
            var commonWords = words1.Intersect(words2).Where(w => w.Length > 4).ToList();
            return commonWords.Count >= 2; // If 2+ common words and different negation, might be contradiction
        }

        return false;
    }

    private List<string> GenerateSuggestions(List<ValidationIssue> issues)
    {
        var suggestions = new List<string>();

        foreach (var issue in issues)
        {
            switch (issue.Type)
            {
                case "ShallowReasoning":
                    suggestions.Add("Break down the problem into more detailed steps");
                    break;
                case "NoAssumptions":
                    suggestions.Add("Explicitly state any assumptions made in the reasoning");
                    break;
                case "NoEvidence":
                    suggestions.Add("Provide supporting evidence or citations");
                    break;
                case "Contradiction":
                    suggestions.Add($"Review step {issue.StepIndex + 1} for logical consistency");
                    break;
                case "GroundTruthMismatch":
                    suggestions.Add("Verify answer against known facts");
                    break;
            }
        }

        return suggestions.Distinct().ToList();
    }

    #endregion
}
