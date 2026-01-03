using System.Diagnostics;
using FluentAssertions;
using Hazina.Observability.Core.Tracing;
using Xunit;

namespace Hazina.Observability.Core.Tests;

public class HazinaActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public HazinaActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == HazinaActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [Fact]
    public void StartLLMOperation_ShouldCreateActivityWithCorrectTags()
    {
        // Arrange
        var operationName = "chat_completion";
        var provider = "openai";
        var model = "gpt-4";

        // Act
        using var activity = HazinaActivitySource.StartLLMOperation(operationName, provider, model);

        // Assert
        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be($"llm.{operationName}");
        activity.Tags.Should().Contain(new KeyValuePair<string, string?>("llm.provider", provider));
        activity.Tags.Should().Contain(new KeyValuePair<string, string?>("llm.model", model));
    }

    [Fact]
    public void StartLLMOperation_WithoutModel_ShouldNotIncludeModelTag()
    {
        // Arrange
        var operationName = "embedding";
        var provider = "anthropic";

        // Act
        using var activity = HazinaActivitySource.StartLLMOperation(operationName, provider);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(new KeyValuePair<string, string?>("llm.provider", provider));
        activity.Tags.Should().NotContain(tag => tag.Key == "llm.model");
    }

    [Fact]
    public void StartNeuroChainOperation_ShouldCreateActivityWithPromptAndLayerInfo()
    {
        // Arrange
        var prompt = "Test prompt for NeuroChain reasoning";
        var layerCount = 3;

        // Act
        using var activity = HazinaActivitySource.StartNeuroChainOperation(prompt, layerCount);

        // Assert
        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("neurochain.reason");
        activity.GetTagItem("neurochain.prompt_length").Should().Be(prompt.Length);
        activity.GetTagItem("neurochain.layer_count").Should().Be(layerCount);
    }

    [Fact]
    public void StartFailoverOperation_ShouldCreateActivityWithProviderInfo()
    {
        // Arrange
        var fromProvider = "openai";
        var toProvider = "anthropic";

        // Act
        using var activity = HazinaActivitySource.StartFailoverOperation(fromProvider, toProvider);

        // Assert
        activity.Should().NotBeNull();
        activity!.DisplayName.Should().Be("provider.failover");
        activity.Tags.Should().Contain(new KeyValuePair<string, string?>("provider.from", fromProvider));
        activity.Tags.Should().Contain(new KeyValuePair<string, string?>("provider.to", toProvider));
    }

    [Fact]
    public void RecordCost_ShouldAddCostTagsToActivity()
    {
        // Arrange
        using var activity = HazinaActivitySource.StartLLMOperation("test", "openai");
        var cost = 0.0042m;
        var inputTokens = 100;
        var outputTokens = 50;

        // Act
        HazinaActivitySource.RecordCost(activity, cost, inputTokens, outputTokens);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("llm.cost_usd").Should().Be((double)cost);
        activity.GetTagItem("llm.tokens.input").Should().Be(inputTokens);
        activity.GetTagItem("llm.tokens.output").Should().Be(outputTokens);
        activity.GetTagItem("llm.tokens.total").Should().Be(inputTokens + outputTokens);
    }

    [Fact]
    public void RecordCost_WithNullActivity_ShouldNotThrow()
    {
        // Act
        Action act = () => HazinaActivitySource.RecordCost(null, 0.01m, 10, 5);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordError_ShouldSetErrorStatusAndTags()
    {
        // Arrange
        using var activity = HazinaActivitySource.StartLLMOperation("test", "openai");
        var exception = new InvalidOperationException("Test error message");

        // Act
        HazinaActivitySource.RecordError(activity, exception);

        // Assert
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.StatusDescription.Should().Be(exception.Message);
        activity.GetTagItem("error.type").Should().Be(exception.GetType().FullName);
        activity.GetTagItem("error.message").Should().Be(exception.Message);
    }

    [Fact]
    public void RecordError_WithNullActivity_ShouldNotThrow()
    {
        // Act
        Action act = () => HazinaActivitySource.RecordError(null, new Exception());

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RecordHallucination_ShouldAddHallucinationTags()
    {
        // Arrange
        using var activity = HazinaActivitySource.StartLLMOperation("test", "openai");
        var hallucinationType = "factual_error";
        var confidence = 0.85;

        // Act
        HazinaActivitySource.RecordHallucination(activity, hallucinationType, confidence);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("hallucination.detected").Should().Be(true);
        activity.GetTagItem("hallucination.type").Should().Be(hallucinationType);
        activity.GetTagItem("hallucination.confidence").Should().Be(confidence);
    }

    [Fact]
    public void RecordLayerResult_ShouldAddLayerTags()
    {
        // Arrange
        using var activity = HazinaActivitySource.StartNeuroChainOperation("test prompt", 3);
        var layerIndex = 0;
        var provider = "openai";
        var confidence = 0.92;
        var isValid = true;

        // Act
        HazinaActivitySource.RecordLayerResult(activity, layerIndex, provider, confidence, isValid);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("neurochain.layer_1.provider").Should().Be(provider);
        activity.GetTagItem("neurochain.layer_1.confidence").Should().Be(confidence);
        activity.GetTagItem("neurochain.layer_1.valid").Should().Be(isValid);
    }

    [Fact]
    public void RecordLayerResult_MultipleLayers_ShouldAddSeparateTags()
    {
        // Arrange
        using var activity = HazinaActivitySource.StartNeuroChainOperation("test", 3);

        // Act
        HazinaActivitySource.RecordLayerResult(activity, 0, "openai", 0.8, true);
        HazinaActivitySource.RecordLayerResult(activity, 1, "anthropic", 0.9, true);
        HazinaActivitySource.RecordLayerResult(activity, 2, "openai", 0.95, true);

        // Assert
        activity.Should().NotBeNull();
        activity!.GetTagItem("neurochain.layer_1.provider").Should().Be("openai");
        activity.GetTagItem("neurochain.layer_2.provider").Should().Be("anthropic");
        activity.GetTagItem("neurochain.layer_3.provider").Should().Be("openai");
    }

    [Fact]
    public void ActivitySource_ShouldHaveCorrectNameAndVersion()
    {
        // Assert
        HazinaActivitySource.SourceName.Should().Be("Hazina.AI");
        HazinaActivitySource.SourceVersion.Should().Be("1.0.0");
        HazinaActivitySource.Source.Name.Should().Be("Hazina.AI");
        HazinaActivitySource.Source.Version.Should().Be("1.0.0");
    }

    public void Dispose()
    {
        _listener?.Dispose();
    }
}
