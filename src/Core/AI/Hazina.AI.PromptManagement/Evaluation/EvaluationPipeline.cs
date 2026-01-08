using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AI.PromptManagement.Core;
using Hazina.LLMs;

namespace Hazina.AI.PromptManagement.Evaluation;

/// <summary>
/// Complete evaluation pipeline with scheduling and regression detection
/// </summary>
public class EvaluationPipeline : IEvaluationPipeline
{
    private readonly IPromptStore _promptStore;
    private readonly IEvaluationStore _evaluationStore;
    private readonly ILLMClient _llmClient;
    private readonly IQualityRubricFactory _rubricFactory;
    private readonly EvaluationConfig _config;

    public EvaluationPipeline(
        IPromptStore promptStore,
        IEvaluationStore evaluationStore,
        ILLMClient llmClient,
        IQualityRubricFactory rubricFactory,
        EvaluationConfig? config = null)
    {
        _promptStore = promptStore ?? throw new ArgumentNullException(nameof(promptStore));
        _evaluationStore = evaluationStore ?? throw new ArgumentNullException(nameof(evaluationStore));
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _rubricFactory = rubricFactory ?? throw new ArgumentNullException(nameof(rubricFactory));
        _config = config ?? new EvaluationConfig();
    }

    public async Task<EvaluationRunResult> RunAsync(
        string promptId,
        string testSetId,
        string? versionId = null,
        CancellationToken cancellationToken = default)
    {
        var runId = Guid.NewGuid().ToString();
        var startTime = DateTime.UtcNow;

        try
        {
            // Get prompt template
            var template = await _promptStore.GetAsync(promptId, versionId, cancellationToken);
            if (template == null)
            {
                throw new InvalidOperationException($"Prompt '{promptId}' not found");
            }

            versionId ??= template.CurrentVersion;

            // Get test set
            var testSet = await _evaluationStore.GetTestSetAsync(testSetId, cancellationToken);
            if (testSet == null)
            {
                throw new InvalidOperationException($"Test set '{testSetId}' not found");
            }

            // Create run record
            var run = new EvaluationRunResult
            {
                RunId = runId,
                PromptId = promptId,
                VersionId = versionId,
                TestSetId = testSetId,
                StartedAt = startTime,
                Status = "running",
                CaseResults = new List<EvaluationCaseResult>()
            };

            await _evaluationStore.SaveRunAsync(run, cancellationToken);

            // Execute test cases
            var caseResults = new List<EvaluationCaseResult>();

            foreach (var testCase in testSet.Cases)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var caseResult = await EvaluateTestCaseAsync(
                    template,
                    versionId,
                    testCase,
                    testSet.Rubrics,
                    cancellationToken);

                caseResults.Add(caseResult);

                // Update run incrementally
                run.CaseResults = caseResults;
                run.Metrics = CalculateAggregateMetrics(caseResults);
                await _evaluationStore.SaveRunAsync(run, cancellationToken);
            }

            // Finalize run
            run.CompletedAt = DateTime.UtcNow;
            run.Status = "completed";
            run.Metrics = CalculateAggregateMetrics(caseResults);

            await _evaluationStore.SaveRunAsync(run, cancellationToken);

            // Update prompt metrics
            await _promptStore.UpdateEvalMetricsAsync(
                promptId,
                versionId,
                run.Metrics!,
                cancellationToken);

            return run;
        }
        catch (Exception ex)
        {
            // Mark run as failed
            var failedRun = new EvaluationRunResult
            {
                RunId = runId,
                PromptId = promptId,
                VersionId = versionId ?? "unknown",
                TestSetId = testSetId,
                StartedAt = startTime,
                CompletedAt = DateTime.UtcNow,
                Status = "failed",
                Errors = ex.Message
            };

            await _evaluationStore.SaveRunAsync(failedRun, cancellationToken);

            throw;
        }
    }

    private async Task<EvaluationCaseResult> EvaluateTestCaseAsync(
        Core.Models.PromptTemplate template,
        string versionId,
        TestCase testCase,
        List<string>? rubricNames,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Render prompt with test case variables
            var renderedPrompt = await _promptStore.RenderAsync(
                template.Id,
                testCase.Variables ?? new Dictionary<string, object>(),
                versionId,
                cancellationToken);

            // Get LLM response
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage(HazinaMessageRole.System, renderedPrompt),
                new HazinaChatMessage(HazinaMessageRole.User, testCase.Query)
            };

            var response = await _llmClient.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);
            var responseText = response.Result;

            // Evaluate with rubrics
            var scores = new Dictionary<string, double>();

            if (rubricNames != null && rubricNames.Any())
            {
                foreach (var rubricName in rubricNames)
                {
                    var rubric = _rubricFactory.GetRubric(rubricName);

                    var context = new EvaluationContext
                    {
                        Query = testCase.Query,
                        Response = responseText,
                        ExpectedOutput = testCase.ExpectedOutput,
                        Metadata = testCase.Metadata
                    };

                    var rubricScore = await rubric.EvaluateAsync(context, cancellationToken);
                    scores[rubricName] = rubricScore.Score;

                    if (rubricScore.Breakdown != null)
                    {
                        foreach (var kvp in rubricScore.Breakdown)
                        {
                            scores[$"{rubricName}_{kvp.Key}"] = kvp.Value;
                        }
                    }
                }
            }

            // Calculate overall score
            scores["Overall"] = scores.Any() ? scores.Values.Average() : 0.0;

            sw.Stop();

            return new EvaluationCaseResult
            {
                CaseId = testCase.Id,
                Query = testCase.Query,
                Response = responseText,
                ExpectedOutput = testCase.ExpectedOutput,
                Scores = scores,
                Duration = sw.Elapsed,
                Metadata = new Dictionary<string, object>
                {
                    { "tokens_used", (response.TokenUsage?.InputTokens ?? 0) + (response.TokenUsage?.OutputTokens ?? 0) },
                    { "model", "unknown" }
                }
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException or OperationCanceledException)
        {
            sw.Stop();

            return new EvaluationCaseResult
            {
                CaseId = testCase.Id,
                Query = testCase.Query,
                Response = $"ERROR: {ex.Message}",
                Scores = new Dictionary<string, double> { { "Overall", 0.0 } },
                Duration = sw.Elapsed,
                Metadata = new Dictionary<string, object> { { "error", ex.Message } }
            };
        }
    }

    private static Dictionary<string, double> CalculateAggregateMetrics(List<EvaluationCaseResult> caseResults)
    {
        if (!caseResults.Any())
            return new Dictionary<string, double>();

        var metrics = new Dictionary<string, double>();

        // Get all unique score keys
        var allKeys = caseResults
            .SelectMany(r => r.Scores.Keys)
            .Distinct()
            .ToList();

        // Calculate average for each metric
        foreach (var key in allKeys)
        {
            var values = caseResults
                .Where(r => r.Scores.ContainsKey(key))
                .Select(r => r.Scores[key])
                .ToList();

            if (values.Any())
            {
                metrics[$"avg_{key}"] = values.Average();
                metrics[$"min_{key}"] = values.Min();
                metrics[$"max_{key}"] = values.Max();
            }
        }

        // Add aggregated stats
        metrics["total_cases"] = caseResults.Count;
        metrics["avg_duration_ms"] = caseResults.Average(r => r.Duration.TotalMilliseconds);

        return metrics;
    }

    public async Task<string> ScheduleAsync(
        string promptId,
        string testSetId,
        string cronExpression,
        CancellationToken cancellationToken = default)
    {
        var schedule = new EvaluationSchedule
        {
            ScheduleId = Guid.NewGuid().ToString(),
            PromptId = promptId,
            TestSetId = testSetId,
            CronExpression = cronExpression,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            NextRunAt = CalculateNextRun(cronExpression, DateTime.UtcNow)
        };

        await _evaluationStore.SaveScheduleAsync(schedule, cancellationToken);

        return schedule.ScheduleId;
    }

    public async Task CancelScheduleAsync(
        string scheduleId,
        CancellationToken cancellationToken = default)
    {
        var schedule = await _evaluationStore.GetScheduleAsync(scheduleId, cancellationToken);
        if (schedule != null)
        {
            schedule.IsActive = false;
            await _evaluationStore.SaveScheduleAsync(schedule, cancellationToken);
        }
    }

    public async Task<List<EvaluationSchedule>> GetSchedulesAsync(
        string? promptId = null,
        CancellationToken cancellationToken = default)
    {
        return await _evaluationStore.GetSchedulesAsync(promptId, cancellationToken);
    }

    public async Task<RegressionReport> DetectRegressionsAsync(
        string promptId,
        string baselineVersionId,
        string newVersionId,
        string testSetId,
        CancellationToken cancellationToken = default)
    {
        // Run evaluation on baseline
        var baselineRun = await RunAsync(promptId, testSetId, baselineVersionId, cancellationToken);

        // Run evaluation on new version
        var newRun = await RunAsync(promptId, testSetId, newVersionId, cancellationToken);

        // Compare metrics
        var report = new RegressionReport
        {
            ReportId = Guid.NewGuid().ToString(),
            PromptId = promptId,
            BaselineVersionId = baselineVersionId,
            NewVersionId = newVersionId,
            CreatedAt = DateTime.UtcNow,
            BaselineMetrics = baselineRun.Metrics ?? new Dictionary<string, double>(),
            NewMetrics = newRun.Metrics ?? new Dictionary<string, double>(),
            Changes = new Dictionary<string, double>(),
            Issues = new List<RegressionIssue>()
        };

        // Calculate changes and detect regressions
        foreach (var metric in report.BaselineMetrics.Keys)
        {
            if (report.NewMetrics.ContainsKey(metric))
            {
                var baselineValue = report.BaselineMetrics[metric];
                var newValue = report.NewMetrics[metric];

                if (baselineValue > 0)
                {
                    var percentChange = ((newValue - baselineValue) / baselineValue) * 100;
                    report.Changes[metric] = percentChange;

                    // Check for regression (negative change beyond threshold)
                    if (percentChange < -_config.RegressionThresholdPercent)
                    {
                        var severity = CalculateSeverity(percentChange);

                        report.Issues.Add(new RegressionIssue
                        {
                            MetricName = metric,
                            BaselineValue = baselineValue,
                            NewValue = newValue,
                            PercentChange = percentChange,
                            Severity = severity,
                            Description = $"{metric} decreased by {Math.Abs(percentChange):F2}% (from {baselineValue:F4} to {newValue:F4})"
                        });
                    }
                }
            }
        }

        report.HasRegression = report.Issues.Any();

        // Save report
        await _evaluationStore.SaveRegressionReportAsync(report, cancellationToken);

        return report;
    }

    private static string CalculateSeverity(double percentChange)
    {
        var absChange = Math.Abs(percentChange);

        if (absChange >= 20) return "critical";
        if (absChange >= 10) return "high";
        if (absChange >= 5) return "medium";
        return "low";
    }

    private static DateTime? CalculateNextRun(string cronExpression, DateTime from)
    {
        // Simple cron parser - in production, use a library like Cronos
        // For now, support basic patterns: "0 2 * * *" (daily at 2 AM)

        try
        {
            var parts = cronExpression.Split(' ');
            if (parts.Length != 5) return null;

            var minute = int.Parse(parts[0]);
            var hour = int.Parse(parts[1]);

            var next = from.Date.AddDays(1).AddHours(hour).AddMinutes(minute);
            return next;
        }
        catch (Exception ex) when (ex is FormatException or IndexOutOfRangeException or OverflowException)
        {
            return null;
        }
    }
}

/// <summary>
/// Configuration for evaluation pipeline
/// </summary>
public class EvaluationConfig
{
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
    public double RegressionThresholdPercent { get; set; } = 5.0;  // 5% drop = regression
    public bool EnableParallelEvaluation { get; set; } = false;
    public int MaxParallelCases { get; set; } = 3;
}
