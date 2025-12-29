using Hazina.AI.Providers.Core;
using System.Diagnostics;

namespace Hazina.AI.Providers.Health;

/// <summary>
/// Monitors health of LLM providers
/// </summary>
public class ProviderHealthMonitor : IProviderHealthMonitor
{
    private readonly ProviderRegistry _registry;
    private readonly Dictionary<string, ProviderHealthStatus> _healthStatuses = new();
    private readonly object _lock = new();
    private CancellationTokenSource? _monitoringCts;
    private Task? _monitoringTask;

    private readonly int _unhealthyThreshold = 3; // Consecutive failures before marking unhealthy
    private readonly TimeSpan _degradedResponseTime = TimeSpan.FromSeconds(5); // Slow response threshold

    public ProviderHealthMonitor(ProviderRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Perform health check using a simple test request
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(string providerName, CancellationToken cancellationToken = default)
    {
        var provider = _registry.GetProvider(providerName);
        if (provider == null)
        {
            return new HealthCheckResult
            {
                IsHealthy = false,
                ErrorMessage = "Provider not found"
            };
        }

        var sw = Stopwatch.StartNew();
        try
        {
            // Simple health check: Try to get a response with a minimal prompt
            var messages = new List<HazinaChatMessage>
            {
                new() { Role = HazinaMessageRole.User, Text = "Hi" }
            };

            await provider.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, cancellationToken);

            sw.Stop();
            return new HealthCheckResult
            {
                IsHealthy = true,
                ResponseTime = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new HealthCheckResult
            {
                IsHealthy = false,
                ResponseTime = sw.Elapsed,
                ErrorMessage = ex.Message,
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Get current health status for a provider
    /// </summary>
    public ProviderHealthStatus GetHealthStatus(string providerName)
    {
        lock (_lock)
        {
            if (!_healthStatuses.TryGetValue(providerName, out var status))
            {
                status = new ProviderHealthStatus
                {
                    ProviderName = providerName,
                    State = HealthState.Unknown
                };
                _healthStatuses[providerName] = status;
            }
            return status;
        }
    }

    /// <summary>
    /// Get all health statuses
    /// </summary>
    public IEnumerable<ProviderHealthStatus> GetAllHealthStatuses()
    {
        lock (_lock)
        {
            return _healthStatuses.Values.ToList();
        }
    }

    /// <summary>
    /// Record a successful request
    /// </summary>
    public void RecordSuccess(string providerName, TimeSpan responseTime)
    {
        lock (_lock)
        {
            var status = GetOrCreateStatus(providerName);
            status.LastSuccess = DateTime.UtcNow;
            status.LastChecked = DateTime.UtcNow;
            status.ResponseTime = responseTime;
            status.TotalRequests++;
            status.SuccessfulRequests++;
            status.ConsecutiveFailures = 0;

            // Update state based on response time
            if (responseTime > _degradedResponseTime)
            {
                status.State = HealthState.Degraded;
            }
            else
            {
                status.State = HealthState.Healthy;
            }
        }
    }

    /// <summary>
    /// Record a failed request
    /// </summary>
    public void RecordFailure(string providerName, string errorMessage, Exception? exception = null)
    {
        lock (_lock)
        {
            var status = GetOrCreateStatus(providerName);
            status.LastFailure = DateTime.UtcNow;
            status.LastChecked = DateTime.UtcNow;
            status.LastError = errorMessage;
            status.TotalRequests++;
            status.FailedRequests++;
            status.ConsecutiveFailures++;

            // Update state based on consecutive failures
            if (status.ConsecutiveFailures >= _unhealthyThreshold)
            {
                status.State = HealthState.Unhealthy;
            }
            else
            {
                status.State = HealthState.Degraded;
            }
        }
    }

    /// <summary>
    /// Start continuous health monitoring
    /// </summary>
    public Task StartMonitoringAsync(TimeSpan interval, CancellationToken cancellationToken = default)
    {
        StopMonitoring();

        _monitoringCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _monitoringTask = Task.Run(async () =>
        {
            while (!_monitoringCts.Token.IsCancellationRequested)
            {
                await PerformHealthChecksAsync(_monitoringCts.Token);
                await Task.Delay(interval, _monitoringCts.Token);
            }
        }, _monitoringCts.Token);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop continuous health monitoring
    /// </summary>
    public void StopMonitoring()
    {
        _monitoringCts?.Cancel();
        _monitoringCts?.Dispose();
        _monitoringCts = null;
        _monitoringTask = null;
    }

    private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
    {
        var providers = _registry.GetEnabledProviders();
        foreach (var (name, _, _) in providers)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var result = await CheckHealthAsync(name, cancellationToken);
                if (result.IsHealthy)
                {
                    RecordSuccess(name, result.ResponseTime);
                }
                else
                {
                    RecordFailure(name, result.ErrorMessage ?? "Unknown error", result.Exception);
                }
            }
            catch (Exception ex)
            {
                RecordFailure(name, ex.Message, ex);
            }
        }
    }

    private ProviderHealthStatus GetOrCreateStatus(string providerName)
    {
        if (!_healthStatuses.TryGetValue(providerName, out var status))
        {
            status = new ProviderHealthStatus
            {
                ProviderName = providerName,
                State = HealthState.Unknown
            };
            _healthStatuses[providerName] = status;
        }
        return status;
    }
}
