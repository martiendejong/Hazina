using BenchmarkDotNet.Attributes;
using Hazina.Observability.Core.Metrics;

namespace Hazina.Observability.Core.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class MetricsBenchmarks
{
    private const string Provider = "openai";

    [Benchmark]
    public void IncrementOperationsTotal()
    {
        HazinaMetrics.OperationsTotal.WithLabels(Provider, "true").Inc();
    }

    [Benchmark]
    public void RecordOperationDuration()
    {
        HazinaMetrics.OperationDuration.WithLabels(Provider).Observe(100);
    }

    [Benchmark]
    public void IncrementTotalCost()
    {
        HazinaMetrics.TotalCost.WithLabels(Provider).Inc(0.001);
    }

    [Benchmark]
    public void IncrementTokensUsed()
    {
        HazinaMetrics.TokensUsed.WithLabels(Provider, "input").Inc(100);
        HazinaMetrics.TokensUsed.WithLabels(Provider, "output").Inc(50);
    }

    [Benchmark]
    public void UpdateProviderHealth()
    {
        HazinaMetrics.ProviderHealth.WithLabels(Provider).Set(0.95);
    }

    [Benchmark]
    public void IncrementProviderFailovers()
    {
        HazinaMetrics.ProviderFailovers.WithLabels("openai", "anthropic").Inc();
    }

    [Benchmark]
    public void IncrementHallucinationsDetected()
    {
        HazinaMetrics.HallucinationsDetected.WithLabels("factual_error").Inc();
    }

    [Benchmark]
    public void IncrementFaultsDetected()
    {
        HazinaMetrics.FaultsDetected.WithLabels("timeout", "true").Inc();
    }

    [Benchmark]
    public void IncrementNeuroChainLayersUsed()
    {
        HazinaMetrics.NeuroChainLayersUsed.WithLabels("3", "high").Inc();
    }

    [Benchmark]
    public void RecordAllMetrics()
    {
        // Benchmark recording multiple metrics in sequence
        HazinaMetrics.OperationsTotal.WithLabels(Provider, "true").Inc();
        HazinaMetrics.OperationDuration.WithLabels(Provider).Observe(100);
        HazinaMetrics.TotalCost.WithLabels(Provider).Inc(0.001);
        HazinaMetrics.TokensUsed.WithLabels(Provider, "input").Inc(100);
        HazinaMetrics.TokensUsed.WithLabels(Provider, "output").Inc(50);
    }
}
