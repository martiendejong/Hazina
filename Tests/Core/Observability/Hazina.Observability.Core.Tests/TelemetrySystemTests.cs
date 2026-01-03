using FluentAssertions;
using Hazina.Observability.Core;
using Hazina.Observability.Core.Metrics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Hazina.Observability.Core.Tests;

public class TelemetrySystemTests
{
    private readonly Mock<ILogger<TelemetrySystem>> _loggerMock;
    private readonly TelemetrySystem _telemetrySystem;

    public TelemetrySystemTests()
    {
        _loggerMock = new Mock<ILogger<TelemetrySystem>>();
        _telemetrySystem = new TelemetrySystem(_loggerMock.Object);
    }

    [Fact]
    public void TrackOperation_ShouldLogInformation()
    {
        // Arrange
        var operationId = "test-op-123";
        var provider = "openai";
        var duration = TimeSpan.FromMilliseconds(500);
        var success = true;
        var operationType = "chat_completion";

        // Act
        _telemetrySystem.TrackOperation(operationId, provider, duration, success, operationType);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(operationId)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TrackOperation_WithNullOperationType_ShouldUseDefault()
    {
        // Arrange
        var operationId = "test-op-124";
        var provider = "anthropic";
        var duration = TimeSpan.FromSeconds(1);
        var success = false;

        // Act
        _telemetrySystem.TrackOperation(operationId, provider, duration, success, null);

        // Assert - Should not throw and should log
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("unknown")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TrackHallucination_ShouldLogWarning()
    {
        // Arrange
        var operationId = "test-op-125";
        var hallucinationType = "factual_error";
        var confidence = 0.85;

        // Act
        _telemetrySystem.TrackHallucination(operationId, hallucinationType, confidence);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(hallucinationType)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TrackProviderFailover_ShouldLogWarning()
    {
        // Arrange
        var fromProvider = "openai";
        var toProvider = "anthropic";
        var reason = "rate_limit_exceeded";

        // Act
        _telemetrySystem.TrackProviderFailover(fromProvider, toProvider, reason);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(fromProvider) && v.ToString()!.Contains(toProvider)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TrackCost_ShouldLogInformation()
    {
        // Arrange
        var provider = "openai";
        var cost = 0.0042m;
        var inputTokens = 100;
        var outputTokens = 50;

        // Act
        _telemetrySystem.TrackCost(provider, cost, inputTokens, outputTokens);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(provider)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TrackNeuroChainLayers_ShouldLogInformation()
    {
        // Arrange
        var operationId = "test-op-126";
        var layersUsed = 3;
        var complexity = "high";

        // Act
        _telemetrySystem.TrackNeuroChainLayers(operationId, layersUsed, complexity);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(operationId) && v.ToString()!.Contains(layersUsed.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TrackFaultDetection_ShouldLogInformation()
    {
        // Arrange
        var operationId = "test-op-127";
        var faultType = "timeout";
        var wasCorrected = true;

        // Act
        _telemetrySystem.TrackFaultDetection(operationId, faultType, wasCorrected);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(faultType)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TrackOperation_MultipleProviders_ShouldTrackSeparately()
    {
        // Arrange & Act
        _telemetrySystem.TrackOperation("op1", "openai", TimeSpan.FromMilliseconds(100), true);
        _telemetrySystem.TrackOperation("op2", "anthropic", TimeSpan.FromMilliseconds(200), true);
        _telemetrySystem.TrackOperation("op3", "openai", TimeSpan.FromMilliseconds(150), false);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void TrackHallucination_WithDifferentConfidenceLevels_ShouldLog(double confidence)
    {
        // Arrange
        var operationId = $"test-op-conf-{confidence}";
        var hallucinationType = "test";

        // Act
        _telemetrySystem.TrackHallucination(operationId, hallucinationType, confidence);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void TrackCost_WithZeroTokens_ShouldStillLog()
    {
        // Arrange
        var provider = "test-provider";
        var cost = 0.0m;

        // Act
        _telemetrySystem.TrackCost(provider, cost, 0, 0);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
