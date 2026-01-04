namespace Hazina.Enterprise.Core.HealthChecks;

/// <summary>
/// Health status of a component
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// Component is healthy
    /// </summary>
    Healthy,

    /// <summary>
    /// Component is degraded but functional
    /// </summary>
    Degraded,

    /// <summary>
    /// Component is unhealthy
    /// </summary>
    Unhealthy
}

/// <summary>
/// Result of a health check
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Health status
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Description of the health status
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Exception if health check failed
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Additional data about the health check
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Duration of the health check
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Create a healthy result
    /// </summary>
    public static HealthCheckResult Healthy(string? description = null, Dictionary<string, object>? data = null)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Healthy,
            Description = description,
            Data = data ?? new()
        };
    }

    /// <summary>
    /// Create a degraded result
    /// </summary>
    public static HealthCheckResult Degraded(string? description = null, Dictionary<string, object>? data = null)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Degraded,
            Description = description,
            Data = data ?? new()
        };
    }

    /// <summary>
    /// Create an unhealthy result
    /// </summary>
    public static HealthCheckResult Unhealthy(string? description = null, Exception? exception = null, Dictionary<string, object>? data = null)
    {
        return new HealthCheckResult
        {
            Status = HealthStatus.Unhealthy,
            Description = description,
            Exception = exception,
            Data = data ?? new()
        };
    }
}

/// <summary>
/// Interface for health checks
/// </summary>
public interface IHealthCheck
{
    /// <summary>
    /// Name of the health check
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Perform the health check
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health check result</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}
