using Microsoft.Extensions.Logging;
using Hazina.Observability.Core.Metrics;

namespace Hazina.Observability.Core;

/// <summary>
/// Production telemetry system with structured logging and metrics
/// </summary>
public class TelemetrySystem : ITelemetrySystem
{
    private readonly ILogger<TelemetrySystem> _logger;

    public TelemetrySystem(ILogger<TelemetrySystem> logger)
    {
        _logger = logger;
    }

    public void TrackOperation(string operationId, string provider, TimeSpan duration, bool success, string? operationType = null)
    {
        _logger.LogInformation(
            "[TELEMETRY] Operation: {OperationId} | Provider: {Provider} | Type: {Type} | Duration: {Duration}ms | Success: {Success}",
            operationId, provider, operationType ?? "unknown", duration.TotalMilliseconds, success);

        // Update Prometheus metrics
        HazinaMetrics.OperationDuration
            .WithLabels(provider, operationType ?? "unknown", success.ToString().ToLower())
            .Observe(duration.TotalMilliseconds);

        HazinaMetrics.OperationsTotal
            .WithLabels(provider, success.ToString().ToLower())
            .Inc();
    }

    public void TrackHallucination(string operationId, string hallucinationType, double confidence)
    {
        _logger.LogWarning(
            "[TELEMETRY] Hallucination detected | Operation: {OperationId} | Type: {Type} | Confidence: {Confidence:P0}",
            operationId, hallucinationType, confidence);

        HazinaMetrics.HallucinationsDetected
            .WithLabels(hallucinationType)
            .Inc();
    }

    public void TrackProviderFailover(string fromProvider, string toProvider, string reason)
    {
        _logger.LogWarning(
            "[TELEMETRY] Provider failover | From: {From} → To: {To} | Reason: {Reason}",
            fromProvider, toProvider, reason);

        HazinaMetrics.ProviderFailovers
            .WithLabels(fromProvider, toProvider, reason)
            .Inc();
    }

    public void TrackCost(string provider, decimal cost, int inputTokens, int outputTokens)
    {
        _logger.LogInformation(
            "[TELEMETRY] Cost | Provider: {Provider} | Cost: ${Cost:F4} | Input: {InputTokens} | Output: {OutputTokens}",
            provider, cost, inputTokens, outputTokens);

        HazinaMetrics.TotalCost
            .WithLabels(provider)
            .Inc((double)cost);

        HazinaMetrics.TokensUsed
            .WithLabels(provider, "input")
            .Inc(inputTokens);

        HazinaMetrics.TokensUsed
            .WithLabels(provider, "output")
            .Inc(outputTokens);
    }

    public void TrackNeuroChainLayers(string operationId, int layersUsed, string complexity)
    {
        _logger.LogInformation(
            "[TELEMETRY] NeuroChain | Operation: {OperationId} | Layers: {Layers} | Complexity: {Complexity}",
            operationId, layersUsed, complexity);

        HazinaMetrics.NeuroChainLayersUsed
            .WithLabels(layersUsed.ToString(), complexity)
            .Inc();
    }

    public void TrackFaultDetection(string operationId, string faultType, bool wasCorrected)
    {
        _logger.LogInformation(
            "[TELEMETRY] Fault detection | Operation: {OperationId} | Type: {Type} | Corrected: {Corrected}",
            operationId, faultType, wasCorrected);

        HazinaMetrics.FaultsDetected
            .WithLabels(faultType, wasCorrected.ToString().ToLower())
            .Inc();
    }
}
