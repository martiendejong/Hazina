using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Hazina.Observability.Core.Benchmarks;

// Run benchmarks with default configuration
var config = DefaultConfig.Instance;

BenchmarkRunner.Run<TelemetryBenchmarks>(config);
BenchmarkRunner.Run<ActivityBenchmarks>(config);
BenchmarkRunner.Run<MetricsBenchmarks>(config);
