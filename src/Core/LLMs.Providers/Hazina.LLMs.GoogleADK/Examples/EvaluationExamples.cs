using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Evaluation;
using Hazina.LLMs.GoogleADK.Evaluation.Models;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Examples;

/// <summary>
/// Examples for Evaluation Framework (Step 8)
/// </summary>
public class EvaluationExamples
{
    /// <summary>
    /// Example 1: Basic test case execution
    /// </summary>
    public static async Task BasicTestCaseExample(ILLMClient llmClient, ILogger logger)
    {
        var agent = new LlmAgent("QuizAgent", llmClient);
        await agent.InitializeAsync();

        var runner = new EvaluationRunner(logger);

        var testCase = new TestCase
        {
            Name = "Capital Question",
            Description = "Test geography knowledge",
            Input = "What is the capital of France?",
            ExpectedOutput = "Paris",
            Difficulty = TestCaseDifficulty.Easy
        };

        var result = await runner.RunTestCaseAsync(agent, testCase);

        Console.WriteLine($"Test: {testCase.Name}");
        Console.WriteLine($"Passed: {result.Passed}");
        Console.WriteLine($"Score: {result.Score:F2}");
        Console.WriteLine($"Duration: {result.Duration.TotalMilliseconds:F0}ms");
        Console.WriteLine($"Expected: {result.ExpectedOutput}");
        Console.WriteLine($"Actual: {result.ActualOutput}");

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 2: Test suite execution
    /// </summary>
    public static async Task TestSuiteExample(ILLMClient llmClient, ILogger logger)
    {
        var agent = new LlmAgent("GeneralKnowledge", llmClient);
        await agent.InitializeAsync();

        var runner = new EvaluationRunner(logger);

        var suite = new TestSuite
        {
            Name = "Geography Quiz",
            Description = "Tests geography knowledge",
            TestCases = new List<TestCase>
            {
                new() { Name = "France", Input = "Capital of France?", ExpectedOutput = "Paris" },
                new() { Name = "Japan", Input = "Capital of Japan?", ExpectedOutput = "Tokyo" },
                new() { Name = "Brazil", Input = "Capital of Brazil?", ExpectedOutput = "Brasília" },
                new() { Name = "Australia", Input = "Capital of Australia?", ExpectedOutput = "Canberra" }
            }
        };

        var result = await runner.RunTestSuiteAsync(agent, suite);

        Console.WriteLine($"Suite: {suite.Name}");
        Console.WriteLine($"Total Tests: {result.TotalTests}");
        Console.WriteLine($"Passed: {result.PassedTests}");
        Console.WriteLine($"Failed: {result.FailedTests}");
        Console.WriteLine($"Pass Rate: {result.PassRate:P}");
        Console.WriteLine($"Average Score: {result.AverageScore:F2}");
        Console.WriteLine($"Duration: {result.TotalDuration.TotalSeconds:F2}s");

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 3: Running a benchmark
    /// </summary>
    public static async Task BenchmarkExample(ILLMClient llmClient, ILogger logger)
    {
        var agent = new LlmAgent("KnowledgeAgent", llmClient);
        await agent.InitializeAsync();

        var benchmarkRunner = new BenchmarkRunner(logger);

        var benchmark = new Benchmark
        {
            Name = "General Knowledge Benchmark",
            Description = "Comprehensive knowledge test",
            TestSuites = new List<TestSuite>
            {
                new()
                {
                    Name = "Geography",
                    TestCases = new List<TestCase>
                    {
                        new() { Input = "Capital of France?", ExpectedOutput = "Paris" },
                        new() { Input = "Capital of Japan?", ExpectedOutput = "Tokyo" }
                    }
                },
                new()
                {
                    Name = "Science",
                    TestCases = new List<TestCase>
                    {
                        new() { Input = "Symbol for gold?", ExpectedOutput = "Au" },
                        new() { Input = "Speed of light?", ExpectedOutput = "299792458" }
                    }
                }
            }
        };

        var result = await benchmarkRunner.RunBenchmarkAsync(agent, benchmark);

        Console.WriteLine($"Benchmark: {benchmark.Name}");
        Console.WriteLine($"\nMetrics:");
        Console.WriteLine($"  Pass Rate: {result.Metrics.PassRate:P}");
        Console.WriteLine($"  Average Score: {result.Metrics.AverageScore:F2}");
        Console.WriteLine($"  Total Tests: {result.Metrics.TotalTests}");
        Console.WriteLine($"\nPerformance:");
        Console.WriteLine($"  Average Latency: {result.Metrics.AverageLatencyMs:F2}ms");
        Console.WriteLine($"  Median Latency: {result.Metrics.MedianLatencyMs:F2}ms");
        Console.WriteLine($"  P95 Latency: {result.Metrics.P95LatencyMs:F2}ms");
        Console.WriteLine($"  P99 Latency: {result.Metrics.P99LatencyMs:F2}ms");

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 4: Comparing multiple agents
    /// </summary>
    public static async Task AgentComparisonExample(
        ILLMClient llmClient1,
        ILLMClient llmClient2,
        ILogger logger)
    {
        var agent1 = new LlmAgent("Agent1", llmClient1);
        var agent2 = new LlmAgent("Agent2", llmClient2);

        await agent1.InitializeAsync();
        await agent2.InitializeAsync();

        var benchmarkRunner = new BenchmarkRunner(logger);

        var benchmark = new Benchmark
        {
            Name = "Agent Comparison Benchmark",
            TestSuites = new List<TestSuite>
            {
                new()
                {
                    Name = "Common Tasks",
                    TestCases = new List<TestCase>
                    {
                        new() { Input = "Summarize: AI is transforming technology", ExpectedOutput = "AI transforms tech" },
                        new() { Input = "Translate to Spanish: Hello", ExpectedOutput = "Hola" }
                    }
                }
            }
        };

        var comparison = await benchmarkRunner.CompareBenchmarkAsync(
            new List<BaseAgent> { agent1, agent2 },
            benchmark
        );

        Console.WriteLine($"Benchmark: {benchmark.Name}");
        Console.WriteLine($"Best Agent: {comparison.BestAgentId} (Score: {comparison.BestScore:F2})");
        Console.WriteLine($"\nComparison:");

        foreach (var kvp in comparison.Results)
        {
            var agentId = kvp.Key;
            var result = kvp.Value;
            Console.WriteLine($"\n  {agentId}:");
            Console.WriteLine($"    Pass Rate: {result.Metrics.PassRate:P}");
            Console.WriteLine($"    Avg Score: {result.Metrics.AverageScore:F2}");
            Console.WriteLine($"    Avg Latency: {result.Metrics.AverageLatencyMs:F2}ms");
        }

        await agent1.DisposeAsync();
        await agent2.DisposeAsync();
    }

    /// <summary>
    /// Example 5: Custom metrics
    /// </summary>
    public static async Task CustomMetricsExample(ILLMClient llmClient, ILogger logger)
    {
        var agent = new LlmAgent("TestAgent", llmClient);
        await agent.InitializeAsync();

        var runner = new EvaluationRunner(logger);

        // Register custom metric
        runner.RegisterMetric(new CustomWordCountMetric());

        var testCase = new TestCase
        {
            Input = "Write a short description of AI",
            ExpectedOutput = "artificial intelligence technology"
        };

        var result = await runner.RunTestCaseAsync(agent, testCase);

        Console.WriteLine($"Metrics:");
        foreach (var metric in result.Metrics)
        {
            Console.WriteLine($"  {metric.Key}: {metric.Value:F2}");
        }

        await agent.DisposeAsync();
    }

    /// <summary>
    /// Example 6: Generating reports
    /// </summary>
    public static async Task ReportGenerationExample(ILLMClient llmClient, ILogger logger)
    {
        var agent = new LlmAgent("ReportAgent", llmClient);
        await agent.InitializeAsync();

        var benchmarkRunner = new BenchmarkRunner(logger);
        var reporter = new EvaluationReporter();

        var benchmark = new Benchmark
        {
            Name = "Sample Benchmark",
            TestSuites = new List<TestSuite>
            {
                new()
                {
                    Name = "Basic Tests",
                    TestCases = new List<TestCase>
                    {
                        new() { Input = "Test 1", ExpectedOutput = "Result 1" },
                        new() { Input = "Test 2", ExpectedOutput = "Result 2" }
                    }
                }
            }
        };

        var result = await benchmarkRunner.RunBenchmarkAsync(agent, benchmark);

        // Generate different report formats
        var jsonReport = reporter.GenerateJsonReport(result);
        var markdownReport = reporter.GenerateMarkdownReport(result);
        var htmlReport = reporter.GenerateHtmlReport(result);

        Console.WriteLine("Generated reports:");
        Console.WriteLine("\nMarkdown Report:");
        Console.WriteLine(markdownReport);

        // Save reports
        await reporter.SaveReportAsync(jsonReport, "benchmark-result.json");
        await reporter.SaveReportAsync(markdownReport, "benchmark-result.md");
        await reporter.SaveReportAsync(htmlReport, "benchmark-result.html");

        Console.WriteLine("\nReports saved to files");

        await agent.DisposeAsync();
    }
}

/// <summary>
/// Custom metric example
/// </summary>
public class CustomWordCountMetric : EvaluationMetric
{
    public CustomWordCountMetric()
    {
        Name = "WordCount";
        Description = "Counts words in output";
        MinValue = 0;
        MaxValue = double.MaxValue;
    }

    public override double Calculate(string expected, string actual)
    {
        return actual.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
