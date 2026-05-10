using FluentAssertions;
using Hazina.AI.Routing.Models;
using Hazina.AI.Routing.Services;
using Hazina.LLMs;
using Microsoft.Extensions.Logging;
using Moq;

namespace Hazina.AI.Routing.Tests;

public class PolymathDelegationServiceTests
{
    private readonly Mock<ILLMClient> _mockLlm;
    private readonly PolymathDelegationService _service;

    public PolymathDelegationServiceTests()
    {
        _mockLlm = new Mock<ILLMClient>();
        var mockLogger = new Mock<ILogger<PolymathDelegationService>>();
        _service = new PolymathDelegationService(_mockLlm.Object, mockLogger.Object);
    }

    private void SetupLlmResponse(string response)
    {
        _mockLlm.Setup(l => l.GetResponse(
                It.IsAny<List<HazinaChatMessage>>(),
                It.IsAny<HazinaChatResponseFormat>(),
                It.IsAny<IToolsContext?>(),
                It.IsAny<List<ImageData>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse<string>(response, new TokenUsageInfo
            {
                InputTokens = 100,
                OutputTokens = 50
            }));
    }

    [Fact]
    public async Task AnalyzeAsync_SingleAgent_SkipsFanOut()
    {
        // Arrange
        SetupLlmResponse("Single agent response");

        // Act
        var result = await _service.AnalyzeAsync("Test query", 1, SynthesisStrategy.Best);

        // Assert
        result.AgentCount.Should().Be(1);
        result.SynthesizedResult.Should().Be("Single agent response");
        result.Strategy.Should().Be(SynthesisStrategy.Best);
    }

    [Fact]
    public async Task AnalyzeAsync_ThreeAgents_ConsensusStrategy_ReturnsSynthesizedResult()
    {
        // Arrange - all calls return different perspectives
        var callCount = 0;
        _mockLlm.Setup(l => l.GetResponse(
                It.IsAny<List<HazinaChatMessage>>(),
                It.IsAny<HazinaChatResponseFormat>(),
                It.IsAny<IToolsContext?>(),
                It.IsAny<List<ImageData>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new LLMResponse<string>(
                    $"Response from agent {callCount}",
                    new TokenUsageInfo { InputTokens = 100, OutputTokens = 50 });
            });

        // Act
        var result = await _service.AnalyzeAsync("Analyze markets", 3, SynthesisStrategy.Consensus);

        // Assert
        result.AgentCount.Should().Be(3);
        result.SynthesizedResult.Should().NotBeNullOrEmpty();
        result.AgentResponses.Should().HaveCount(3);
        // 3 agent calls + 1 synthesis call = 4 total
        _mockLlm.Verify(l => l.GetResponse(
            It.IsAny<List<HazinaChatMessage>>(),
            It.IsAny<HazinaChatResponseFormat>(),
            It.IsAny<IToolsContext?>(),
            It.IsAny<List<ImageData>?>(),
            It.IsAny<CancellationToken>()), Times.AtLeast(4));
    }

    [Fact]
    public async Task AnalyzeAsync_CostGuardrail_CapsAtMaxAgents()
    {
        // Arrange
        SetupLlmResponse("Capped response");

        // Act - request 10 agents (should be capped to 3)
        var result = await _service.AnalyzeAsync("Analyze everything", 10, SynthesisStrategy.Best);

        // Assert
        result.AgentCount.Should().BeLessThanOrEqualTo(_service.MaxAgents);
    }

    [Fact]
    public async Task AnalyzeAsync_MergeStrategy_CombinesFindings()
    {
        // Arrange
        var callCount = 0;
        _mockLlm.Setup(l => l.GetResponse(
                It.IsAny<List<HazinaChatMessage>>(),
                It.IsAny<HazinaChatResponseFormat>(),
                It.IsAny<IToolsContext?>(),
                It.IsAny<List<ImageData>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new LLMResponse<string>(
                    $"Merged insight {callCount}",
                    new TokenUsageInfo { InputTokens = 80, OutputTokens = 40 });
            });

        // Act
        var result = await _service.AnalyzeAsync("Compare approaches", 2, SynthesisStrategy.Merge);

        // Assert
        result.AgentCount.Should().Be(2);
        result.Strategy.Should().Be(SynthesisStrategy.Merge);
        result.SynthesizedResult.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AnalyzeAsync_TracksTokenUsage()
    {
        // Arrange
        _mockLlm.Setup(l => l.GetResponse(
                It.IsAny<List<HazinaChatMessage>>(),
                It.IsAny<HazinaChatResponseFormat>(),
                It.IsAny<IToolsContext?>(),
                It.IsAny<List<ImageData>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse<string>(
                "Response",
                new TokenUsageInfo { InputTokens = 200, OutputTokens = 100 }));

        // Act
        var result = await _service.AnalyzeAsync("Query", 1, SynthesisStrategy.Best);

        // Assert
        result.TotalInputTokens.Should().Be(200);
        result.TotalOutputTokens.Should().Be(100);
    }

    [Fact]
    public void MaxAgents_Returns3()
    {
        _service.MaxAgents.Should().Be(3);
    }

    [Fact]
    public async Task AnalyzeAsync_BestStrategy_SelectsLongestResponse()
    {
        // Arrange - set up responses of different lengths
        var responses = new Queue<string>(["Short", "This is a much longer and more detailed response that should win", "Medium response here"]);
        _mockLlm.Setup(l => l.GetResponse(
                It.IsAny<List<HazinaChatMessage>>(),
                It.IsAny<HazinaChatResponseFormat>(),
                It.IsAny<IToolsContext?>(),
                It.IsAny<List<ImageData>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new LLMResponse<string>(
                responses.Dequeue(),
                new TokenUsageInfo { InputTokens = 100, OutputTokens = 50 }));

        // Act
        var result = await _service.AnalyzeAsync("Test", 3, SynthesisStrategy.Best);

        // Assert - Best strategy should pick the longest response
        result.SynthesizedResult.Should().Contain("much longer and more detailed");
    }
}
