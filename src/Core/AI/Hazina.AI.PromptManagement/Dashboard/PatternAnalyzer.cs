using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Hazina.AI.PromptManagement.Evaluation;
using Npgsql;

namespace Hazina.AI.PromptManagement.Dashboard;

/// <summary>
/// PostgreSQL implementation of pattern analyzer
/// </summary>
public class PatternAnalyzer : IPatternAnalyzer
{
    private readonly string _connectionString;

    public PatternAnalyzer(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    private async Task<IDbConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<List<FailurePattern>> DetectFailurePatternsAsync(
        string promptId,
        DateTime startDate,
        DateTime endDate,
        double minFrequency = 0.1,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        // Get all failed eval cases
        var sql = @"
            SELECT
                er.run_id,
                er.started_at,
                er.case_results
            FROM eval_runs er
            WHERE er.prompt_id = @PromptId
              AND er.status = 'completed'
              AND er.started_at >= @StartDate
              AND er.started_at <= @EndDate";

        var runs = await connection.QueryAsync<dynamic>(sql, new
        {
            PromptId = promptId,
            StartDate = startDate,
            EndDate = endDate
        });

        var allCases = new List<EvaluationCaseResult>();
        var totalCases = 0;

        foreach (var run in runs)
        {
            if (run.case_results == null) continue;

            var cases = JsonSerializer.Deserialize<List<EvaluationCaseResult>>(
                run.case_results.ToString());

            if (cases != null)
            {
                allCases.AddRange(cases);
                totalCases += cases.Count;
            }
        }

        // Detect patterns in low-scoring cases
        var lowScoringCases = allCases.Where(c =>
            c.Scores.ContainsKey("Overall") && c.Scores["Overall"] < 0.6
        ).ToList();

        // Group by similar characteristics (simplified pattern detection)
        var patterns = new List<FailurePattern>();

        // Pattern 1: Low accuracy scores
        var lowAccuracy = lowScoringCases.Where(c =>
            c.Scores.ContainsKey("Accuracy") && c.Scores["Accuracy"] < 0.5
        ).ToList();

        if (lowAccuracy.Any())
        {
            var frequency = (double)lowAccuracy.Count / totalCases;
            if (frequency >= minFrequency)
            {
                patterns.Add(new FailurePattern
                {
                    PatternId = $"low-accuracy-{Guid.NewGuid():N}",
                    Description = "Low accuracy scores - factual errors or incorrect information",
                    Frequency = frequency,
                    Occurrences = lowAccuracy.Count,
                    Examples = lowAccuracy.Take(5).Select(c => $"Query: {c.Query}\nResponse: {c.Response.Substring(0, Math.Min(200, c.Response.Length))}...").ToList(),
                    RootCause = "Prompt may lack fact-checking instructions or relevant context",
                    AffectedMetrics = new List<string> { "Accuracy", "Overall" },
                    Severity = frequency > 0.3 ? 0.8 : 0.5,
                    FirstSeen = lowAccuracy.Min(c => c.Metadata?["timestamp"] as DateTime? ?? DateTime.MinValue),
                    LastSeen = lowAccuracy.Max(c => c.Metadata?["timestamp"] as DateTime? ?? DateTime.MaxValue),
                    IsRecurring = true
                });
            }
        }

        // Pattern 2: Low relevance scores
        var lowRelevance = lowScoringCases.Where(c =>
            c.Scores.ContainsKey("Relevance") && c.Scores["Relevance"] < 0.5
        ).ToList();

        if (lowRelevance.Any())
        {
            var frequency = (double)lowRelevance.Count / totalCases;
            if (frequency >= minFrequency)
            {
                patterns.Add(new FailurePattern
                {
                    PatternId = $"low-relevance-{Guid.NewGuid():N}",
                    Description = "Low relevance scores - responses don't address the query",
                    Frequency = frequency,
                    Occurrences = lowRelevance.Count,
                    Examples = lowRelevance.Take(5).Select(c => $"Query: {c.Query}\nResponse: {c.Response.Substring(0, Math.Min(200, c.Response.Length))}...").ToList(),
                    RootCause = "Prompt may be too generic or lack query understanding instructions",
                    AffectedMetrics = new List<string> { "Relevance", "Overall" },
                    Severity = frequency > 0.3 ? 0.7 : 0.4,
                    FirstSeen = lowRelevance.Min(c => c.Metadata?["timestamp"] as DateTime? ?? DateTime.MinValue),
                    LastSeen = lowRelevance.Max(c => c.Metadata?["timestamp"] as DateTime? ?? DateTime.MaxValue),
                    IsRecurring = true
                });
            }
        }

        // Pattern 3: High latency failures
        var highLatency = allCases.Where(c => c.Duration.TotalMilliseconds > 5000).ToList();

        if (highLatency.Any())
        {
            var frequency = (double)highLatency.Count / totalCases;
            if (frequency >= minFrequency)
            {
                patterns.Add(new FailurePattern
                {
                    PatternId = $"high-latency-{Guid.NewGuid():N}",
                    Description = "High latency (>5s) - slow response generation",
                    Frequency = frequency,
                    Occurrences = highLatency.Count,
                    Examples = highLatency.Take(5).Select(c => $"Query: {c.Query} (took {c.Duration.TotalSeconds:F2}s)").ToList(),
                    RootCause = "Prompt may be too complex or require excessive reasoning",
                    AffectedMetrics = new List<string> { "Latency" },
                    Severity = frequency > 0.2 ? 0.6 : 0.3,
                    FirstSeen = DateTime.UtcNow.AddDays(-30),  // Placeholder
                    LastSeen = DateTime.UtcNow,
                    IsRecurring = true
                });
            }
        }

        return patterns.OrderByDescending(p => p.Severity * p.Frequency).ToList();
    }

    public async Task<List<SuccessPattern>> DetectSuccessPatternsAsync(
        string promptId,
        DateTime startDate,
        DateTime endDate,
        double minFrequency = 0.1,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        // Similar to failure patterns but for high-scoring cases
        var sql = @"
            SELECT
                er.run_id,
                er.started_at,
                er.case_results
            FROM eval_runs er
            WHERE er.prompt_id = @PromptId
              AND er.status = 'completed'
              AND er.started_at >= @StartDate
              AND er.started_at <= @EndDate";

        var runs = await connection.QueryAsync<dynamic>(sql, new
        {
            PromptId = promptId,
            StartDate = startDate,
            EndDate = endDate
        });

        var allCases = new List<EvaluationCaseResult>();
        var totalCases = 0;

        foreach (var run in runs)
        {
            if (run.case_results == null) continue;

            var cases = JsonSerializer.Deserialize<List<EvaluationCaseResult>>(
                run.case_results.ToString());

            if (cases != null)
            {
                allCases.AddRange(cases);
                totalCases += cases.Count;
            }
        }

        var highScoringCases = allCases.Where(c =>
            c.Scores.ContainsKey("Overall") && c.Scores["Overall"] >= 0.8
        ).ToList();

        var patterns = new List<SuccessPattern>();

        // Pattern 1: Excellent accuracy
        var highAccuracy = highScoringCases.Where(c =>
            c.Scores.ContainsKey("Accuracy") && c.Scores["Accuracy"] >= 0.9
        ).ToList();

        if (highAccuracy.Any())
        {
            var frequency = (double)highAccuracy.Count / totalCases;
            if (frequency >= minFrequency)
            {
                patterns.Add(new SuccessPattern
                {
                    PatternId = $"high-accuracy-{Guid.NewGuid():N}",
                    Description = "Excellent accuracy - factually correct responses",
                    Frequency = frequency,
                    Occurrences = highAccuracy.Count,
                    Examples = highAccuracy.Take(5).Select(c => $"Query: {c.Query}\nScore: {c.Scores["Accuracy"]:F2}").ToList(),
                    KeyFactors = new List<string> { "Clear instructions", "Relevant context", "Fact-checking" },
                    MetricImpact = new Dictionary<string, double>
                    {
                        { "Accuracy", highAccuracy.Average(c => c.Scores["Accuracy"]) },
                        { "Overall", highAccuracy.Average(c => c.Scores["Overall"]) }
                    },
                    FirstSeen = DateTime.UtcNow.AddDays(-30),
                    LastSeen = DateTime.UtcNow
                });
            }
        }

        return patterns.OrderByDescending(p => p.Frequency).ToList();
    }

    public async Task<DriftAnalysis> DetectDriftAsync(
        string promptId,
        string metricName,
        DateTime baselineStartDate,
        DateTime baselineEndDate,
        DateTime currentStartDate,
        DateTime currentEndDate,
        CancellationToken cancellationToken = default)
    {
        using var connection = await GetConnectionAsync();

        // Get baseline metrics
        var baselineSql = @"
            SELECT
                AVG((er.metrics->>@MetricName)::float) as mean,
                STDDEV((er.metrics->>@MetricName)::float) as std_dev,
                COUNT(*) as sample_size
            FROM eval_runs er
            WHERE er.prompt_id = @PromptId
              AND er.status = 'completed'
              AND er.started_at >= @StartDate
              AND er.started_at <= @EndDate
              AND er.metrics ? @MetricName";

        var baseline = await connection.QueryFirstOrDefaultAsync<dynamic>(baselineSql, new
        {
            PromptId = promptId,
            MetricName = metricName,
            StartDate = baselineStartDate,
            EndDate = baselineEndDate
        });

        // Get current metrics
        var current = await connection.QueryFirstOrDefaultAsync<dynamic>(baselineSql, new
        {
            PromptId = promptId,
            MetricName = metricName,
            StartDate = currentStartDate,
            EndDate = currentEndDate
        });

        if (baseline == null || current == null)
        {
            return new DriftAnalysis
            {
                PromptId = promptId,
                MetricName = metricName,
                HasDrift = false
            };
        }

        var baselineMean = (double)(baseline!.mean ?? 0.0);
        var baselineStdDev = (double)(baseline!.std_dev ?? 0.0);
        var baselineSampleSize = (int)(baseline!.sample_size ?? 0);

        var currentMean = (double)(current!.mean ?? 0.0);
        var currentStdDev = (double)(current!.std_dev ?? 0.0);
        var currentSampleSize = (int)(current!.sample_size ?? 0);

        var absoluteChange = currentMean - baselineMean;
        var percentChange = baselineMean > 0 ? (absoluteChange / baselineMean) * 100 : 0;

        // Calculate drift magnitude (in standard deviations)
        var pooledStdDev = Math.Sqrt(
            ((baselineSampleSize - 1) * baselineStdDev * baselineStdDev +
             (currentSampleSize - 1) * currentStdDev * currentStdDev) /
            (baselineSampleSize + currentSampleSize - 2)
        );

        var driftMagnitude = pooledStdDev > 0 ? Math.Abs(absoluteChange) / pooledStdDev : 0;

        // T-test for statistical significance
        var standardError = pooledStdDev * Math.Sqrt(1.0 / baselineSampleSize + 1.0 / currentSampleSize);
        var tStatistic = standardError > 0 ? absoluteChange / standardError : 0;

        // Simplified p-value (2-tailed)
        var pValue = CalculatePValue(Math.Abs(tStatistic), baselineSampleSize + currentSampleSize - 2);

        var isSignificant = pValue < 0.05;
        var hasDrift = isSignificant && Math.Abs(percentChange) > 5.0;

        var driftDirection = !hasDrift ? "stable" :
                            absoluteChange > 0 ? "improving" : "degrading";

        // Effect size (Cohen's d)
        var effectSize = pooledStdDev > 0 ? absoluteChange / pooledStdDev : 0;

        var recommendations = new List<string>();
        if (hasDrift && driftDirection == "degrading")
        {
            recommendations.Add("Consider rolling back to previous version");
            recommendations.Add("Review recent prompt changes");
            recommendations.Add("Check for data quality issues in recent evaluations");
        }
        else if (hasDrift && driftDirection == "improving")
        {
            recommendations.Add("Document successful changes for future reference");
            recommendations.Add("Consider expanding successful patterns to other prompts");
        }

        return new DriftAnalysis
        {
            PromptId = promptId,
            MetricName = metricName,
            BaselineMean = baselineMean,
            BaselineStdDev = baselineStdDev,
            BaselineSampleSize = baselineSampleSize,
            CurrentMean = currentMean,
            CurrentStdDev = currentStdDev,
            CurrentSampleSize = currentSampleSize,
            AbsoluteChange = absoluteChange,
            PercentChange = percentChange,
            HasDrift = hasDrift,
            DriftDirection = driftDirection,
            DriftMagnitude = driftMagnitude,
            PValue = pValue,
            IsSignificant = isSignificant,
            EffectSize = effectSize,
            Recommendations = recommendations
        };
    }

    public async Task<PatternEvolution> GetPatternEvolutionAsync(
        string promptId,
        string patternType,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        // Simplified implementation - track pattern frequency over time
        var timeline = new List<PatternSnapshot>();

        // In production, this would analyze patterns in time windows
        // For now, return basic structure

        return new PatternEvolution
        {
            PromptId = promptId,
            PatternType = patternType,
            Timeline = timeline,
            Trends = new PatternTrends
            {
                EmergingPatterns = new List<string>(),
                DecliningPatterns = new List<string>(),
                StablePatterns = new List<string>(),
                IsImproving = true,
                ImprovementRate = 0.0
            }
        };
    }

    // Helper method for p-value calculation (simplified)
    private static double CalculatePValue(double tStat, int degreesOfFreedom)
    {
        // Simplified t-distribution p-value
        // In production, use a proper statistical library

        if (tStat < 0.5) return 1.0;
        if (tStat > 4.0) return 0.0001;

        // Rough approximation
        return Math.Exp(-tStat * tStat / 2) / Math.Sqrt(2 * Math.PI);
    }
}
