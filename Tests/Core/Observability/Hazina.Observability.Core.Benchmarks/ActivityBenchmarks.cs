using System.Diagnostics;
using BenchmarkDotNet.Attributes;
using Hazina.Observability.Core.Tracing;

namespace Hazina.Observability.Core.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class ActivityBenchmarks
{
    private ActivityListener? _listener;
    private const string Provider = "openai";
    private const string Model = "gpt-4";

    [GlobalSetup]
    public void Setup()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == HazinaActivitySource.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(_listener);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _listener?.Dispose();
    }

    [Benchmark]
    public void StartLLMOperation()
    {
        using var activity = HazinaActivitySource.StartLLMOperation("chat_completion", Provider, Model);
    }

    [Benchmark]
    public void StartLLMOperation_WithCost()
    {
        using var activity = HazinaActivitySource.StartLLMOperation("chat_completion", Provider, Model);
        HazinaActivitySource.RecordCost(activity, 0.001m, 100, 50);
    }

    [Benchmark]
    public void StartNeuroChainOperation()
    {
        using var activity = HazinaActivitySource.StartNeuroChainOperation("Test prompt", 3);
    }

    [Benchmark]
    public void StartNeuroChainOperation_WithLayers()
    {
        using var activity = HazinaActivitySource.StartNeuroChainOperation("Test prompt", 3);
        HazinaActivitySource.RecordLayerResult(activity, 0, "openai", 0.85, true);
        HazinaActivitySource.RecordLayerResult(activity, 1, "anthropic", 0.92, true);
        HazinaActivitySource.RecordLayerResult(activity, 2, "openai", 0.95, true);
    }

    [Benchmark]
    public void StartFailoverOperation()
    {
        using var activity = HazinaActivitySource.StartFailoverOperation("openai", "anthropic");
    }

    [Benchmark]
    public void RecordError()
    {
        using var activity = HazinaActivitySource.StartLLMOperation("chat_completion", Provider, Model);
        var exception = new InvalidOperationException("Test error");
        HazinaActivitySource.RecordError(activity, exception);
    }

    [Benchmark]
    public void RecordHallucination()
    {
        using var activity = HazinaActivitySource.StartLLMOperation("chat_completion", Provider, Model);
        HazinaActivitySource.RecordHallucination(activity, "factual_error", 0.85);
    }

    [Benchmark]
    public void CompleteOperation()
    {
        // Benchmark a complete operation with all tags
        using var activity = HazinaActivitySource.StartLLMOperation("chat_completion", Provider, Model);
        HazinaActivitySource.RecordCost(activity, 0.001m, 100, 50);
    }
}
