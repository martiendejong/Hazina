using System.Diagnostics;

namespace Hazina.App.HazinaCoder.Core.Performance;

/// <summary>
/// Performance benchmarking suite
/// Iteration 32: Performance measurement
/// </summary>
public class PerformanceBenchmark
{
    public class BenchmarkResult
    {
        public string Name { get; set; } = "";
        public TimeSpan Duration { get; set; }
        public double OperationsPerSecond { get; set; }
        public long MemoryUsedBytes { get; set; }
    }

    public async Task<BenchmarkResult> RunAsync(string name, Func<Task> operation, int iterations = 100)
    {
        // Warmup
        await operation();

        var initialMemory = GC.GetTotalMemory(true);
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < iterations; i++)
            await operation();

        sw.Stop();
        var finalMemory = GC.GetTotalMemory(false);

        return new BenchmarkResult
        {
            Name = name,
            Duration = sw.Elapsed,
            OperationsPerSecond = iterations / sw.Elapsed.TotalSeconds,
            MemoryUsedBytes = finalMemory - initialMemory
        };
    }

    public void PrintResults(List<BenchmarkResult> results)
    {
        Console.WriteLine("=== Performance Benchmark Results ===");
        foreach (var result in results)
        {
            Console.WriteLine($"\n{result.Name}:");
            Console.WriteLine($"  Duration: {result.Duration.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Ops/sec: {result.OperationsPerSecond:F2}");
            Console.WriteLine($"  Memory: {result.MemoryUsedBytes / 1024:F2} KB");
        }
    }
}

/// <summary>
/// Load testing framework
/// Iteration 33: Load testing
/// </summary>
public class LoadTester
{
    public class LoadTestResult
    {
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public TimeSpan Duration { get; set; }
        public double RequestsPerSecond { get; set; }
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan P95Latency { get; set; }
        public TimeSpan P99Latency { get; set; }
    }

    public async Task<LoadTestResult> RunLoadTestAsync(
        Func<Task> operation,
        int concurrentUsers = 10,
        int requestsPerUser = 100,
        CancellationToken cancellationToken = default)
    {
        var latencies = new List<TimeSpan>();
        var successCount = 0;
        var failCount = 0;
        var totalSw = Stopwatch.StartNew();

        var tasks = Enumerable.Range(0, concurrentUsers).Select(async _ =>
        {
            for (int i = 0; i < requestsPerUser; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var sw = Stopwatch.StartNew();
                try
                {
                    await operation();
                    sw.Stop();
                    lock (latencies)
                    {
                        latencies.Add(sw.Elapsed);
                        successCount++;
                    }
                }
                catch
                {
                    lock (latencies)
                    {
                        failCount++;
                    }
                }
            }
        });

        await Task.WhenAll(tasks);
        totalSw.Stop();

        var sortedLatencies = latencies.OrderBy(l => l).ToList();

        return new LoadTestResult
        {
            TotalRequests = concurrentUsers * requestsPerUser,
            SuccessfulRequests = successCount,
            FailedRequests = failCount,
            Duration = totalSw.Elapsed,
            RequestsPerSecond = successCount / totalSw.Elapsed.TotalSeconds,
            AverageLatency = TimeSpan.FromTicks((long)latencies.Average(l => l.Ticks)),
            P95Latency = sortedLatencies[(int)(sortedLatencies.Count * 0.95)],
            P99Latency = sortedLatencies[(int)(sortedLatencies.Count * 0.99)]
        };
    }
}
