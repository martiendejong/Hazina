using System.Diagnostics;

namespace Hazina.Observability.Core.Tracing;

/// <summary>
/// ActivitySource for Hazina distributed tracing
/// Uses System.Diagnostics.Activity for OpenTelemetry-compatible tracing
/// </summary>
public static class HazinaActivitySource
{
    /// <summary>
    /// Activity source name for Hazina operations
    /// </summary>
    public const string SourceName = "Hazina.AI";

    /// <summary>
    /// Activity source version
    /// </summary>
    public const string SourceVersion = "1.0.0";

    /// <summary>
    /// Main activity source for all Hazina operations
    /// </summary>
    public static readonly ActivitySource Source = new(SourceName, SourceVersion);

    /// <summary>
    /// Start a new activity for an LLM operation
    /// </summary>
    public static Activity? StartLLMOperation(string operationName, string provider, string? model = null)
    {
        var activity = Source.StartActivity($"llm.{operationName}", ActivityKind.Client);
        activity?.SetTag("llm.provider", provider);
        if (model != null)
        {
            activity?.SetTag("llm.model", model);
        }
        return activity;
    }

    /// <summary>
    /// Start a new activity for a NeuroChain operation
    /// </summary>
    public static Activity? StartNeuroChainOperation(string prompt, int layerCount)
    {
        var activity = Source.StartActivity("neurochain.reason", ActivityKind.Internal);
        activity?.SetTag("neurochain.prompt_length", prompt.Length);
        activity?.SetTag("neurochain.layer_count", layerCount);
        return activity;
    }

    /// <summary>
    /// Start a new activity for a provider failover
    /// </summary>
    public static Activity? StartFailoverOperation(string fromProvider, string toProvider)
    {
        var activity = Source.StartActivity("provider.failover", ActivityKind.Internal);
        activity?.SetTag("provider.from", fromProvider);
        activity?.SetTag("provider.to", toProvider);
        return activity;
    }

    /// <summary>
    /// Add cost information to current activity
    /// </summary>
    public static void RecordCost(Activity? activity, decimal cost, int inputTokens, int outputTokens)
    {
        if (activity == null) return;

        activity.SetTag("llm.cost_usd", (double)cost);
        activity.SetTag("llm.tokens.input", inputTokens);
        activity.SetTag("llm.tokens.output", outputTokens);
        activity.SetTag("llm.tokens.total", inputTokens + outputTokens);
    }

    /// <summary>
    /// Record an error in the current activity
    /// </summary>
    public static void RecordError(Activity? activity, Exception exception)
    {
        if (activity == null) return;

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("error.type", exception.GetType().FullName);
        activity.SetTag("error.message", exception.Message);
        activity.SetTag("error.stacktrace", exception.StackTrace);
    }

    /// <summary>
    /// Record hallucination detection
    /// </summary>
    public static void RecordHallucination(Activity? activity, string hallucinationType, double confidence)
    {
        if (activity == null) return;

        activity.SetTag("hallucination.detected", true);
        activity.SetTag("hallucination.type", hallucinationType);
        activity.SetTag("hallucination.confidence", confidence);
    }

    /// <summary>
    /// Record layer result in NeuroChain
    /// </summary>
    public static void RecordLayerResult(Activity? activity, int layerIndex, string provider, double confidence, bool isValid)
    {
        if (activity == null) return;

        var prefix = $"neurochain.layer_{layerIndex + 1}";
        activity.SetTag($"{prefix}.provider", provider);
        activity.SetTag($"{prefix}.confidence", confidence);
        activity.SetTag($"{prefix}.valid", isValid);
    }
}
