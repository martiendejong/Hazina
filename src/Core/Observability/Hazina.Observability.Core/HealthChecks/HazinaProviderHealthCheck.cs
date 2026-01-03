using Hazina.AI.Providers.Core;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MsHealthCheckResult = Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult;

namespace Hazina.Observability.Core.HealthChecks;

/// <summary>
/// Health check for Hazina provider orchestrator
/// Monitors all registered providers and reports overall health
/// </summary>
public class HazinaProviderHealthCheck : IHealthCheck
{
    private readonly IProviderOrchestrator _orchestrator;

    public HazinaProviderHealthCheck(IProviderOrchestrator orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public Task<MsHealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var statuses = _orchestrator.GetAllHealthStatuses().ToList();

            if (statuses.Count == 0)
            {
                return Task.FromResult(MsHealthCheckResult.Degraded(
                    "No providers registered",
                    data: new Dictionary<string, object> { ["provider_count"] = 0 }
                ));
            }

            var healthyCount = statuses.Count(s => s.IsHealthy);
            var degradedCount = statuses.Count(s => s.IsDegraded);
            var unhealthyCount = statuses.Count(s => s.IsUnhealthy);
            var totalCount = statuses.Count;

            var data = new Dictionary<string, object>
            {
                ["total_providers"] = totalCount,
                ["healthy_providers"] = healthyCount,
                ["degraded_providers"] = degradedCount,
                ["unhealthy_providers"] = unhealthyCount,
                ["overall_success_rate"] = statuses.Average(s => s.SuccessRate),
                ["total_cost"] = _orchestrator.GetTotalCost()
            };

            // Add per-provider details
            foreach (var status in statuses)
            {
                var prefix = $"provider_{status.ProviderName}";
                data[$"{prefix}_state"] = status.State.ToString();
                data[$"{prefix}_success_rate"] = status.SuccessRate;
                data[$"{prefix}_consecutive_failures"] = status.ConsecutiveFailures;

                if (status.ResponseTime.HasValue)
                {
                    data[$"{prefix}_response_time_ms"] = status.ResponseTime.Value.TotalMilliseconds;
                }

                if (status.LastError != null)
                {
                    data[$"{prefix}_last_error"] = status.LastError;
                }
            }

            // Determine overall health status
            if (healthyCount == 0)
            {
                // No healthy providers
                return Task.FromResult(MsHealthCheckResult.Unhealthy(
                    $"All {totalCount} providers are unhealthy or degraded",
                    data: data
                ));
            }
            else if (unhealthyCount > 0 || degradedCount > 0)
            {
                // Some providers have issues
                return Task.FromResult(MsHealthCheckResult.Degraded(
                    $"{healthyCount}/{totalCount} providers healthy ({unhealthyCount} unhealthy, {degradedCount} degraded)",
                    data: data
                ));
            }
            else
            {
                // All providers healthy
                return Task.FromResult(MsHealthCheckResult.Healthy(
                    $"All {totalCount} providers healthy",
                    data: data
                ));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(MsHealthCheckResult.Unhealthy(
                "Failed to check provider health",
                exception: ex,
                data: new Dictionary<string, object> { ["error"] = ex.Message }
            ));
        }
    }
}
