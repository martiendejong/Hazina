using System.Text;
using System.Text.Json;
using Hazina.LLMs.GoogleADK.Evaluation.Models;

namespace Hazina.LLMs.GoogleADK.Evaluation;

/// <summary>
/// Generates evaluation reports in various formats
/// </summary>
public class EvaluationReporter
{
    /// <summary>
    /// Generate JSON report
    /// </summary>
    public string GenerateJsonReport(TestSuiteResult suiteResult)
    {
        return JsonSerializer.Serialize(suiteResult, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Generate JSON report for benchmark
    /// </summary>
    public string GenerateJsonReport(BenchmarkResult benchmarkResult)
    {
        return JsonSerializer.Serialize(benchmarkResult, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    /// <summary>
    /// Generate markdown report for test suite
    /// </summary>
    public string GenerateMarkdownReport(TestSuiteResult suiteResult)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Test Suite Report: {suiteResult.SuiteId}");
        sb.AppendLine();
        sb.AppendLine($"**Agent ID:** {suiteResult.AgentId}");
        sb.AppendLine($"**Executed:** {suiteResult.ExecutedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Duration:** {suiteResult.TotalDuration.TotalSeconds:F2}s");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine($"- **Total Tests:** {suiteResult.TotalTests}");
        sb.AppendLine($"- **Passed:** {suiteResult.PassedTests} ({suiteResult.PassRate:P})");
        sb.AppendLine($"- **Failed:** {suiteResult.FailedTests}");
        sb.AppendLine($"- **Average Score:** {suiteResult.AverageScore:F2}");
        sb.AppendLine();

        sb.AppendLine("## Test Results");
        sb.AppendLine();
        sb.AppendLine("| Test ID | Passed | Score | Duration | Error |");
        sb.AppendLine("|---------|--------|-------|----------|-------|");

        foreach (var result in suiteResult.Results)
        {
            var passedIcon = result.Passed ? "✓" : "✗";
            var error = result.ErrorMessage ?? "-";
            sb.AppendLine($"| {result.TestId.Substring(0, 8)}... | {passedIcon} | {result.Score:F2} | {result.Duration.TotalMilliseconds:F0}ms | {error} |");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate markdown report for benchmark
    /// </summary>
    public string GenerateMarkdownReport(BenchmarkResult benchmarkResult)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Benchmark Report: {benchmarkResult.BenchmarkId}");
        sb.AppendLine();
        sb.AppendLine($"**Agent ID:** {benchmarkResult.AgentId}");
        sb.AppendLine($"**Executed:** {benchmarkResult.ExecutedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Total Duration:** {benchmarkResult.TotalDuration.TotalSeconds:F2}s");
        sb.AppendLine();

        sb.AppendLine("## Metrics");
        sb.AppendLine();
        var metrics = benchmarkResult.Metrics;
        sb.AppendLine($"- **Pass Rate:** {metrics.PassRate:P}");
        sb.AppendLine($"- **Average Score:** {metrics.AverageScore:F2}");
        sb.AppendLine($"- **Total Tests:** {metrics.TotalTests}");
        sb.AppendLine($"- **Passed:** {metrics.PassedTests}");
        sb.AppendLine($"- **Failed:** {metrics.FailedTests}");
        sb.AppendLine();

        sb.AppendLine("## Performance");
        sb.AppendLine();
        sb.AppendLine($"- **Average Latency:** {metrics.AverageLatencyMs:F2}ms");
        sb.AppendLine($"- **Median Latency:** {metrics.MedianLatencyMs:F2}ms");
        sb.AppendLine($"- **P95 Latency:** {metrics.P95LatencyMs:F2}ms");
        sb.AppendLine($"- **P99 Latency:** {metrics.P99LatencyMs:F2}ms");
        sb.AppendLine();

        sb.AppendLine("## Suite Results");
        sb.AppendLine();
        sb.AppendLine("| Suite ID | Total | Passed | Failed | Pass Rate | Avg Score |");
        sb.AppendLine("|----------|-------|--------|--------|-----------|-----------|");

        foreach (var suite in benchmarkResult.SuiteResults)
        {
            sb.AppendLine($"| {suite.SuiteId.Substring(0, 8)}... | {suite.TotalTests} | {suite.PassedTests} | {suite.FailedTests} | {suite.PassRate:P} | {suite.AverageScore:F2} |");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Generate comparison report
    /// </summary>
    public string GenerateComparisonReport(BenchmarkComparison comparison)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Benchmark Comparison: {comparison.BenchmarkId}");
        sb.AppendLine();
        sb.AppendLine($"**Compared:** {comparison.ComparedAt:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**Agents Compared:** {comparison.Results.Count}");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(comparison.BestAgentId))
        {
            sb.AppendLine($"**Best Performer:** {comparison.BestAgentId} (Score: {comparison.BestScore:F2})");
            sb.AppendLine();
        }

        sb.AppendLine("## Agent Comparison");
        sb.AppendLine();
        sb.AppendLine("| Agent ID | Pass Rate | Avg Score | Avg Latency | P95 Latency | Total Tests |");
        sb.AppendLine("|----------|-----------|-----------|-------------|-------------|-------------|");

        foreach (var kvp in comparison.Results.OrderByDescending(r => r.Value.Metrics.AverageScore))
        {
            var agentId = kvp.Key;
            var result = kvp.Value;
            var metrics = result.Metrics;

            var isBest = agentId == comparison.BestAgentId ? " 🏆" : "";

            sb.AppendLine($"| {agentId}{isBest} | {metrics.PassRate:P} | {metrics.AverageScore:F2} | {metrics.AverageLatencyMs:F2}ms | {metrics.P95LatencyMs:F2}ms | {metrics.TotalTests} |");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Save report to file
    /// </summary>
    public async Task SaveReportAsync(string content, string filePath)
    {
        await File.WriteAllTextAsync(filePath, content);
    }

    /// <summary>
    /// Generate HTML report (basic)
    /// </summary>
    public string GenerateHtmlReport(BenchmarkResult benchmarkResult)
    {
        var sb = new StringBuilder();

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html>");
        sb.AppendLine("<head>");
        sb.AppendLine($"<title>Benchmark Report - {benchmarkResult.BenchmarkId}</title>");
        sb.AppendLine("<style>");
        sb.AppendLine("body { font-family: Arial, sans-serif; margin: 20px; }");
        sb.AppendLine("table { border-collapse: collapse; width: 100%; margin: 20px 0; }");
        sb.AppendLine("th, td { border: 1px solid #ddd; padding: 8px; text-align: left; }");
        sb.AppendLine("th { background-color: #4CAF50; color: white; }");
        sb.AppendLine(".metric { background-color: #f2f2f2; padding: 10px; margin: 10px 0; }");
        sb.AppendLine(".pass { color: green; }");
        sb.AppendLine(".fail { color: red; }");
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");

        sb.AppendLine($"<h1>Benchmark Report: {benchmarkResult.BenchmarkId}</h1>");
        sb.AppendLine($"<p><strong>Agent ID:</strong> {benchmarkResult.AgentId}</p>");
        sb.AppendLine($"<p><strong>Executed:</strong> {benchmarkResult.ExecutedAt:yyyy-MM-dd HH:mm:ss}</p>");

        sb.AppendLine("<h2>Metrics</h2>");
        sb.AppendLine("<div class='metric'>");
        var metrics = benchmarkResult.Metrics;
        sb.AppendLine($"<p><strong>Pass Rate:</strong> {metrics.PassRate:P}</p>");
        sb.AppendLine($"<p><strong>Average Score:</strong> {metrics.AverageScore:F2}</p>");
        sb.AppendLine($"<p><strong>Average Latency:</strong> {metrics.AverageLatencyMs:F2}ms</p>");
        sb.AppendLine($"<p><strong>P95 Latency:</strong> {metrics.P95LatencyMs:F2}ms</p>");
        sb.AppendLine("</div>");

        sb.AppendLine("<h2>Suite Results</h2>");
        sb.AppendLine("<table>");
        sb.AppendLine("<tr><th>Suite ID</th><th>Total</th><th>Passed</th><th>Failed</th><th>Pass Rate</th><th>Avg Score</th></tr>");

        foreach (var suite in benchmarkResult.SuiteResults)
        {
            sb.AppendLine($"<tr>");
            sb.AppendLine($"<td>{suite.SuiteId}</td>");
            sb.AppendLine($"<td>{suite.TotalTests}</td>");
            sb.AppendLine($"<td class='pass'>{suite.PassedTests}</td>");
            sb.AppendLine($"<td class='fail'>{suite.FailedTests}</td>");
            sb.AppendLine($"<td>{suite.PassRate:P}</td>");
            sb.AppendLine($"<td>{suite.AverageScore:F2}</td>");
            sb.AppendLine($"</tr>");
        }

        sb.AppendLine("</table>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
