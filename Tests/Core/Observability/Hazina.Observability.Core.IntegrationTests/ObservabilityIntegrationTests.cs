using System.Diagnostics;
using FluentAssertions;
using Hazina.Observability.Core;
using Hazina.Observability.Core.Metrics;
using Hazina.Observability.Core.Tracing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Hazina.Observability.Core.IntegrationTests;

public class ObservabilityIntegrationTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly ILogger<TelemetrySystem> _logger;
    private readonly TelemetrySystem _telemetrySystem;
    private readonly ActivityListener _activityListener;

    public ObservabilityIntegrationTests()
    {
        // Set up dependency injection
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<TelemetrySystem>();
        _serviceProvider = services.BuildServiceProvider();

        _logger = _serviceProvider.GetRequiredService<ILogger<TelemetrySystem>>();
        _telemetrySystem = _serviceProvider.GetRequiredService<TelemetrySystem>();

        // Set up activity listener for distributed tracing
        _activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == HazinaActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity => { },
            ActivityStopped = activity => { }
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    [Fact]
    public void EndToEnd_LLMOperation_ShouldTrackAllMetrics()
    {
        // Arrange
        var provider = "openai";
        var model = "gpt-4";
        var operationType = "chat_completion";
        var operationId = Guid.NewGuid().ToString();

        // Act - Simulate an LLM operation with telemetry
        using var activity = HazinaActivitySource.StartLLMOperation(operationType, provider, model);
        activity.Should().NotBeNull();

        // Simulate operation execution
        Thread.Sleep(50); // Simulate work

        // Record cost and tokens
        HazinaActivitySource.RecordCost(activity, 0.0023m, 150, 75);

        var duration = TimeSpan.FromMilliseconds(50);
        _telemetrySystem.TrackOperation(operationId, provider, duration, true, operationType);
        _telemetrySystem.TrackCost(provider, 0.0023m, 150, 75);

        // Assert - Verify activity was created with correct tags
        activity!.DisplayName.Should().Be($"llm.{operationType}");
        activity.GetTagItem("llm.provider").Should().Be(provider);
        activity.GetTagItem("llm.model").Should().Be(model);
        activity.GetTagItem("llm.cost_usd").Should().Be(0.0023);
        activity.GetTagItem("llm.tokens.input").Should().Be(150);
        activity.GetTagItem("llm.tokens.output").Should().Be(75);
        activity.GetTagItem("llm.tokens.total").Should().Be(225);

        // Verify metrics were recorded
        var costMetric = HazinaMetrics.TotalCost.WithLabels(provider).Value;
        costMetric.Should().BeGreaterThan(0);

        var tokenMetric = HazinaMetrics.TokensUsed.WithLabels(provider, "input").Value;
        tokenMetric.Should().BeGreaterOrEqualTo(150);
    }

    [Fact]
    public void EndToEnd_ProviderFailover_ShouldTrackFailoverAndContinue()
    {
        // Arrange
        var fromProvider = "openai";
        var toProvider = "anthropic";
        var reason = "rate_limit_exceeded";

        // Act
        using var failoverActivity = HazinaActivitySource.StartFailoverOperation(fromProvider, toProvider);
        failoverActivity.Should().NotBeNull();

        // Track failover
        _telemetrySystem.TrackProviderFailover(fromProvider, toProvider, reason);

        // Continue with new provider
        using var activity = HazinaActivitySource.StartLLMOperation("chat_completion", toProvider, "claude-3-opus");
        HazinaActivitySource.RecordCost(activity, 0.015m, 200, 100);
        _telemetrySystem.TrackCost(toProvider, 0.015m, 200, 100);

        // Assert
        failoverActivity!.DisplayName.Should().Be("provider.failover");
        failoverActivity.GetTagItem("provider.from").Should().Be(fromProvider);
        failoverActivity.GetTagItem("provider.to").Should().Be(toProvider);

        activity!.GetTagItem("llm.provider").Should().Be(toProvider);
        activity.GetTagItem("llm.model").Should().Be("claude-3-opus");
    }

    [Fact]
    public void EndToEnd_NeuroChainOperation_ShouldTrackLayersAndComplexity()
    {
        // Arrange
        var prompt = "Analyze this complex business scenario";
        var layerCount = 3;
        var operationId = Guid.NewGuid().ToString();

        // Act
        using var neuroChainActivity = HazinaActivitySource.StartNeuroChainOperation(prompt, layerCount);
        neuroChainActivity.Should().NotBeNull();

        // Simulate processing through layers
        HazinaActivitySource.RecordLayerResult(neuroChainActivity, 0, "openai", 0.85, true);
        HazinaActivitySource.RecordLayerResult(neuroChainActivity, 1, "anthropic", 0.92, true);
        HazinaActivitySource.RecordLayerResult(neuroChainActivity, 2, "openai", 0.95, true);

        _telemetrySystem.TrackNeuroChainLayers(operationId, layerCount, "high");

        // Assert
        neuroChainActivity!.DisplayName.Should().Be("neurochain.reason");
        neuroChainActivity.GetTagItem("neurochain.prompt_length").Should().Be(prompt.Length);
        neuroChainActivity.GetTagItem("neurochain.layer_count").Should().Be(layerCount);

        neuroChainActivity.GetTagItem("neurochain.layer_1.provider").Should().Be("openai");
        neuroChainActivity.GetTagItem("neurochain.layer_1.confidence").Should().Be(0.85);
        neuroChainActivity.GetTagItem("neurochain.layer_1.valid").Should().Be(true);

        neuroChainActivity.GetTagItem("neurochain.layer_2.provider").Should().Be("anthropic");
        neuroChainActivity.GetTagItem("neurochain.layer_2.confidence").Should().Be(0.92);

        neuroChainActivity.GetTagItem("neurochain.layer_3.provider").Should().Be("openai");
        neuroChainActivity.GetTagItem("neurochain.layer_3.confidence").Should().Be(0.95);
    }

    [Fact]
    public void EndToEnd_ErrorHandling_ShouldRecordErrorDetailsInActivity()
    {
        // Arrange
        var provider = "openai";
        var operationId = Guid.NewGuid().ToString();
        var exception = new InvalidOperationException("API rate limit exceeded");

        // Act
        using var activity = HazinaActivitySource.StartLLMOperation("chat_completion", provider);
        activity.Should().NotBeNull();

        // Simulate error
        HazinaActivitySource.RecordError(activity, exception);

        var duration = TimeSpan.FromMilliseconds(100);
        _telemetrySystem.TrackOperation(operationId, provider, duration, false, "chat_completion");

        // Assert
        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be(exception.Message);
        activity.GetTagItem("error.type").Should().Be(exception.GetType().FullName);
        activity.GetTagItem("error.message").Should().Be(exception.Message);
    }

    [Fact]
    public void EndToEnd_HallucinationDetection_ShouldTrackQualityIssues()
    {
        // Arrange
        var provider = "openai";
        var operationId = Guid.NewGuid().ToString();
        var hallucinationType = "factual_error";
        var confidence = 0.88;

        // Act
        using var activity = HazinaActivitySource.StartLLMOperation("chat_completion", provider);
        activity.Should().NotBeNull();

        // Detect hallucination
        HazinaActivitySource.RecordHallucination(activity, hallucinationType, confidence);
        _telemetrySystem.TrackHallucination(operationId, hallucinationType, confidence);

        // Assert
        activity!.GetTagItem("hallucination.detected").Should().Be(true);
        activity.GetTagItem("hallucination.type").Should().Be(hallucinationType);
        activity.GetTagItem("hallucination.confidence").Should().Be(confidence);

        var hallucinationMetric = HazinaMetrics.HallucinationsDetected.WithLabels(hallucinationType).Value;
        hallucinationMetric.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EndToEnd_FaultDetection_ShouldTrackDetectionAndCorrection()
    {
        // Arrange
        var operationId = Guid.NewGuid().ToString();
        var faultType = "timeout";
        var wasCorrected = true;

        // Act
        _telemetrySystem.TrackFaultDetection(operationId, faultType, wasCorrected);

        // Assert - Verify fault detection was tracked
        var faultMetric = HazinaMetrics.FaultsDetected.WithLabels(faultType, wasCorrected.ToString().ToLower()).Value;
        faultMetric.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EndToEnd_ConcurrentOperations_ShouldTrackAllIndependently()
    {
        // Arrange
        var tasks = new List<Task>();

        // Act - Simulate concurrent operations
        for (int i = 0; i < 5; i++)
        {
            var taskIndex = i;
            var task = Task.Run(() =>
            {
                var provider = taskIndex % 2 == 0 ? "openai" : "anthropic";
                var operationId = Guid.NewGuid().ToString();

                using var activity = HazinaActivitySource.StartLLMOperation("chat_completion", provider, $"model-{taskIndex}");
                Thread.Sleep(10); // Simulate work

                HazinaActivitySource.RecordCost(activity, 0.001m * (taskIndex + 1), 100 * (taskIndex + 1), 50 * (taskIndex + 1));

                var duration = TimeSpan.FromMilliseconds(10);
                _telemetrySystem.TrackOperation(operationId, provider, duration, true, "chat_completion");
                _telemetrySystem.TrackCost(provider, 0.001m * (taskIndex + 1), 100 * (taskIndex + 1), 50 * (taskIndex + 1));
            });
            tasks.Add(task);
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - Verify all operations were tracked
        var openaiOps = HazinaMetrics.OperationsTotal.WithLabels("openai", "true").Value;
        var anthropicOps = HazinaMetrics.OperationsTotal.WithLabels("anthropic", "true").Value;

        (openaiOps + anthropicOps).Should().BeGreaterOrEqualTo(5);
    }

    [Fact]
    public void EndToEnd_MetricsAggregation_ShouldAccumulateCorrectly()
    {
        // Arrange
        var provider = "openai";
        var baselineCost = HazinaMetrics.TotalCost.WithLabels(provider).Value;
        var baselineTokens = HazinaMetrics.TokensUsed.WithLabels(provider, "input").Value;

        // Act - Perform multiple operations
        for (int i = 0; i < 3; i++)
        {
            _telemetrySystem.TrackCost(provider, 0.001m, 100, 50);
        }

        // Assert
        var finalCost = HazinaMetrics.TotalCost.WithLabels(provider).Value;
        var finalTokens = HazinaMetrics.TokensUsed.WithLabels(provider, "input").Value;

        (finalCost - baselineCost).Should().BeApproximately(0.003, 0.0001);
        (finalTokens - baselineTokens).Should().Be(300);
    }

    public void Dispose()
    {
        _activityListener?.Dispose();
        _serviceProvider?.Dispose();
    }
}
