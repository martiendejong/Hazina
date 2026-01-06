using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AI.PromptManagement.Dashboard;
using Hazina.AI.PromptManagement.Core;
using Hazina.LLMs.Client;

namespace Hazina.AI.PromptManagement.Reflection;

/// <summary>
/// Reflection engine implementation that analyzes prompt performance and generates improvement hypotheses
/// </summary>
public class ReflectionEngine : IReflectionEngine
{
    private readonly IPatternAnalyzer _patternAnalyzer;
    private readonly IPromptStore _promptStore;
    private readonly IReflectionStore _reflectionStore;
    private readonly ILLMClient _llmClient;

    public ReflectionEngine(
        IPatternAnalyzer patternAnalyzer,
        IPromptStore promptStore,
        IReflectionStore reflectionStore,
        ILLMClient llmClient)
    {
        _patternAnalyzer = patternAnalyzer ?? throw new ArgumentNullException(nameof(patternAnalyzer));
        _promptStore = promptStore ?? throw new ArgumentNullException(nameof(promptStore));
        _reflectionStore = reflectionStore ?? throw new ArgumentNullException(nameof(reflectionStore));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
    }

    public async Task<ReflectionReport> GenerateReportAsync(
        string promptId,
        DateTime startDate,
        DateTime endDate,
        int minRunsRequired = 10,
        CancellationToken cancellationToken = default)
    {
        // Get prompt template for context
        var promptTemplate = await _promptStore.GetAsync(promptId, cancellationToken: cancellationToken);
        if (promptTemplate == null)
        {
            throw new ArgumentException($"Prompt '{promptId}' not found", nameof(promptId));
        }

        // Get current version for analysis
        var currentVersion = await _promptStore.GetVersionAsync(
            promptTemplate.CurrentVersion,
            cancellationToken
        );

        if (currentVersion == null)
        {
            throw new InvalidOperationException($"Current version '{promptTemplate.CurrentVersion}' not found");
        }

        // Get metrics for the period
        var metrics = await _promptStore.GetMetricsAsync(
            promptId,
            promptTemplate.CurrentVersion,
            startDate,
            endDate,
            cancellationToken
        );

        if (metrics == null || metrics.TotalUses < minRunsRequired)
        {
            throw new InvalidOperationException(
                $"Insufficient evaluation runs. Found: {metrics?.TotalUses ?? 0}, Required: {minRunsRequired}"
            );
        }

        // Detect failure patterns
        var failurePatterns = await _patternAnalyzer.DetectFailurePatternsAsync(
            promptId,
            startDate,
            endDate,
            minFrequency: 0.05,  // 5% threshold
            cancellationToken
        );

        // Detect success patterns
        var successPatterns = await _patternAnalyzer.DetectSuccessPatternsAsync(
            promptId,
            startDate,
            endDate,
            minFrequency: 0.1,  // 10% threshold
            cancellationToken
        );

        // Convert patterns to summaries
        var failureSummaries = failurePatterns.Select(fp => new FailurePatternSummary
        {
            PatternId = fp.PatternId,
            Description = fp.Description,
            Frequency = fp.Frequency,
            Occurrences = fp.Occurrences,
            Examples = fp.Examples,
            RootCause = fp.RootCause ?? "Unknown",
            AffectedMetrics = fp.AffectedMetrics,
            Impact = fp.Severity * fp.Frequency  // Combined impact score
        }).ToList();

        var successSummaries = successPatterns.Select(sp => new SuccessPatternSummary
        {
            PatternId = sp.PatternId,
            Description = sp.Description,
            Frequency = sp.Frequency,
            Occurrences = sp.Occurrences,
            Examples = sp.Examples,
            KeyFactors = sp.KeyFactors,
            MetricImpact = sp.MetricImpact
        }).ToList();

        // Generate improvement hypotheses using LLM
        var hypotheses = await GenerateImprovementHypothesesAsync(
            promptTemplate,
            currentVersion,
            failureSummaries,
            successSummaries,
            metrics,
            cancellationToken
        );

        // Generate overall assessment
        var overallAssessment = await GenerateOverallAssessmentAsync(
            promptTemplate,
            metrics,
            failureSummaries,
            successSummaries,
            hypotheses,
            cancellationToken
        );

        // Create reflection report
        var report = new ReflectionReport
        {
            PromptId = promptId,
            StartDate = startDate,
            EndDate = endDate,
            TotalRuns = metrics.TotalUses,
            SuccessRate = metrics.SuccessRate,
            AverageMetrics = metrics.EvalMetrics ?? new Dictionary<string, double>(),
            FailurePatterns = failureSummaries,
            SuccessPatterns = successSummaries,
            ImprovementHypotheses = hypotheses,
            OverallAssessment = overallAssessment,
            Metadata = new Dictionary<string, object>
            {
                { "prompt_name", promptTemplate.Name },
                { "current_version", currentVersion.VersionNumber },
                { "analysis_period_days", (endDate - startDate).TotalDays }
            }
        };

        // Save the report
        await _reflectionStore.SaveReportAsync(report, cancellationToken);

        return report;
    }

    public async Task<ReflectionReport?> GetReportAsync(
        string reportId,
        CancellationToken cancellationToken = default)
    {
        return await _reflectionStore.GetReportAsync(reportId, cancellationToken);
    }

    public async Task<List<ReflectionReport>> GetReportHistoryAsync(
        string promptId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return await _reflectionStore.GetReportHistoryAsync(promptId, limit, cancellationToken);
    }

    public async Task<List<ImprovementHypothesis>> AnalyzePatternsAsync(
        string promptId,
        List<string> patternIds,
        CancellationToken cancellationToken = default)
    {
        // Get the latest report for this prompt
        var latestReport = await _reflectionStore.GetLatestReportAsync(promptId, cancellationToken);

        if (latestReport == null)
        {
            throw new InvalidOperationException($"No reflection reports found for prompt '{promptId}'");
        }

        // Filter patterns by IDs
        var targetPatterns = latestReport.FailurePatterns
            .Where(fp => patternIds.Contains(fp.PatternId))
            .ToList();

        if (!targetPatterns.Any())
        {
            return new List<ImprovementHypothesis>();
        }

        // Get prompt template for context
        var promptTemplate = await _promptStore.GetAsync(promptId, cancellationToken: cancellationToken);
        if (promptTemplate == null)
        {
            throw new ArgumentException($"Prompt '{promptId}' not found", nameof(promptId));
        }

        var currentVersion = await _promptStore.GetVersionAsync(
            promptTemplate.CurrentVersion,
            cancellationToken
        );

        // Generate targeted hypotheses for these specific patterns
        var hypotheses = await GenerateTargetedHypothesesAsync(
            promptTemplate,
            currentVersion!,
            targetPatterns,
            cancellationToken
        );

        return hypotheses;
    }

    // Private helper methods

    private async Task<List<ImprovementHypothesis>> GenerateImprovementHypothesesAsync(
        PromptTemplate promptTemplate,
        PromptVersion currentVersion,
        List<FailurePatternSummary> failurePatterns,
        List<SuccessPatternSummary> successPatterns,
        PromptMetrics metrics,
        CancellationToken cancellationToken)
    {
        if (!failurePatterns.Any())
        {
            // No failures, no hypotheses needed
            return new List<ImprovementHypothesis>();
        }

        // Build analysis prompt for LLM
        var analysisPrompt = BuildHypothesisGenerationPrompt(
            promptTemplate,
            currentVersion,
            failurePatterns,
            successPatterns,
            metrics
        );

        var request = new LLMRequest
        {
            Messages = new[]
            {
                new LLMMessage
                {
                    Role = "user",
                    Content = analysisPrompt
                }
            },
            Temperature = 0.3,  // Low temperature for focused, consistent analysis
            MaxTokens = 2000
        };

        var response = await _llmClient.CompleteAsync(request, cancellationToken);
        var analysisResult = response.Choices[0].Message.Content;

        // Parse LLM response into structured hypotheses
        var hypotheses = ParseHypothesesFromLLMResponse(analysisResult, failurePatterns);

        return hypotheses;
    }

    private async Task<List<ImprovementHypothesis>> GenerateTargetedHypothesesAsync(
        PromptTemplate promptTemplate,
        PromptVersion currentVersion,
        List<FailurePatternSummary> targetPatterns,
        CancellationToken cancellationToken)
    {
        var analysisPrompt = BuildTargetedHypothesisPrompt(
            promptTemplate,
            currentVersion,
            targetPatterns
        );

        var request = new LLMRequest
        {
            Messages = new[]
            {
                new LLMMessage
                {
                    Role = "user",
                    Content = analysisPrompt
                }
            },
            Temperature = 0.3,
            MaxTokens = 1500
        };

        var response = await _llmClient.CompleteAsync(request, cancellationToken);
        var analysisResult = response.Choices[0].Message.Content;

        return ParseHypothesesFromLLMResponse(analysisResult, targetPatterns);
    }

    private async Task<string> GenerateOverallAssessmentAsync(
        PromptTemplate promptTemplate,
        PromptMetrics metrics,
        List<FailurePatternSummary> failurePatterns,
        List<SuccessPatternSummary> successPatterns,
        List<ImprovementHypothesis> hypotheses,
        CancellationToken cancellationToken)
    {
        var assessmentPrompt = BuildOverallAssessmentPrompt(
            promptTemplate,
            metrics,
            failurePatterns,
            successPatterns,
            hypotheses
        );

        var request = new LLMRequest
        {
            Messages = new[]
            {
                new LLMMessage
                {
                    Role = "user",
                    Content = assessmentPrompt
                }
            },
            Temperature = 0.4,
            MaxTokens = 800
        };

        var response = await _llmClient.CompleteAsync(request, cancellationToken);
        return response.Choices[0].Message.Content;
    }

    private string BuildHypothesisGenerationPrompt(
        PromptTemplate promptTemplate,
        PromptVersion currentVersion,
        List<FailurePatternSummary> failurePatterns,
        List<SuccessPatternSummary> successPatterns,
        PromptMetrics metrics)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an expert AI prompt engineer analyzing prompt performance data.");
        sb.AppendLine("Generate specific, actionable improvement hypotheses based on the patterns below.");
        sb.AppendLine();
        sb.AppendLine($"## Prompt Being Analyzed");
        sb.AppendLine($"**Name**: {promptTemplate.Name}");
        sb.AppendLine($"**Category**: {promptTemplate.Category}");
        sb.AppendLine($"**Current Template**:");
        sb.AppendLine("```");
        sb.AppendLine(currentVersion.Template);
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Performance Metrics");
        sb.AppendLine($"- Success Rate: {metrics.SuccessRate:P}");
        sb.AppendLine($"- Total Uses: {metrics.TotalUses}");
        if (metrics.EvalMetrics != null && metrics.EvalMetrics.Any())
        {
            foreach (var (metric, value) in metrics.EvalMetrics.OrderByDescending(kv => kv.Value))
            {
                sb.AppendLine($"- {metric}: {value:F3}");
            }
        }
        sb.AppendLine();

        sb.AppendLine("## Failure Patterns Detected");
        if (failurePatterns.Any())
        {
            foreach (var pattern in failurePatterns.OrderByDescending(p => p.Impact).Take(5))
            {
                sb.AppendLine($"### Pattern: {pattern.Description}");
                sb.AppendLine($"- Frequency: {pattern.Frequency:P} ({pattern.Occurrences} occurrences)");
                sb.AppendLine($"- Impact: {pattern.Impact:F2}");
                sb.AppendLine($"- Root Cause: {pattern.RootCause}");
                sb.AppendLine($"- Affected Metrics: {string.Join(", ", pattern.AffectedMetrics)}");
                if (pattern.Examples.Any())
                {
                    sb.AppendLine("- Examples:");
                    foreach (var example in pattern.Examples.Take(2))
                    {
                        sb.AppendLine($"  - {example}");
                    }
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("No significant failure patterns detected.");
            sb.AppendLine();
        }

        sb.AppendLine("## Success Patterns Detected");
        if (successPatterns.Any())
        {
            foreach (var pattern in successPatterns.OrderByDescending(p => p.Frequency).Take(3))
            {
                sb.AppendLine($"### Pattern: {pattern.Description}");
                sb.AppendLine($"- Frequency: {pattern.Frequency:P} ({pattern.Occurrences} occurrences)");
                sb.AppendLine($"- Key Factors: {string.Join(", ", pattern.KeyFactors)}");
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("No significant success patterns detected.");
            sb.AppendLine();
        }

        sb.AppendLine("## Task");
        sb.AppendLine("Generate 2-5 improvement hypotheses in JSON format. Each hypothesis should:");
        sb.AppendLine("1. Address one or more failure patterns");
        sb.AppendLine("2. Leverage insights from success patterns where applicable");
        sb.AppendLine("3. Propose specific, actionable changes to the prompt template");
        sb.AppendLine("4. Estimate expected impact and confidence");
        sb.AppendLine();
        sb.AppendLine("Return ONLY a valid JSON array with this structure:");
        sb.AppendLine("```json");
        sb.AppendLine("[");
        sb.AppendLine("  {");
        sb.AppendLine("    \"hypothesis\": \"Brief description of what to change\",");
        sb.AppendLine("    \"rationale\": \"Why this will improve performance\",");
        sb.AppendLine("    \"expectedImpact\": 0.15,  // 0.0 to 1.0");
        sb.AppendLine("    \"confidence\": 0.8,  // 0.0 to 1.0");
        sb.AppendLine("    \"priority\": \"high\",  // low|medium|high|critical");
        sb.AppendLine("    \"addressedPatterns\": [\"pattern-id-1\", \"pattern-id-2\"],");
        sb.AppendLine("    \"targetMetrics\": [\"Accuracy\", \"Relevance\"],");
        sb.AppendLine("    \"suggestedChanges\": [");
        sb.AppendLine("      {");
        sb.AppendLine("        \"changeType\": \"Add\",  // Add|Remove|Modify|Restructure");
        sb.AppendLine("        \"section\": \"instructions\",");
        sb.AppendLine("        \"currentValue\": \"existing text or empty\",");
        sb.AppendLine("        \"proposedValue\": \"new text\",");
        sb.AppendLine("        \"reason\": \"explanation\"");
        sb.AppendLine("      }");
        sb.AppendLine("    ]");
        sb.AppendLine("  }");
        sb.AppendLine("]");
        sb.AppendLine("```");

        return sb.ToString();
    }

    private string BuildTargetedHypothesisPrompt(
        PromptTemplate promptTemplate,
        PromptVersion currentVersion,
        List<FailurePatternSummary> targetPatterns)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are an expert AI prompt engineer. Generate targeted improvement hypotheses");
        sb.AppendLine("for the following specific failure patterns.");
        sb.AppendLine();
        sb.AppendLine($"## Prompt Template");
        sb.AppendLine("```");
        sb.AppendLine(currentVersion.Template);
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("## Target Failure Patterns");
        foreach (var pattern in targetPatterns)
        {
            sb.AppendLine($"### {pattern.Description}");
            sb.AppendLine($"- Frequency: {pattern.Frequency:P}");
            sb.AppendLine($"- Root Cause: {pattern.RootCause}");
            sb.AppendLine();
        }

        sb.AppendLine("Generate 1-3 highly specific hypotheses in JSON array format (same structure as before).");

        return sb.ToString();
    }

    private string BuildOverallAssessmentPrompt(
        PromptTemplate promptTemplate,
        PromptMetrics metrics,
        List<FailurePatternSummary> failurePatterns,
        List<SuccessPatternSummary> successPatterns,
        List<ImprovementHypothesis> hypotheses)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Provide a concise overall assessment (2-3 paragraphs) of this prompt's performance:");
        sb.AppendLine();
        sb.AppendLine($"**Prompt**: {promptTemplate.Name}");
        sb.AppendLine($"**Success Rate**: {metrics.SuccessRate:P}");
        sb.AppendLine($"**Failure Patterns**: {failurePatterns.Count}");
        sb.AppendLine($"**Success Patterns**: {successPatterns.Count}");
        sb.AppendLine($"**Improvement Hypotheses Generated**: {hypotheses.Count}");
        sb.AppendLine();
        sb.AppendLine("Focus on:");
        sb.AppendLine("1. Overall performance state (good/needs improvement/critical)");
        sb.AppendLine("2. Most impactful failure patterns");
        sb.AppendLine("3. Recommended next steps");

        return sb.ToString();
    }

    private List<ImprovementHypothesis> ParseHypothesesFromLLMResponse(
        string llmResponse,
        List<FailurePatternSummary> failurePatterns)
    {
        try
        {
            // Extract JSON from response (handle code blocks)
            var jsonStart = llmResponse.IndexOf('[');
            var jsonEnd = llmResponse.LastIndexOf(']');

            if (jsonStart == -1 || jsonEnd == -1)
            {
                // Fallback: create basic hypothesis
                return CreateFallbackHypotheses(failurePatterns);
            }

            var jsonContent = llmResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);

            var hypotheses = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(jsonContent);

            if (hypotheses == null || !hypotheses.Any())
            {
                return CreateFallbackHypotheses(failurePatterns);
            }

            return hypotheses.Select(h => new ImprovementHypothesis
            {
                Hypothesis = h.ContainsKey("hypothesis") ? h["hypothesis"].GetString() ?? "" : "",
                Rationale = h.ContainsKey("rationale") ? h["rationale"].GetString() ?? "" : "",
                ExpectedImpact = h.ContainsKey("expectedImpact") ? h["expectedImpact"].GetDouble() : 0.1,
                Confidence = h.ContainsKey("confidence") ? h["confidence"].GetDouble() : 0.5,
                Priority = h.ContainsKey("priority") ? h["priority"].GetString() ?? "medium" : "medium",
                AddressedPatterns = h.ContainsKey("addressedPatterns")
                    ? JsonSerializer.Deserialize<List<string>>(h["addressedPatterns"].GetRawText()) ?? new List<string>()
                    : new List<string>(),
                TargetMetrics = h.ContainsKey("targetMetrics")
                    ? JsonSerializer.Deserialize<List<string>>(h["targetMetrics"].GetRawText()) ?? new List<string>()
                    : new List<string>(),
                SuggestedChanges = h.ContainsKey("suggestedChanges")
                    ? ParseSuggestedChanges(h["suggestedChanges"])
                    : new List<SuggestedChange>()
            }).ToList();
        }
        catch (JsonException)
        {
            // JSON parsing failed, return fallback
            return CreateFallbackHypotheses(failurePatterns);
        }
    }

    private List<SuggestedChange> ParseSuggestedChanges(JsonElement changesElement)
    {
        try
        {
            var changes = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(changesElement.GetRawText());

            if (changes == null)
            {
                return new List<SuggestedChange>();
            }

            return changes.Select(c => new SuggestedChange
            {
                ChangeType = c.ContainsKey("changeType") ? c["changeType"].GetString() ?? "" : "",
                Section = c.ContainsKey("section") ? c["section"].GetString() ?? "" : "",
                CurrentValue = c.ContainsKey("currentValue") ? c["currentValue"].GetString() ?? "" : "",
                ProposedValue = c.ContainsKey("proposedValue") ? c["proposedValue"].GetString() ?? "" : "",
                Reason = c.ContainsKey("reason") ? c["reason"].GetString() ?? "" : ""
            }).ToList();
        }
        catch
        {
            return new List<SuggestedChange>();
        }
    }

    private List<ImprovementHypothesis> CreateFallbackHypotheses(List<FailurePatternSummary> failurePatterns)
    {
        // Create basic hypotheses based on pattern descriptions
        return failurePatterns.Take(3).Select(fp => new ImprovementHypothesis
        {
            Hypothesis = $"Address {fp.Description.ToLower()}",
            Rationale = $"Pattern affects {fp.Frequency:P} of cases and impacts: {string.Join(", ", fp.AffectedMetrics)}",
            ExpectedImpact = Math.Min(fp.Impact, 0.3),
            Confidence = 0.5,
            Priority = fp.Impact > 0.5 ? "high" : "medium",
            AddressedPatterns = new List<string> { fp.PatternId },
            TargetMetrics = fp.AffectedMetrics,
            SuggestedChanges = new List<SuggestedChange>
            {
                new SuggestedChange
                {
                    ChangeType = "Modify",
                    Section = "instructions",
                    CurrentValue = "",
                    ProposedValue = $"Add instructions to handle {fp.Description.ToLower()}",
                    Reason = fp.RootCause
                }
            }
        }).ToList();
    }
}
