using System.Text;
using System.Text.Json;
using Hazina.Evals.Models;

namespace Hazina.Evals.Core;

/// <summary>
/// Compares two evaluation runs to detect quality regressions.
/// </summary>
public static class RegressionComparison
{
    /// <summary>
    /// Compare two evaluation runs and return a comparison report
    /// </summary>
    public static ComparisonReport Compare(EvalRun baseline, EvalRun candidate)
    {
        var report = new ComparisonReport
        {
            BaselineRunId = baseline.RunId,
            CandidateRunId = candidate.RunId,
            BaselineTimestamp = baseline.Timestamp,
            CandidateTimestamp = candidate.Timestamp
        };

        if (baseline.AggregateMetrics != null && candidate.AggregateMetrics != null)
        {
            report.MetricDifferences["HitAtK"] = CalculateDifference(
                baseline.AggregateMetrics.HitAtK,
                candidate.AggregateMetrics.HitAtK
            );

            report.MetricDifferences["MRR"] = CalculateDifference(
                baseline.AggregateMetrics.MRR,
                candidate.AggregateMetrics.MRR
            );

            report.MetricDifferences["NDCG"] = CalculateDifference(
                baseline.AggregateMetrics.NDCG,
                candidate.AggregateMetrics.NDCG
            );

            report.MetricDifferences["PrecisionAtK"] = CalculateDifference(
                baseline.AggregateMetrics.PrecisionAtK,
                candidate.AggregateMetrics.PrecisionAtK
            );

            report.MetricDifferences["RecallAtK"] = CalculateDifference(
                baseline.AggregateMetrics.RecallAtK,
                candidate.AggregateMetrics.RecallAtK
            );

            if (baseline.AggregateMetrics.AvgRetrievalTime.HasValue &&
                candidate.AggregateMetrics.AvgRetrievalTime.HasValue)
            {
                var baselineMs = baseline.AggregateMetrics.AvgRetrievalTime.Value.TotalMilliseconds;
                var candidateMs = candidate.AggregateMetrics.AvgRetrievalTime.Value.TotalMilliseconds;
                report.AvgLatencyDifference = candidateMs - baselineMs;
            }
        }

        report.HasRegression = DetectRegression(report);

        return report;
    }

    private static double? CalculateDifference(double? baseline, double? candidate)
    {
        if (!baseline.HasValue || !candidate.HasValue)
            return null;

        return candidate.Value - baseline.Value;
    }

    private static bool DetectRegression(ComparisonReport report)
    {
        foreach (var (metric, diff) in report.MetricDifferences)
        {
            if (diff.HasValue && diff.Value < -0.05)
                return true;
        }

        if (report.AvgLatencyDifference.HasValue &&
            report.AvgLatencyDifference.Value > 500)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Generate a console-friendly summary
    /// </summary>
    public static string GenerateConsoleSummary(ComparisonReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== Regression Comparison Report ===");
        sb.AppendLine($"Baseline: {report.BaselineRunId} ({report.BaselineTimestamp:yyyy-MM-dd HH:mm})");
        sb.AppendLine($"Candidate: {report.CandidateRunId} ({report.CandidateTimestamp:yyyy-MM-dd HH:mm})");
        sb.AppendLine();

        sb.AppendLine("Metric Differences:");
        foreach (var (metric, diff) in report.MetricDifferences)
        {
            if (diff.HasValue)
            {
                var sign = diff.Value >= 0 ? "+" : "";
                var color = diff.Value < -0.05 ? "REGRESSION" : diff.Value > 0.05 ? "IMPROVEMENT" : "";
                sb.AppendLine($"  {metric,-15}: {sign}{diff.Value:F4}  {color}");
            }
        }

        if (report.AvgLatencyDifference.HasValue)
        {
            var sign = report.AvgLatencyDifference.Value >= 0 ? "+" : "";
            sb.AppendLine($"  {"Latency (ms)",-15}: {sign}{report.AvgLatencyDifference.Value:F2}");
        }

        sb.AppendLine();
        sb.AppendLine($"Regression Detected: {(report.HasRegression ? "YES" : "NO")}");

        return sb.ToString();
    }

    /// <summary>
    /// Export comparison to JSONL format
    /// </summary>
    public static void ExportToJsonL(ComparisonReport report, string filePath)
    {
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = false
        });
        File.AppendAllText(filePath, json + Environment.NewLine);
    }

    /// <summary>
    /// Export evaluation run to JSONL format
    /// </summary>
    public static void ExportRunToJsonL(EvalRun run, string filePath)
    {
        var json = JsonSerializer.Serialize(run, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        File.AppendAllText(filePath, json + Environment.NewLine);
    }
}

/// <summary>
/// Comparison report between two evaluation runs
/// </summary>
public class ComparisonReport
{
    public required string BaselineRunId { get; init; }
    public required string CandidateRunId { get; init; }
    public DateTime BaselineTimestamp { get; init; }
    public DateTime CandidateTimestamp { get; init; }

    public Dictionary<string, double?> MetricDifferences { get; init; } = new();
    public double? AvgLatencyDifference { get; set; }
    public bool HasRegression { get; set; }
}
