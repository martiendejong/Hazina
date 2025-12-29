using System.Collections.Concurrent;
using System.Diagnostics;

namespace Hazina.Production.Monitoring.Metrics;

/// <summary>
/// Collects and aggregates metrics for monitoring
/// </summary>
public class MetricsCollector
{
    private readonly ConcurrentDictionary<string, MetricValue> _counters = new();
    private readonly ConcurrentDictionary<string, MetricValue> _gauges = new();
    private readonly ConcurrentDictionary<string, List<double>> _histograms = new();
    private readonly ConcurrentDictionary<string, Stopwatch> _timers = new();
    private readonly object _lock = new();

    /// <summary>
    /// Increment a counter
    /// </summary>
    public void IncrementCounter(string name, double value = 1, Dictionary<string, string>? tags = null)
    {
        var key = BuildKey(name, tags);
        _counters.AddOrUpdate(key,
            new MetricValue { Name = name, Value = value, Tags = tags, LastUpdated = DateTime.UtcNow },
            (k, existing) =>
            {
                existing.Value += value;
                existing.LastUpdated = DateTime.UtcNow;
                return existing;
            });
    }

    /// <summary>
    /// Set a gauge value
    /// </summary>
    public void SetGauge(string name, double value, Dictionary<string, string>? tags = null)
    {
        var key = BuildKey(name, tags);
        _gauges[key] = new MetricValue
        {
            Name = name,
            Value = value,
            Tags = tags,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Record a histogram value
    /// </summary>
    public void RecordHistogram(string name, double value, Dictionary<string, string>? tags = null)
    {
        var key = BuildKey(name, tags);
        lock (_lock)
        {
            if (!_histograms.ContainsKey(key))
            {
                _histograms[key] = new List<double>();
            }
            _histograms[key].Add(value);
        }
    }

    /// <summary>
    /// Start a timer
    /// </summary>
    public void StartTimer(string name)
    {
        _timers[name] = Stopwatch.StartNew();
    }

    /// <summary>
    /// Stop a timer and record duration
    /// </summary>
    public TimeSpan StopTimer(string name, Dictionary<string, string>? tags = null)
    {
        if (_timers.TryRemove(name, out var stopwatch))
        {
            stopwatch.Stop();
            RecordHistogram($"{name}.duration_ms", stopwatch.Elapsed.TotalMilliseconds, tags);
            return stopwatch.Elapsed;
        }
        return TimeSpan.Zero;
    }

    /// <summary>
    /// Record an operation with automatic timing
    /// </summary>
    public async Task<T> TimeOperationAsync<T>(
        string name,
        Func<Task<T>> operation,
        Dictionary<string, string>? tags = null)
    {
        var stopwatch = Stopwatch.StartNew();
        var success = false;

        try
        {
            var result = await operation();
            success = true;
            return result;
        }
        finally
        {
            stopwatch.Stop();
            RecordHistogram($"{name}.duration_ms", stopwatch.Elapsed.TotalMilliseconds, tags);
            IncrementCounter($"{name}.total", 1, tags);

            if (success)
            {
                IncrementCounter($"{name}.success", 1, tags);
            }
            else
            {
                IncrementCounter($"{name}.failure", 1, tags);
            }
        }
    }

    /// <summary>
    /// Get all metrics
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        var snapshot = new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow
        };

        // Counters
        foreach (var kvp in _counters)
        {
            snapshot.Counters.Add(kvp.Value);
        }

        // Gauges
        foreach (var kvp in _gauges)
        {
            snapshot.Gauges.Add(kvp.Value);
        }

        // Histograms with statistics
        lock (_lock)
        {
            foreach (var kvp in _histograms)
            {
                if (kvp.Value.Count > 0)
                {
                    var values = kvp.Value.OrderBy(v => v).ToList();
                    snapshot.Histograms.Add(new HistogramMetric
                    {
                        Name = kvp.Key,
                        Count = values.Count,
                        Min = values.First(),
                        Max = values.Last(),
                        Mean = values.Average(),
                        Median = GetPercentile(values, 0.5),
                        P95 = GetPercentile(values, 0.95),
                        P99 = GetPercentile(values, 0.99)
                    });
                }
            }
        }

        return snapshot;
    }

    /// <summary>
    /// Reset all metrics
    /// </summary>
    public void Reset()
    {
        _counters.Clear();
        _gauges.Clear();
        lock (_lock)
        {
            _histograms.Clear();
        }
        _timers.Clear();
    }

    /// <summary>
    /// Get percentile from sorted values
    /// </summary>
    private double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0)
            return 0;

        var index = (int)Math.Ceiling(sortedValues.Count * percentile) - 1;
        index = Math.Max(0, Math.Min(index, sortedValues.Count - 1));
        return sortedValues[index];
    }

    /// <summary>
    /// Build metric key with tags
    /// </summary>
    private string BuildKey(string name, Dictionary<string, string>? tags)
    {
        if (tags == null || tags.Count == 0)
            return name;

        var tagStr = string.Join(",", tags.OrderBy(kvp => kvp.Key).Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"{name}[{tagStr}]";
    }
}

/// <summary>
/// Metric value
/// </summary>
public class MetricValue
{
    public string Name { get; set; } = string.Empty;
    public double Value { get; set; }
    public Dictionary<string, string>? Tags { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Histogram metric with statistics
/// </summary>
public class HistogramMetric
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
    public double Min { get; set; }
    public double Max { get; set; }
    public double Mean { get; set; }
    public double Median { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
}

/// <summary>
/// Metrics snapshot
/// </summary>
public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public List<MetricValue> Counters { get; set; } = new();
    public List<MetricValue> Gauges { get; set; } = new();
    public List<HistogramMetric> Histograms { get; set; } = new();

    /// <summary>
    /// Export to Prometheus format
    /// </summary>
    public string ToPrometheusFormat()
    {
        var output = new System.Text.StringBuilder();

        foreach (var counter in Counters)
        {
            output.AppendLine($"# TYPE {counter.Name} counter");
            output.AppendLine($"{counter.Name}{FormatTags(counter.Tags)} {counter.Value}");
        }

        foreach (var gauge in Gauges)
        {
            output.AppendLine($"# TYPE {gauge.Name} gauge");
            output.AppendLine($"{gauge.Name}{FormatTags(gauge.Tags)} {gauge.Value}");
        }

        foreach (var histogram in Histograms)
        {
            output.AppendLine($"# TYPE {histogram.Name} histogram");
            output.AppendLine($"{histogram.Name}_count {histogram.Count}");
            output.AppendLine($"{histogram.Name}_min {histogram.Min}");
            output.AppendLine($"{histogram.Name}_max {histogram.Max}");
            output.AppendLine($"{histogram.Name}_mean {histogram.Mean}");
            output.AppendLine($"{histogram.Name}_median {histogram.Median}");
            output.AppendLine($"{histogram.Name}_p95 {histogram.P95}");
            output.AppendLine($"{histogram.Name}_p99 {histogram.P99}");
        }

        return output.ToString();
    }

    private string FormatTags(Dictionary<string, string>? tags)
    {
        if (tags == null || tags.Count == 0)
            return "";

        var tagStr = string.Join(",", tags.Select(kvp => $"{kvp.Key}=\"{kvp.Value}\""));
        return $"{{{tagStr}}}";
    }
}
