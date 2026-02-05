using System.Diagnostics;

namespace Hazina.App.HazinaCoder.Core.Monitoring;

/// <summary>
/// Collects and exports metrics (Prometheus format)
/// Iteration 12: Metrics export
/// </summary>
public class MetricsCollector
{
    private readonly Dictionary<string, Counter> _counters = new();
    private readonly Dictionary<string, Gauge> _gauges = new();
    private readonly Dictionary<string, Histogram> _histograms = new();

    public class Counter
    {
        public string Name { get; set; } = "";
        public long Value { get; set; }
        public void Increment(long by = 1) => Value += by;
    }

    public class Gauge
    {
        public string Name { get; set; } = "";
        public double Value { get; set; }
        public void Set(double value) => Value = value;
    }

    public class Histogram
    {
        public string Name { get; set; } = "";
        public List<double> Values { get; set; } = new();
        public void Observe(double value) => Values.Add(value);

        public double Average => Values.Any() ? Values.Average() : 0;
        public double P50 => Percentile(50);
        public double P95 => Percentile(95);
        public double P99 => Percentile(99);

        private double Percentile(int p)
        {
            if (!Values.Any()) return 0;
            var sorted = Values.OrderBy(v => v).ToList();
            var index = (int)Math.Ceiling(p / 100.0 * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(index, sorted.Count - 1))];
        }
    }

    // Counter methods
    public void IncrementCounter(string name, long by = 1)
    {
        if (!_counters.ContainsKey(name))
            _counters[name] = new Counter { Name = name };

        _counters[name].Increment(by);
    }

    // Gauge methods
    public void SetGauge(string name, double value)
    {
        if (!_gauges.ContainsKey(name))
            _gauges[name] = new Gauge { Name = name };

        _gauges[name].Set(value);
    }

    // Histogram methods
    public void ObserveHistogram(string name, double value)
    {
        if (!_histograms.ContainsKey(name))
            _histograms[name] = new Histogram { Name = name };

        _histograms[name].Observe(value);
    }

    // Export to Prometheus format
    public string ExportPrometheus()
    {
        var sb = new System.Text.StringBuilder();

        foreach (var counter in _counters.Values)
        {
            sb.AppendLine($"# TYPE {counter.Name} counter");
            sb.AppendLine($"{counter.Name} {counter.Value}");
        }

        foreach (var gauge in _gauges.Values)
        {
            sb.AppendLine($"# TYPE {gauge.Name} gauge");
            sb.AppendLine($"{gauge.Name} {gauge.Value}");
        }

        foreach (var histogram in _histograms.Values)
        {
            sb.AppendLine($"# TYPE {histogram.Name} histogram");
            sb.AppendLine($"{histogram.Name}_count {histogram.Values.Count}");
            sb.AppendLine($"{histogram.Name}_sum {histogram.Values.Sum()}");
            sb.AppendLine($"{histogram.Name}_avg {histogram.Average}");
            sb.AppendLine($"{histogram.Name}_p50 {histogram.P50}");
            sb.AppendLine($"{histogram.Name}_p95 {histogram.P95}");
            sb.AppendLine($"{histogram.Name}_p99 {histogram.P99}");
        }

        return sb.ToString();
    }

    // Common metrics
    public void RecordProviderCall(string provider, long durationMs, bool success)
    {
        IncrementCounter($"provider_calls_total{{provider=\"{provider}\"}}");
        if (!success)
            IncrementCounter($"provider_errors_total{{provider=\"{provider}\"}}");

        ObserveHistogram($"provider_duration_ms{{provider=\"{provider}\"}}", durationMs);
    }

    public void RecordTokens(string provider, long tokens, decimal cost)
    {
        IncrementCounter($"tokens_total{{provider=\"{provider}\"}}", tokens);
        SetGauge($"cost_total{{provider=\"{provider}\"}}", (double)cost);
    }
}

/// <summary>
/// Automatic timing wrapper
/// </summary>
public class MetricTimer : IDisposable
{
    private readonly MetricsCollector _collector;
    private readonly string _name;
    private readonly Stopwatch _stopwatch;

    public MetricTimer(MetricsCollector collector, string name)
    {
        _collector = collector;
        _name = name;
        _stopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        _stopwatch.Stop();
        _collector.ObserveHistogram(_name, _stopwatch.ElapsedMilliseconds);
    }
}
