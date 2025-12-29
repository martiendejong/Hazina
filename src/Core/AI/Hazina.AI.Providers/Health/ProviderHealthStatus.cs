namespace Hazina.AI.Providers.Health;

/// <summary>
/// Health status of a provider
/// </summary>
public class ProviderHealthStatus
{
    public string ProviderName { get; set; } = string.Empty;
    public HealthState State { get; set; } = HealthState.Unknown;
    public DateTime LastChecked { get; set; } = DateTime.UtcNow;
    public DateTime? LastSuccess { get; set; }
    public DateTime? LastFailure { get; set; }
    public string? LastError { get; set; }
    public TimeSpan? ResponseTime { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 0.0;
    public Dictionary<string, object> AdditionalData { get; set; } = new();

    public bool IsHealthy => State == HealthState.Healthy;
    public bool IsDegraded => State == HealthState.Degraded;
    public bool IsUnhealthy => State == HealthState.Unhealthy;
}

/// <summary>
/// Health state enum
/// </summary>
public enum HealthState
{
    Unknown,
    Healthy,
    Degraded,
    Unhealthy
}

/// <summary>
/// Health check result
/// </summary>
public class HealthCheckResult
{
    public bool IsHealthy { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string? ErrorMessage { get; set; }
    public Exception? Exception { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}
