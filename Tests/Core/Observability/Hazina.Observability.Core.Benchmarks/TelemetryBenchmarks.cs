using BenchmarkDotNet.Attributes;
using Hazina.Observability.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hazina.Observability.Core.Benchmarks;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 5)]
public class TelemetryBenchmarks
{
    private TelemetrySystem? _telemetrySystem;
    private const string Provider = "openai";
    private const string OperationType = "chat_completion";

    [GlobalSetup]
    public void Setup()
    {
        var logger = NullLogger<TelemetrySystem>.Instance;
        _telemetrySystem = new TelemetrySystem(logger);
    }

    [Benchmark]
    public void TrackOperation()
    {
        var operationId = Guid.NewGuid().ToString();
        var duration = TimeSpan.FromMilliseconds(100);
        _telemetrySystem!.TrackOperation(operationId, Provider, duration, true, OperationType);
    }

    [Benchmark]
    public void TrackCost()
    {
        _telemetrySystem!.TrackCost(Provider, 0.001m, 100, 50);
    }

    [Benchmark]
    public void TrackHallucination()
    {
        var operationId = Guid.NewGuid().ToString();
        _telemetrySystem!.TrackHallucination(operationId, "factual_error", 0.85);
    }

    [Benchmark]
    public void TrackProviderFailover()
    {
        _telemetrySystem!.TrackProviderFailover("openai", "anthropic", "rate_limit");
    }

    [Benchmark]
    public void TrackNeuroChainLayers()
    {
        var operationId = Guid.NewGuid().ToString();
        _telemetrySystem!.TrackNeuroChainLayers(operationId, 3, "high");
    }

    [Benchmark]
    public void TrackFaultDetection()
    {
        var operationId = Guid.NewGuid().ToString();
        _telemetrySystem!.TrackFaultDetection(operationId, "timeout", true);
    }

    [Benchmark]
    public void TrackAllOperations()
    {
        // Benchmark combined operations
        var operationId = Guid.NewGuid().ToString();
        var duration = TimeSpan.FromMilliseconds(100);

        _telemetrySystem!.TrackOperation(operationId, Provider, duration, true, OperationType);
        _telemetrySystem!.TrackCost(Provider, 0.001m, 100, 50);
    }
}
