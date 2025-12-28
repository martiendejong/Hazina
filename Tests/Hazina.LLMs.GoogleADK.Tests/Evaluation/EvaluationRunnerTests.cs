using Hazina.LLMs.GoogleADK.Agents;
using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Evaluation;
using Hazina.LLMs.GoogleADK.Evaluation.Models;
using Xunit;

namespace Hazina.LLMs.GoogleADK.Tests.Evaluation;

public class EvaluationRunnerTests
{
    private class MockLLMClient : ILLMClient
    {
        private readonly string _response;

        public MockLLMClient(string response = "Test response")
        {
            _response = response;
        }

        public Task<HazinaChatResponse> GenerateChatAsync(HazinaChatRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HazinaChatResponse
            {
                Content = _response,
                FinishReason = HazinaChatFinishReason.Stop
            });
        }

        public IAsyncEnumerable<HazinaChatStreamChunk> StreamChatAsync(HazinaChatRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public async Task RunTestCaseAsync_ShouldExecuteAndRecordResult()
    {
        // Arrange
        var llmClient = new MockLLMClient("Paris");
        var agent = new LlmAgent("TestAgent", llmClient);
        await agent.InitializeAsync();

        var runner = new EvaluationRunner();
        var testCase = new TestCase
        {
            Name = "Capital of France",
            Input = "What is the capital of France?",
            ExpectedOutput = "Paris"
        };

        // Act
        var result = await runner.RunTestCaseAsync(agent, testCase);

        // Assert
        Assert.Equal("Paris", result.ActualOutput);
        Assert.True(result.Passed);
        Assert.Equal(1.0, result.Score);
        Assert.True(result.Duration > TimeSpan.Zero);

        await agent.DisposeAsync();
    }

    [Fact]
    public async Task RunTestCaseAsync_WithMismatch_ShouldFail()
    {
        // Arrange
        var llmClient = new MockLLMClient("London");
        var agent = new LlmAgent("TestAgent", llmClient);
        await agent.InitializeAsync();

        var runner = new EvaluationRunner();
        var testCase = new TestCase
        {
            Name = "Capital of France",
            Input = "What is the capital of France?",
            ExpectedOutput = "Paris"
        };

        // Act
        var result = await runner.RunTestCaseAsync(agent, testCase);

        // Assert
        Assert.Equal("London", result.ActualOutput);
        Assert.False(result.Passed);
        Assert.True(result.Score < 0.7);

        await agent.DisposeAsync();
    }

    [Fact]
    public async Task RunTestSuiteAsync_ShouldExecuteAllTests()
    {
        // Arrange
        var llmClient = new MockLLMClient("Test output");
        var agent = new LlmAgent("TestAgent", llmClient);
        await agent.InitializeAsync();

        var runner = new EvaluationRunner();
        var suite = new TestSuite
        {
            Name = "Basic Tests",
            TestCases = new List<TestCase>
            {
                new() { Name = "Test1", Input = "Input1", ExpectedOutput = "Test output" },
                new() { Name = "Test2", Input = "Input2", ExpectedOutput = "Test output" },
                new() { Name = "Test3", Input = "Input3", ExpectedOutput = "Different" }
            }
        };

        // Act
        var result = await runner.RunTestSuiteAsync(agent, suite);

        // Assert
        Assert.Equal(3, result.TotalTests);
        Assert.Equal(2, result.PassedTests);
        Assert.Equal(1, result.FailedTests);
        Assert.True(result.PassRate > 0.6 && result.PassRate < 0.7);

        await agent.DisposeAsync();
    }

    [Fact]
    public void ExactMatchMetric_ShouldCalculateCorrectly()
    {
        // Arrange
        var metric = new ExactMatchMetric();

        // Act
        var match = metric.Calculate("Paris", "Paris");
        var noMatch = metric.Calculate("Paris", "London");

        // Assert
        Assert.Equal(1.0, match);
        Assert.Equal(0.0, noMatch);
    }

    [Fact]
    public void ContainsMetric_ShouldCalculateCorrectly()
    {
        // Arrange
        var metric = new ContainsMetric();

        // Act
        var contains = metric.Calculate("Paris", "The capital is Paris");
        var notContains = metric.Calculate("Paris", "London is great");

        // Assert
        Assert.Equal(1.0, contains);
        Assert.Equal(0.0, notContains);
    }

    [Fact]
    public void SimilarityMetric_ShouldCalculateCorrectly()
    {
        // Arrange
        var metric = new SimilarityMetric();

        // Act
        var highSimilarity = metric.Calculate("the quick brown fox", "the quick brown fox");
        var lowSimilarity = metric.Calculate("the quick brown fox", "completely different text");

        // Assert
        Assert.Equal(1.0, highSimilarity);
        Assert.True(lowSimilarity < 0.5);
    }
}
