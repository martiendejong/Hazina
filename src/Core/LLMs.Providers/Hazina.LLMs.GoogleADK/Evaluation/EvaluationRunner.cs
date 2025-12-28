using System.Diagnostics;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Evaluation.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Evaluation;

/// <summary>
/// Runs evaluation tests against agents
/// </summary>
public class EvaluationRunner
{
    private readonly ILogger? _logger;
    private readonly List<EvaluationMetric> _metrics = new();

    public EvaluationRunner(ILogger? logger = null)
    {
        _logger = logger;

        // Register default metrics
        RegisterMetric(new ExactMatchMetric());
        RegisterMetric(new ContainsMetric());
        RegisterMetric(new SimilarityMetric());
    }

    /// <summary>
    /// Register a custom metric
    /// </summary>
    public void RegisterMetric(EvaluationMetric metric)
    {
        _metrics.Add(metric);
    }

    /// <summary>
    /// Run a single test case
    /// </summary>
    public async Task<TestCaseResult> RunTestCaseAsync(
        BaseAgent agent,
        TestCase testCase,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Running test case: {TestId} - {Name}", testCase.TestId, testCase.Name);

        var stopwatch = Stopwatch.StartNew();
        var result = new TestCaseResult
        {
            TestId = testCase.TestId,
            AgentId = agent.AgentId,
            Input = testCase.Input,
            ExpectedOutput = testCase.ExpectedOutput
        };

        try
        {
            // Execute agent
            var agentResult = await agent.ExecuteAsync(testCase.Input, cancellationToken);
            result.ActualOutput = agentResult.Output;

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;

            // Calculate metrics
            if (!string.IsNullOrEmpty(testCase.ExpectedOutput))
            {
                foreach (var metric in _metrics)
                {
                    var metricValue = metric.Calculate(testCase.ExpectedOutput, result.ActualOutput ?? string.Empty);
                    result.Metrics[metric.Name] = metricValue;
                }

                // Use similarity metric as primary score
                result.Score = result.Metrics.ContainsKey("Similarity")
                    ? result.Metrics["Similarity"]
                    : 0.0;

                // Test passes if similarity > 0.7 or exact match
                result.Passed = result.Score > 0.7 ||
                    (result.Metrics.ContainsKey("ExactMatch") && result.Metrics["ExactMatch"] == 1.0);
            }
            else
            {
                // No expected output, just check if agent produced something
                result.Passed = !string.IsNullOrEmpty(result.ActualOutput);
                result.Score = result.Passed ? 1.0 : 0.0;
            }

            _logger?.LogInformation("Test {TestId} completed: Passed={Passed}, Score={Score:F2}, Duration={Duration}ms",
                testCase.TestId, result.Passed, result.Score, result.Duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Passed = false;
            result.Score = 0.0;
            result.ErrorMessage = ex.Message;

            _logger?.LogError(ex, "Test {TestId} failed with error", testCase.TestId);
        }

        return result;
    }

    /// <summary>
    /// Run a test suite
    /// </summary>
    public async Task<TestSuiteResult> RunTestSuiteAsync(
        BaseAgent agent,
        TestSuite testSuite,
        CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("Running test suite: {SuiteId} - {Name} ({Count} tests)",
            testSuite.SuiteId, testSuite.Name, testSuite.TestCases.Count);

        var suiteStopwatch = Stopwatch.StartNew();
        var results = new List<TestCaseResult>();

        foreach (var testCase in testSuite.TestCases)
        {
            var result = await RunTestCaseAsync(agent, testCase, cancellationToken);
            results.Add(result);
        }

        suiteStopwatch.Stop();

        var suiteResult = new TestSuiteResult
        {
            SuiteId = testSuite.SuiteId,
            AgentId = agent.AgentId,
            Results = results,
            TotalTests = results.Count,
            PassedTests = results.Count(r => r.Passed),
            FailedTests = results.Count(r => !r.Passed),
            TotalDuration = suiteStopwatch.Elapsed
        };

        suiteResult.PassRate = suiteResult.TotalTests > 0
            ? (double)suiteResult.PassedTests / suiteResult.TotalTests
            : 0.0;

        suiteResult.AverageScore = results.Count > 0
            ? results.Average(r => r.Score)
            : 0.0;

        _logger?.LogInformation("Test suite {SuiteId} completed: {Passed}/{Total} passed, Average Score={Score:F2}, Duration={Duration}s",
            testSuite.SuiteId, suiteResult.PassedTests, suiteResult.TotalTests,
            suiteResult.AverageScore, suiteResult.TotalDuration.TotalSeconds);

        return suiteResult;
    }

    /// <summary>
    /// Run multiple test suites
    /// </summary>
    public async Task<List<TestSuiteResult>> RunMultipleSuitesAsync(
        BaseAgent agent,
        List<TestSuite> testSuites,
        CancellationToken cancellationToken = default)
    {
        var results = new List<TestSuiteResult>();

        foreach (var suite in testSuites)
        {
            var suiteResult = await RunTestSuiteAsync(agent, suite, cancellationToken);
            results.Add(suiteResult);
        }

        return results;
    }
}
