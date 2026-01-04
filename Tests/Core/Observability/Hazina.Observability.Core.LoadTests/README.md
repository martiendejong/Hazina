# Hazina Observability Load Tests

Load and stress testing for the Hazina Observability system using NBomber.

## Overview

This project contains comprehensive load tests for:

- **Telemetry Tracking**: Load tests for TrackOperation, TrackCost, TrackHallucination, etc.
- **Activity Tracing**: Load tests for concurrent OpenTelemetry activity creation
- **Metrics Recording**: Load tests for high-volume Prometheus metric operations

## Test Scenarios

### Telemetry Load Tests

1. **RunSustainedLoad**
   - **Pattern**: Sustained load
   - **Rate**: 100 requests/second
   - **Duration**: 30 seconds
   - **Purpose**: Validate system stability under continuous load

2. **RunSpikeLoad**
   - **Pattern**: Spike (normal → spike → normal)
   - **Rates**: 50/sec → 500/sec → 50/sec
   - **Duration**: 25 seconds total
   - **Purpose**: Test system resilience during traffic spikes

3. **RunStressTest**
   - **Pattern**: Ramping load
   - **Rates**: 100/sec → 300/sec → 500/sec
   - **Duration**: 30 seconds
   - **Purpose**: Find system breaking point and capacity limits

### Activity Load Tests

1. **RunConcurrentActivityCreation**
   - **Pattern**: High concurrency
   - **Rate**: 200 activities/second
   - **Duration**: 30 seconds
   - **Purpose**: Test OpenTelemetry activity creation under load

2. **RunNeuroChainActivityLoad**
   - **Pattern**: Multi-layer operations
   - **Rate**: 50 operations/second
   - **Duration**: 30 seconds
   - **Purpose**: Test NeuroChain activity tracking with 2-5 layers

3. **RunActivityErrorHandlingLoad**
   - **Pattern**: High error rate (20%)
   - **Rate**: 100 operations/second
   - **Duration**: 20 seconds
   - **Purpose**: Validate error recording under load

### Metrics Load Tests

1. **RunHighVolumeMetrics**
   - **Pattern**: High volume
   - **Rate**: 500 metric updates/second
   - **Duration**: 30 seconds
   - **Purpose**: Test Prometheus metric performance

2. **RunMetricsBurstLoad**
   - **Pattern**: Burst (10 metrics per operation)
   - **Rate**: 100 operations/second
   - **Duration**: 10 seconds
   - **Purpose**: Test metric system under burst conditions

3. **RunComplexMetricsScenario**
   - **Pattern**: Mixed metrics (operations, hallucinations, failovers, NeuroChain)
   - **Rate**: 150 operations/second
   - **Duration**: 30 seconds
   - **Purpose**: Test realistic production-like metric patterns

## Running Load Tests

### Run All Load Tests

```bash
cd tests/Core/Observability/Hazina.Observability.Core.LoadTests
dotnet run -c Release
```

**Note**: Always run in Release mode for accurate performance measurements.

### Run Individual Test Scenarios

Modify `Program.cs` to comment out scenarios you don't want to run:

```csharp
// Comment out scenarios not needed
// TelemetryLoadTests.RunSustainedLoad();

TelemetryLoadTests.RunSpikeLoad(); // Only run spike test
```

## Interpreting Results

NBomber provides detailed reports including:

- **RPS (Requests Per Second)**: Actual throughput achieved
- **Latency Percentiles**: p50, p75, p95, p99
- **OK/Failed Requests**: Success rate
- **Data Transfer**: Request/response sizes
- **Status Codes**: Distribution of response statuses

### Key Metrics to Monitor

1. **Success Rate**: Should be > 99% under normal load
2. **p95 Latency**: Should remain stable and low
3. **RPS**: Should match or exceed target rate
4. **Memory**: Monitor for memory leaks during sustained tests

## Performance Goals

Based on benchmark results, expected performance:

- **Telemetry operations**: < 1 μs per operation
- **Activity creation**: < 5 μs per operation
- **Metric recording**: < 500 ns per operation

Under load:
- **Sustained Load**: System should handle 100 ops/sec indefinitely
- **Spike Load**: System should recover gracefully from 500 ops/sec spikes
- **Stress Test**: System should handle 300+ ops/sec before degradation

## Continuous Monitoring

Run load tests regularly to:

1. **Detect Performance Regressions**: Compare results over time
2. **Validate Optimizations**: Measure impact of performance improvements
3. **Capacity Planning**: Determine scaling requirements
4. **SLA Validation**: Ensure system meets performance SLAs

## Integration with CI/CD

### Example GitHub Actions Workflow

```yaml
name: Load Tests

on:
  schedule:
    - cron: '0 2 * * 0'  # Weekly on Sunday at 2 AM
  workflow_dispatch:

jobs:
  load-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '9.0.x'
      - name: Run Load Tests
        run: |
          cd tests/Core/Observability/Hazina.Observability.Core.LoadTests
          dotnet run -c Release
```

## Troubleshooting

### High Failure Rate

- **Check System Resources**: CPU, memory, disk I/O
- **Verify Dependencies**: Ensure all services are running
- **Review Logs**: Check for error patterns
- **Reduce Load**: Lower RPS to find stable operating point

### Inconsistent Results

- **Run in Release Mode**: Debug builds skew results
- **Close Background Apps**: Minimize interference
- **Multiple Runs**: Average results from 3-5 runs
- **Warm-up Period**: Ensure JIT compilation is complete

### Memory Issues

- **Check for Leaks**: Monitor memory over time
- **Review Object Lifetime**: Ensure proper disposal
- **GC Pressure**: Check Gen0/Gen1/Gen2 collection rates

## Report Generation

NBomber generates HTML reports in `bin/Release/net9.0/reports/`:

- **Timeline Charts**: Visual representation of load over time
- **Statistics Tables**: Detailed metrics per scenario
- **Status Code Distribution**: Success/failure breakdown

## Customization

To add new load test scenarios:

1. Create a new method in the appropriate test class
2. Define the scenario using `Scenario.Create()`
3. Configure load simulation pattern
4. Add to `Program.cs` execution sequence

Example:

```csharp
public static void RunCustomScenario()
{
    var scenario = Scenario.Create("custom_scenario", async context =>
    {
        // Your test logic here
        return Response.Ok();
    })
    .WithoutWarmUp()
    .WithLoadSimulations(
        Simulation.Inject(rate: 100, interval: TimeSpan.FromSeconds(1),
                         during: TimeSpan.FromSeconds(20))
    );

    NBomberRunner.RegisterScenarios(scenario).Run();
}
```

## References

- [NBomber Documentation](https://nbomber.com/docs/overview)
- [Load Testing Best Practices](https://nbomber.com/docs/load-testing-basics)
- [Hazina Observability Core](../../../src/Core/Observability/)
