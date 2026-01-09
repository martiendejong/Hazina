using FluentAssertions;
using Hazina.LLMs;
using Hazina.Tools.Models;
using Hazina.Tools.Services.ToolAgent.Models;
using Hazina.Tools.Services.ToolAgent.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Hazina.Tools.Services.ToolAgent.Tests;

public class ToolAgentServiceTests
{
    private readonly Mock<ILogger<ToolAgentService>> _loggerMock;
    private readonly Mock<ILLMClient> _llmClientMock;
    private Func<string, Task<ILLMClient>> _clientFactory;

    public ToolAgentServiceTests()
    {
        _loggerMock = new Mock<ILogger<ToolAgentService>>();
        _llmClientMock = new Mock<ILLMClient>();
        _clientFactory = _ => Task.FromResult(_llmClientMock.Object);
    }

    [Fact]
    public async Task ExecuteActionAsync_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var service = new ToolAgentService(_clientFactory, _loggerMock.Object);
        var request = new ToolAgentRequest
        {
            Action = "update_brand_profile",
            ContextHint = "Italian restaurant",
            ProjectId = "test-project",
            ChatId = "test-chat",
            UserId = "test-user"
        };

        var mockResponse = new LLMResponse<string>(
            "Brand profile updated successfully",
            new TokenUsageInfo { InputTokens = 50, OutputTokens = 50 }
        );

        _llmClientMock
            .Setup(x => x.GetResponse(
                It.IsAny<List<HazinaChatMessage>>(),
                It.IsAny<HazinaChatResponseFormat>(),
                It.IsAny<IToolsContext>(),
                It.IsAny<List<ImageData>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        var result = await service.ExecuteActionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.Summary.Should().Be("Brand profile updated successfully");
        result.ExecutedTools.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteActionAsync_WithEmptyAction_ReturnsFailure()
    {
        // Arrange
        var service = new ToolAgentService(_clientFactory, _loggerMock.Object);
        var request = new ToolAgentRequest
        {
            Action = "",
            ProjectId = "test-project",
            ChatId = "test-chat"
        };

        // Act
        var result = await service.ExecuteActionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Action parameter is required");
    }

    [Fact]
    public async Task ExecuteActionAsync_WithException_ReturnsFailure()
    {
        // Arrange
        var service = new ToolAgentService(_clientFactory, _loggerMock.Object);
        var request = new ToolAgentRequest
        {
            Action = "update_brand_profile",
            ProjectId = "test-project",
            ChatId = "test-chat"
        };

        _llmClientMock
            .Setup(x => x.GetResponse(
                It.IsAny<List<HazinaChatMessage>>(),
                It.IsAny<HazinaChatResponseFormat>(),
                It.IsAny<IToolsContext>(),
                It.IsAny<List<ImageData>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("LLM error"));

        // Act
        var result = await service.ExecuteActionAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("LLM error");
    }

    [Fact]
    public async Task ExecuteActionAsync_CallsClientFactory_WithCorrectTaskName()
    {
        // Arrange
        string capturedTaskName = null;
        _clientFactory = taskName =>
        {
            capturedTaskName = taskName;
            return Task.FromResult(_llmClientMock.Object);
        };

        var service = new ToolAgentService(_clientFactory, _loggerMock.Object);
        var request = new ToolAgentRequest
        {
            Action = "update_brand_profile",
            ProjectId = "test-project",
            ChatId = "test-chat"
        };

        var mockResponse = new LLMResponse<string>(
            "Success",
            new TokenUsageInfo { InputTokens = 50, OutputTokens = 50 }
        );

        _llmClientMock
            .Setup(x => x.GetResponse(
                It.IsAny<List<HazinaChatMessage>>(),
                It.IsAny<HazinaChatResponseFormat>(),
                It.IsAny<IToolsContext>(),
                It.IsAny<List<ImageData>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockResponse);

        // Act
        await service.ExecuteActionAsync(request);

        // Assert
        capturedTaskName.Should().Be("tool.orchestration");
    }

    [Fact]
    public async Task GetAvailableActionsAsync_ReturnsExpectedActions()
    {
        // Arrange
        var service = new ToolAgentService(_clientFactory, _loggerMock.Object);

        // Act
        var actions = await service.GetAvailableActionsAsync();

        // Assert
        actions.Should().NotBeNull();
        actions.Should().HaveCount(4);
        actions.Should().Contain(a => a.Action == "update_brand_profile");
        actions.Should().Contain(a => a.Action == "generate_logo");
        actions.Should().Contain(a => a.Action == "store_conversation_data");
        actions.Should().Contain(a => a.Action == "show_guidance");
    }

    [Theory]
    [InlineData("update_brand_profile", "Italian restaurant")]
    [InlineData("generate_logo", "minimalist design")]
    [InlineData("store_conversation_data", "restaurant info")]
    [InlineData("show_guidance", "next steps")]
    public async Task ExecuteActionAsync_WithContextHint_IncludesInRequest(string action, string contextHint)
    {
        // Arrange
        var service = new ToolAgentService(_clientFactory, _loggerMock.Object);
        var request = new ToolAgentRequest
        {
            Action = action,
            ContextHint = contextHint,
            ProjectId = "test-project",
            ChatId = "test-chat"
        };

        List<HazinaChatMessage> capturedMessages = null;
        _llmClientMock
            .Setup(x => x.GetResponse(
                It.IsAny<List<HazinaChatMessage>>(),
                It.IsAny<HazinaChatResponseFormat>(),
                It.IsAny<IToolsContext>(),
                It.IsAny<List<ImageData>>(),
                It.IsAny<CancellationToken>()))
            .Callback<List<HazinaChatMessage>, HazinaChatResponseFormat, IToolsContext, List<ImageData>, CancellationToken>(
                (msgs, _, _, _, _) => capturedMessages = msgs)
            .ReturnsAsync(new LLMResponse<string>(
                "Success",
                new TokenUsageInfo { InputTokens = 50, OutputTokens = 50 }
            ));

        // Act
        await service.ExecuteActionAsync(request);

        // Assert
        capturedMessages.Should().NotBeNull();
        capturedMessages.Should().HaveCountGreaterThan(0);
        capturedMessages.Should().Contain(m => m.Text.Contains(contextHint));
    }
}
