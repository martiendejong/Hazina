# Hazina Observability Performance Benchmarks

Performance benchmarks for the Hazina Observability system using BenchmarkDotNet.

## Overview

This project contains comprehensive performance benchmarks for:

- **Telemetry Tracking**: Benchmarks for TrackOperation, TrackCost, TrackHallucination, etc.
- **Activity Tracing**: Benchmarks for creating and managing OpenTelemetry activities
- **Metrics Recording**: Benchmarks for Prometheus metric operations

## Running Benchmarks

### Run All Benchmarks

```bash
cd tests/Core/Observability/Hazina.Observability.Core.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmark Class

```bash
dotnet run -c Release --filter "*TelemetryBenchmarks*"
dotnet run -c Release --filter "*ActivityBenchmarks*"
dotnet run -c Release --filter "*MetricsBenchmarks*"
```

### Run Specific Benchmark Method

```bash
dotnet run -c Release --filter "*TrackOperation*"
dotnet run -c Release --filter "*StartLLMOperation*"
```

## Benchmark Results

Results are saved to `BenchmarkDotNet.Artifacts/results/` in multiple formats:
- HTML report
- Markdown report
- CSV data

## Configuration

Benchmarks are configured with:
- **Warmup**: 3 iterations
- **Iterations**: 5 runs per benchmark
- **Memory Diagnostics**: Enabled to track allocations

## Interpreting Results

Key metrics to focus on:
- **Mean**: Average execution time
- **Error**: Standard error of the mean
- **StdDev**: Standard deviation
- **Gen0/Gen1/Gen2**: GC collection counts
- **Allocated**: Memory allocated per operation

### Target Performance Goals

- Telemetry operations: < 1 μs per operation
- Activity creation: < 5 μs per operation
- Metric recording: < 500 ns per operation
- Memory allocations: Minimize Gen1/Gen2 collections

## Continuous Monitoring

Run benchmarks regularly to:
1. Detect performance regressions
2. Validate optimizations
3. Guide caching strategies
4. Inform capacity planning

## Adding New Benchmarks

1. Create a new benchmark method in the appropriate class
2. Mark with `[Benchmark]` attribute
3. Follow existing patterns for setup/cleanup
4. Run benchmarks to establish baseline
