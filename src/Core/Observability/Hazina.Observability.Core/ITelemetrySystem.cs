namespace Hazina.Observability.Core;

/// <summary>
/// Telemetry system for tracking AI operations, failures, and performance metrics
/// </summary>
public interface ITelemetrySystem
{
    /// <summary>
    /// Track an AI operation
    /// </summary>
    void TrackOperation(string operationId, string provider, TimeSpan duration, bool success, string? operationType = null);

    /// <summary>
    /// Track hallucination detection
    /// </summary>
    void TrackHallucination(string operationId, string hallucinationType, double confidence);

    /// <summary>
    /// Track provider failover event
    /// </summary>
    void TrackProviderFailover(string fromProvider, string toProvider, string reason);

    /// <summary>
    /// Track cost of an operation
    /// </summary>
    void TrackCost(string provider, decimal cost, int inputTokens, int outputTokens);

    /// <summary>
    /// Track Neurochain layer usage
    /// </summary>
    void TrackNeuroChainLayers(string operationId, int layersUsed, string complexity);

    /// <summary>
    /// Track fault detection and correction
    /// </summary>
    void TrackFaultDetection(string operationId, string faultType, bool wasCorrected);
}
