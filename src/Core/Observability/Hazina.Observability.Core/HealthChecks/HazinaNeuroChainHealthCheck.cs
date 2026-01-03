using Hazina.Neurochain.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using MsHealthCheckResult = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace Hazina.Observability.Core.HealthChecks;

/// <summary>
/// Health check for Hazina NeuroChain orchestrator
/// Performs a lightweight reasoning check to verify layers are functioning
/// </summary>
public class HazinaNeuroChainHealthCheck : IHealthCheck
{
    private readonly NeuroChainOrchestrator _orchestrator;
    private readonly ILogger<HazinaNeuroChainHealthCheck> _logger;
    private readonly TimeSpan _timeout;

    public HazinaNeuroChainHealthCheck(
        NeuroChainOrchestrator orchestrator,
        ILogger<HazinaNeuroChainHealthCheck> logger,
        TimeSpan? timeout = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
    }

    public async Task<MsHealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(_timeout);

            // Perform lightweight health check prompt
            var result = await _orchestrator.ReasonAsync(
                "Respond with 'OK' to confirm you are functioning.",
                new ReasoningContext { MaxSteps = 1 },
                cts.Token
            );

            var data = new Dictionary<string, object>
            {
                ["layers_executed"] = result.LayerResults.Count,
                ["total_duration_ms"] = result.TotalDurationMs,
                ["total_cost"] = result.TotalCost,
                ["final_confidence"] = result.FinalConfidence,
                ["early_stopped"] = result.EarlyStopped
            };

            // Add layer-specific details
            for (int i = 0; i < result.LayerResults.Count; i++)
            {
                var layer = result.LayerResults[i];
                var prefix = $"layer_{i + 1}";
                data[$"{prefix}_provider"] = layer.Provider;
                data[$"{prefix}_confidence"] = layer.Confidence;
                data[$"{prefix}_duration_ms"] = layer.DurationMs;
                data[$"{prefix}_valid"] = layer.IsValid;
            }

            if (!result.IsSuccessful)
            {
                return MsHealthCheckResult.Unhealthy(
                    $"NeuroChain failed: {result.Error}",
                    data: data
                );
            }

            if (result.FinalConfidence < 0.5)
            {
                return MsHealthCheckResult.Degraded(
                    $"NeuroChain returned low confidence: {result.FinalConfidence:P0}",
                    data: data
                );
            }

            return MsHealthCheckResult.Healthy(
                $"NeuroChain executed {result.LayerResults.Count} layers successfully",
                data: data
            );
        }
        catch (OperationCanceledException)
        {
            return MsHealthCheckResult.Degraded(
                $"NeuroChain health check timed out after {_timeout.TotalSeconds}s",
                data: new Dictionary<string, object> { ["timeout_ms"] = _timeout.TotalMilliseconds }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NeuroChain health check failed");
            return MsHealthCheckResult.Unhealthy(
                "NeuroChain health check failed",
                exception: ex,
                data: new Dictionary<string, object> { ["error"] = ex.Message }
            );
        }
    }
}
