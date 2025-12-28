using System.Diagnostics;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Evaluation.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Evaluation;

/// <summary>
/// Runs benchmarks and collects performance metrics
/// </summary>
public class BenchmarkRunner
{
    private readonly EvaluationRunner _evaluationRunner;
    private readonly ILogger? _logger;

    public BenchmarkRunner(ILogger? logger = null)
    {
        _logger = logger;
        _evaluationRunner = new EvaluationRunner(logger);
    }

    /// <summary>
    /// Run a benchmark against an agent
    /// </summary>
    public async Task<BenchmarkResult> RunBenchmarkAsync(
        BaseAgent agent,
        Benchmark benchmark,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Starting benchmark: {BenchmarkId} - {Name}", benchmark.BenchmarkId, benchmark.Name);

        var overallStopwatch = Stopwatch.StartNew();
        var suiteResults = new List<TestSuiteResult>();
        var latencies = new List<double>();

        foreach (var suite in benchmark.TestSuites)
        {
            var suiteResult = await _evaluationRunner.RunTestSuiteAsync(agent, suite, cancellationToken);
            suiteResults.Add(suiteResult);

            // Collect latencies for percentile calculations
            foreach (var testResult in suiteResult.Results)
            {
                latencies.Add(testResult.Duration.TotalMilliseconds);
            }
        }

        overallStopwatch.Stop();

        // Calculate aggregate metrics
        var metrics = CalculateAggregateMetrics(suiteResults, latencies);

        var benchmarkResult = new BenchmarkResult
        {
            BenchmarkId = benchmark.BenchmarkId,
            AgentId = agent.AgentId,
            SuiteResults = suiteResults,
            Metrics = metrics,
            TotalDuration = overallStopwatch.Elapsed
        };

        _logger?.LogInformation("Benchmark {BenchmarkId} completed: Pass Rate={PassRate:P}, Avg Score={Score:F2}, Avg Latency={Latency:F2}ms",
            benchmark.BenchmarkId, metrics.PassRate, metrics.AverageScore, metrics.AverageLatencyMs);

        return benchmarkResult;
    }

    /// <summary>
    /// Compare multiple agents on the same benchmark
    /// </summary>
    public async Task<BenchmarkComparison> CompareBenchmarkAsync(
        List<BaseAgent> agents,
        Benchmark benchmark,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Running benchmark comparison with {Count} agents", agents.Count);

        var results = new Dictionary<string, BenchmarkResult>();

        foreach (var agent in agents)
        {
            var result = await RunBenchmarkAsync(agent, benchmark, cancellationToken);
            results[agent.AgentId] = result;
        }

        var comparison = new BenchmarkComparison
        {
            BenchmarkId = benchmark.BenchmarkId,
            Results = results,
            ComparedAt = DateTime.UtcNow
        };

        // Find best performer by average score
        if (results.Any())
        {
            var bestAgent = results.OrderByDescending(r => r.Value.Metrics.AverageScore).First();
            comparison.BestAgentId = bestAgent.Key;
            comparison.BestScore = bestAgent.Value.Metrics.AverageScore;
        }

        return comparison;
    }

    private BenchmarkMetrics CalculateAggregateMetrics(
        List<TestSuiteResult> suiteResults,
        List<double> latencies)
    {
        var allResults = suiteResults.SelectMany(s => s.Results).ToList();

        latencies.Sort();

        return new BenchmarkMetrics
        {
            TotalTests = allResults.Count,
            PassedTests = allResults.Count(r => r.Passed),
            FailedTests = allResults.Count(r => !r.Passed),
            PassRate = allResults.Count > 0 ? (double)allResults.Count(r => r.Passed) / allResults.Count : 0.0,
            AverageScore = allResults.Count > 0 ? allResults.Average(r => r.Score) : 0.0,
            AverageLatencyMs = latencies.Count > 0 ? latencies.Average() : 0.0,
            MedianLatencyMs = latencies.Count > 0 ? GetPercentile(latencies, 0.5) : 0.0,
            P95LatencyMs = latencies.Count > 0 ? GetPercentile(latencies, 0.95) : 0.0,
            P99LatencyMs = latencies.Count > 0 ? GetPercentile(latencies, 0.99) : 0.0
        };
    }

    private double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0.0;

        int index = (int)Math.Ceiling(percentile * sortedValues.Count) - 1;
        index = Math.Max(0, Math.Min(sortedValues.Count - 1, index));

        return sortedValues[index];
    }
}

/// <summary>
/// Benchmark definition
/// </summary>
public class Benchmark
{
    public string BenchmarkId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<TestSuite> TestSuites { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of running a benchmark
/// </summary>
public class BenchmarkResult
{
    public string BenchmarkId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public List<TestSuiteResult> SuiteResults { get; set; } = new();
    public BenchmarkMetrics Metrics { get; set; } = new();
    public TimeSpan TotalDuration { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Comparison of multiple agents on same benchmark
/// </summary>
public class BenchmarkComparison
{
    public string BenchmarkId { get; set; } = string.Empty;
    public Dictionary<string, BenchmarkResult> Results { get; set; } = new();
    public string? BestAgentId { get; set; }
    public double BestScore { get; set; }
    public DateTime ComparedAt { get; set; }
}
