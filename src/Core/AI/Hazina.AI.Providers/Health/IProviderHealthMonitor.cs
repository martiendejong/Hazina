namespace Hazina.AI.Providers.Health;

/// <summary>
/// Interface for provider health monitoring
/// </summary>
public interface IProviderHealthMonitor
{
    /// <summary>
    /// Perform health check on a provider
    /// </summary>
    Task<HealthCheckResult> CheckHealthAsync(string providerName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get current health status
    /// </summary>
    ProviderHealthStatus GetHealthStatus(string providerName);

    /// <summary>
    /// Get health status for all providers
    /// </summary>
    IEnumerable<ProviderHealthStatus> GetAllHealthStatuses();

    /// <summary>
    /// Record a successful request
    /// </summary>
    void RecordSuccess(string providerName, TimeSpan responseTime);

    /// <summary>
    /// Record a failed request
    /// </summary>
    void RecordFailure(string providerName, string errorMessage, Exception? exception = null);

    /// <summary>
    /// Start continuous health monitoring
    /// </summary>
    Task StartMonitoringAsync(TimeSpan interval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop continuous health monitoring
    /// </summary>
    void StopMonitoring();
}
