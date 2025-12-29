using System.Diagnostics;
using System.Text;
using Hazina.AI.Providers.Core;

namespace Hazina.Neurochain.Core.Layers;

/// <summary>
/// Verification layer for cross-validating reasoning from other layers
/// Uses different models to provide independent verification
/// </summary>
public class VerificationLayer : IReasoningLayer
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly string? _preferredProvider;

    public string Name => "Verification";
    public LayerType Type => LayerType.Verification;
    public ResponseSpeed Speed => ResponseSpeed.Medium;
    public CostLevel Cost => CostLevel.Medium;

    public VerificationLayer(IProviderOrchestrator orchestrator, string? preferredProvider = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _preferredProvider = preferredProvider;
    }

    public async Task<ReasoningResult> ReasonAsync(
        string prompt,
        ReasoningContext context,
        CancellationToken cancellationToken = default)
    {
        // Verification layer doesn't do primary reasoning, it validates other results
        // If called for primary reasoning, delegate to fast layer
        var sw = Stopwatch.StartNew();

        try
        {
            var messages = BuildVerificationMessages(prompt, context);

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

            return new ReasoningResult
            {
                Response = response.Result,
                Confidence = 0.7, // Moderate confidence for independent reasoning
                ReasoningChain = new List<string> { "Independent verification reasoning" },
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
                Response = $"Error in verification: {ex.Message}",
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
        try
        {
            var messages = BuildCrossValidationMessages(result, context);

            var response = await _orchestrator.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                null,
                null,
                cancellationToken
            );

            // Parse validation response
            return ParseValidationResponse(response.Result, result);
        }
        catch (Exception ex)
        {
            return new ValidationResult
            {
                IsValid = false,
                Confidence = 0,
                Issues = new List<ValidationIssue>
                {
                    new ValidationIssue
                    {
                        Type = "VerificationError",
                        Description = ex.Message,
                        Severity = 0.5
                    }
                }
            };
        }
    }

    /// <summary>
    /// Cross-validate multiple reasoning results
    /// </summary>
    public async Task<CrossValidationResult> CrossValidateAsync(
        List<ReasoningResult> results,
        ReasoningContext context,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<ValidationIssue>();
        var agreements = new List<string>();
        var disagreements = new List<string>();

        // Check for consensus
        var responses = results.Select(r => r.Response.ToLower()).ToList();
        var uniqueResponses = responses.Distinct().Count();

        if (uniqueResponses == 1)
        {
            agreements.Add("All layers agree on the answer");
        }
        else if (uniqueResponses == responses.Count)
        {
            disagreements.Add("All layers provided different answers");
            issues.Add(new ValidationIssue
            {
                Type = "NoConsensus",
                Description = "No agreement between layers",
                Severity = 0.9
            });
        }
        else
        {
            // Partial agreement
            var grouped = responses.GroupBy(r => r).OrderByDescending(g => g.Count());
            var majority = grouped.First();
            var minorityCount = grouped.Count() - 1;

            agreements.Add($"{majority.Count()} layers agree on the primary answer");
            if (minorityCount > 0)
            {
                disagreements.Add($"{minorityCount} layer(s) provided alternative answers");
                issues.Add(new ValidationIssue
                {
                    Type = "PartialConsensus",
                    Description = $"Only {majority.Count()}/{responses.Count} layers agree",
                    Severity = 0.5
                });
            }
        }

        // Check confidence scores
        var avgConfidence = results.Average(r => r.Confidence);
        var minConfidence = results.Min(r => r.Confidence);

        if (minConfidence < context.MinConfidence)
        {
            issues.Add(new ValidationIssue
            {
                Type = "LowConfidence",
                Description = $"At least one layer has confidence below threshold ({minConfidence:P0} < {context.MinConfidence:P0})",
                Severity = 0.6
            });
        }

        // Check reasoning depth variance
        var reasoningDepths = results.Select(r => r.ReasoningChain.Count).ToList();
        var avgDepth = reasoningDepths.Average();
        var minDepth = reasoningDepths.Min();

        if (minDepth < avgDepth / 2)
        {
            issues.Add(new ValidationIssue
            {
                Type = "ReasoningDepthVariance",
                Description = "Significant variance in reasoning depth between layers",
                Severity = 0.4
            });
        }

        // Perform AI-based cross-validation
        var aiValidation = await PerformAICrossValidation(results, context, cancellationToken);
        issues.AddRange(aiValidation.Issues);

        return new CrossValidationResult
        {
            IsValid = issues.Count == 0 || issues.All(i => i.Severity < 0.7),
            Confidence = avgConfidence,
            Issues = issues,
            Agreements = agreements,
            Disagreements = disagreements,
            ConsensusAnswer = DetermineConsensusAnswer(results),
            LayerResults = results
        };
    }

    #region Private Methods

    private List<HazinaChatMessage> BuildVerificationMessages(string prompt, ReasoningContext context)
    {
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = "You are a verification and validation system. Provide independent reasoning and check for logical flaws."
            }
        };

        messages.AddRange(context.History);

        messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.User,
            Text = prompt
        });

        return messages;
    }

    private List<HazinaChatMessage> BuildCrossValidationMessages(ReasoningResult result, ReasoningContext context)
    {
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = @"You are a critical validation system. Review the provided reasoning and identify any issues.

Check for:
1. Logical consistency
2. Valid assumptions
3. Sufficient evidence
4. Potential flaws or weaknesses
5. Contradictions

Respond in this format:
VALID: [yes/no]
CONFIDENCE: [0-100%]
ISSUES:
- [issue 1]
- [issue 2]
...
SUGGESTIONS:
- [suggestion 1]
- [suggestion 2]
..."
            }
        };

        // Build validation prompt
        var sb = new StringBuilder();
        sb.AppendLine("Please validate this reasoning:");
        sb.AppendLine();
        sb.AppendLine($"Answer: {result.Response}");
        sb.AppendLine($"Confidence: {result.Confidence:P0}");

        if (result.ReasoningChain.Count > 0)
        {
            sb.AppendLine("\nReasoning Steps:");
            for (int i = 0; i < result.ReasoningChain.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {result.ReasoningChain[i]}");
            }
        }

        if (result.Assumptions.Count > 0)
        {
            sb.AppendLine("\nAssumptions:");
            foreach (var assumption in result.Assumptions)
            {
                sb.AppendLine($"- {assumption}");
            }
        }

        messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.User,
            Text = sb.ToString()
        });

        return messages;
    }

    private ValidationResult ParseValidationResponse(string response, ReasoningResult originalResult)
    {
        var issues = new List<ValidationIssue>();
        var suggestions = new List<string>();
        var isValid = true;
        var confidence = 0.8;

        var lines = response.Split('\n');
        string? currentSection = null;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            if (trimmedLine.StartsWith("VALID:", StringComparison.OrdinalIgnoreCase))
            {
                var value = trimmedLine.Substring("VALID:".Length).Trim().ToLower();
                isValid = value.Contains("yes") || value.Contains("true");
                continue;
            }
            else if (trimmedLine.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                var confStr = trimmedLine.Substring("CONFIDENCE:".Length).Trim().TrimEnd('%');
                if (double.TryParse(confStr, out var conf))
                {
                    confidence = conf / 100.0;
                }
                continue;
            }
            else if (trimmedLine.StartsWith("ISSUES:", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "issues";
                continue;
            }
            else if (trimmedLine.StartsWith("SUGGESTIONS:", StringComparison.OrdinalIgnoreCase))
            {
                currentSection = "suggestions";
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmedLine)) continue;

            if (currentSection == "issues" && trimmedLine.StartsWith("-"))
            {
                var issueText = trimmedLine.TrimStart('-', ' ');
                issues.Add(new ValidationIssue
                {
                    Type = "CrossValidation",
                    Description = issueText,
                    Severity = isValid ? 0.3 : 0.8
                });
            }
            else if (currentSection == "suggestions" && trimmedLine.StartsWith("-"))
            {
                suggestions.Add(trimmedLine.TrimStart('-', ' '));
            }
        }

        return new ValidationResult
        {
            IsValid = isValid,
            Confidence = confidence,
            Issues = issues,
            Suggestions = suggestions
        };
    }

    private async Task<ValidationResult> PerformAICrossValidation(
        List<ReasoningResult> results,
        ReasoningContext context,
        CancellationToken cancellationToken)
    {
        // Build prompt comparing all results
        var sb = new StringBuilder();
        sb.AppendLine("Compare and validate these reasoning results from multiple AI systems:");
        sb.AppendLine();

        for (int i = 0; i < results.Count; i++)
        {
            sb.AppendLine($"Layer {i + 1} ({results[i].Provider}):");
            sb.AppendLine($"Answer: {results[i].Response}");
            sb.AppendLine($"Confidence: {results[i].Confidence:P0}");
            if (results[i].ReasoningChain.Count > 0)
            {
                sb.AppendLine($"Steps: {string.Join(" → ", results[i].ReasoningChain.Take(3))}...");
            }
            sb.AppendLine();
        }

        sb.AppendLine("Identify any logical inconsistencies, contradictions, or concerns.");

        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = "You are a meta-validator comparing multiple reasoning chains. Identify inconsistencies and determine the most reliable answer."
            },
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = sb.ToString()
            }
        };

        var response = await _orchestrator.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            null,
            null,
            cancellationToken
        );

        return ParseValidationResponse(response.Result, results.First());
    }

    private string DetermineConsensusAnswer(List<ReasoningResult> results)
    {
        // Find the most common answer
        var grouped = results
            .GroupBy(r => r.Response.ToLower().Trim())
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Average(r => r.Confidence))
            .FirstOrDefault();

        return grouped?.First().Response ?? results.First().Response;
    }

    #endregion
}

/// <summary>
/// Result of cross-validation
/// </summary>
public class CrossValidationResult
{
    public bool IsValid { get; set; }
    public double Confidence { get; set; }
    public List<ValidationIssue> Issues { get; set; } = new();
    public List<string> Agreements { get; set; } = new();
    public List<string> Disagreements { get; set; } = new();
    public string ConsensusAnswer { get; set; } = string.Empty;
    public List<ReasoningResult> LayerResults { get; set; } = new();
}
