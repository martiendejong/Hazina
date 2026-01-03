using Prometheus;

namespace Hazina.Observability.Core.Metrics;

/// <summary>
/// Prometheus metrics for Hazina AI operations
/// </summary>
public static class HazinaMetrics
{
    /// <summary>
    /// Operation duration histogram
    /// </summary>
    public static readonly Histogram OperationDuration = Prometheus.Metrics.CreateHistogram(
        "hazina_operation_duration_ms",
        "Duration of AI operations in milliseconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "provider", "operation_type", "success" },
            Buckets = Histogram.ExponentialBuckets(10, 2, 10) // 10ms to 5120ms
        }
    );

    /// <summary>
    /// Total operations counter
    /// </summary>
    public static readonly Counter OperationsTotal = Prometheus.Metrics.CreateCounter(
        "hazina_operations_total",
        "Total number of AI operations",
        new CounterConfiguration { LabelNames = new[] { "provider", "success" } }
    );

    /// <summary>
    /// Provider health gauge (0-1)
    /// </summary>
    public static readonly Gauge ProviderHealth = Prometheus.Metrics.CreateGauge(
        "hazina_provider_health",
        "Provider health score (0-1)",
        new GaugeConfiguration { LabelNames = new[] { "provider" } }
    );

    /// <summary>
    /// Hallucinations detected counter
    /// </summary>
    public static readonly Counter HallucinationsDetected = Prometheus.Metrics.CreateCounter(
        "hazina_hallucinations_detected_total",
        "Total hallucinations detected",
        new CounterConfiguration { LabelNames = new[] { "type" } }
    );

    /// <summary>
    /// Provider failover events
    /// </summary>
    public static readonly Counter ProviderFailovers = Prometheus.Metrics.CreateCounter(
        "hazina_provider_failovers_total",
        "Total provider failover events",
        new CounterConfiguration { LabelNames = new[] { "from_provider", "to_provider", "reason" } }
    );

    /// <summary>
    /// Total cost in USD
    /// </summary>
    public static readonly Counter TotalCost = Prometheus.Metrics.CreateCounter(
        "hazina_cost_usd_total",
        "Total cost in USD",
        new CounterConfiguration { LabelNames = new[] { "provider" } }
    );

    /// <summary>
    /// Tokens used (input/output)
    /// </summary>
    public static readonly Counter TokensUsed = Prometheus.Metrics.CreateCounter(
        "hazina_tokens_used_total",
        "Total tokens used",
        new CounterConfiguration { LabelNames = new[] { "provider", "type" } }
    );

    /// <summary>
    /// NeuroChain layers used
    /// </summary>
    public static readonly Counter NeuroChainLayersUsed = Prometheus.Metrics.CreateCounter(
        "hazina_neurochain_layers_used_total",
        "Number of NeuroChain layers used",
        new CounterConfiguration { LabelNames = new[] { "layers", "complexity" } }
    );

    /// <summary>
    /// Faults detected and corrected
    /// </summary>
    public static readonly Counter FaultsDetected = Prometheus.Metrics.CreateCounter(
        "hazina_faults_detected_total",
        "Total faults detected",
        new CounterConfiguration { LabelNames = new[] { "type", "corrected" } }
    );
}
