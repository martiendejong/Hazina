using Polly;
using Polly.CircuitBreaker;
using System.Collections.Concurrent;

namespace Hazina.App.HazinaCoder.Core.Infrastructure;

/// <summary>
/// Circuit breaker service for external dependencies
/// </summary>
public class CircuitBreakerService
{
    private readonly ConcurrentDictionary<string, ResiliencePipeline> _pipelines;
    private readonly ConcurrentDictionary<string, ServiceHealthStatus> _healthStatus;

    public CircuitBreakerService()
    {
        _pipelines = new ConcurrentDictionary<string, ResiliencePipeline>();
        _healthStatus = new ConcurrentDictionary<string, ServiceHealthStatus>();
    }

    /// <summary>
    /// Get or create circuit breaker for service
    /// </summary>
    public ResiliencePipeline GetOrCreateCircuitBreaker(
        string serviceName,
        CircuitBreakerConfig? config = null)
    {
        return _pipelines.GetOrAdd(serviceName, _ => CreatePipeline(serviceName, config ?? new CircuitBreakerConfig()));
    }

    /// <summary>
    /// Execute action with circuit breaker protection
    /// </summary>
    public async Task<T> ExecuteAsync<T>(
        string serviceName,
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        var pipeline = GetOrCreateCircuitBreaker(serviceName);

        try
        {
            var result = await pipeline.ExecuteAsync(async ct =>
            {
                var result = await action(ct);
                UpdateHealthStatus(serviceName, ServiceState.Healthy, null);
                return result;
            }, cancellationToken);

            return result;
        }
        catch (BrokenCircuitException ex)
        {
            UpdateHealthStatus(serviceName, ServiceState.Failed, "Circuit breaker open");
            throw new ServiceUnavailableException(serviceName, "Service circuit breaker is open", ex);
        }
        catch (Exception ex)
        {
            UpdateHealthStatus(serviceName, ServiceState.Degraded, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Get current health status of service
    /// </summary>
    public ServiceHealthStatus GetHealthStatus(string serviceName)
    {
        return _healthStatus.GetOrAdd(serviceName, _ => new ServiceHealthStatus
        {
            ServiceName = serviceName,
            State = ServiceState.Unknown,
            LastChecked = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get all service health statuses
    /// </summary>
    public Dictionary<string, ServiceHealthStatus> GetAllHealthStatuses()
    {
        return _healthStatus.ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    /// <summary>
    /// Reset circuit breaker for service
    /// </summary>
    public void ResetCircuitBreaker(string serviceName)
    {
        if (_pipelines.TryRemove(serviceName, out _))
        {
            UpdateHealthStatus(serviceName, ServiceState.Unknown, "Circuit breaker reset");
        }
    }

    /// <summary>
    /// Check if service is healthy
    /// </summary>
    public bool IsHealthy(string serviceName)
    {
        var status = GetHealthStatus(serviceName);
        return status.State == ServiceState.Healthy;
    }

    /// <summary>
    /// Check if service is available (healthy or degraded)
    /// </summary>
    public bool IsAvailable(string serviceName)
    {
        var status = GetHealthStatus(serviceName);
        return status.State is ServiceState.Healthy or ServiceState.Degraded;
    }

    /// <summary>
    /// Create resilience pipeline with circuit breaker
    /// </summary>
    private ResiliencePipeline CreatePipeline(string serviceName, CircuitBreakerConfig config)
    {
        return new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio = config.FailureThreshold,
                SamplingDuration = TimeSpan.FromSeconds(config.SamplingDurationSeconds),
                MinimumThroughput = config.MinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(config.BreakDurationSeconds),
                OnOpened = args =>
                {
                    UpdateHealthStatus(serviceName, ServiceState.Failed, "Circuit breaker opened");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    UpdateHealthStatus(serviceName, ServiceState.Healthy, "Circuit breaker closed");
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    UpdateHealthStatus(serviceName, ServiceState.Degraded, "Circuit breaker half-open");
                    return ValueTask.CompletedTask;
                }
            })
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = config.MaxRetries,
                Delay = TimeSpan.FromSeconds(config.RetryDelaySeconds),
                BackoffType = Polly.DelayBackoffType.Exponential
            })
            .AddTimeout(TimeSpan.FromSeconds(config.TimeoutSeconds))
            .Build();
    }

    /// <summary>
    /// Update health status for service
    /// </summary>
    private void UpdateHealthStatus(string serviceName, ServiceState state, string? message)
    {
        _healthStatus.AddOrUpdate(
            serviceName,
            _ => new ServiceHealthStatus
            {
                ServiceName = serviceName,
                State = state,
                Message = message,
                LastChecked = DateTime.UtcNow,
                LastStateChange = DateTime.UtcNow
            },
            (_, existing) =>
            {
                var lastStateChange = existing.State != state ? DateTime.UtcNow : existing.LastStateChange;
                return new ServiceHealthStatus
                {
                    ServiceName = serviceName,
                    State = state,
                    Message = message,
                    LastChecked = DateTime.UtcNow,
                    LastStateChange = lastStateChange
                };
            }
        );
    }
}

/// <summary>
/// Circuit breaker configuration
/// </summary>
public class CircuitBreakerConfig
{
    /// <summary>
    /// Failure threshold (0.0-1.0) to open circuit
    /// </summary>
    public double FailureThreshold { get; set; } = 0.5;

    /// <summary>
    /// Sampling duration in seconds
    /// </summary>
    public int SamplingDurationSeconds { get; set; } = 30;

    /// <summary>
    /// Minimum throughput before evaluating failure rate
    /// </summary>
    public int MinimumThroughput { get; set; } = 10;

    /// <summary>
    /// Break duration in seconds (how long circuit stays open)
    /// </summary>
    public int BreakDurationSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Retry delay in seconds
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 1;

    /// <summary>
    /// Timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Service health status
/// </summary>
public class ServiceHealthStatus
{
    public string ServiceName { get; set; } = string.Empty;
    public ServiceState State { get; set; }
    public string? Message { get; set; }
    public DateTime LastChecked { get; set; }
    public DateTime LastStateChange { get; set; }

    public TimeSpan TimeSinceLastStateChange => DateTime.UtcNow - LastStateChange;
}

/// <summary>
/// Service state enumeration
/// </summary>
public enum ServiceState
{
    Unknown,
    Healthy,
    Degraded,
    Failed,
    Offline
}

/// <summary>
/// Exception thrown when service is unavailable
/// </summary>
public class ServiceUnavailableException : Exception
{
    public string ServiceName { get; }

    public ServiceUnavailableException(string serviceName, string message, Exception? innerException = null)
        : base($"{serviceName}: {message}", innerException)
    {
        ServiceName = serviceName;
    }
}
