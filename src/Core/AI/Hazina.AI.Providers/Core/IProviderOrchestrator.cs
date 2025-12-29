using Hazina.AI.Providers.Selection;

namespace Hazina.AI.Providers.Core;

/// <summary>
/// Main interface for provider orchestration
/// Provides high-level API for multi-provider management with failover, health monitoring, and cost tracking
/// </summary>
public interface IProviderOrchestrator : ILLMClient
{
    /// <summary>
    /// Configure default selection strategy
    /// </summary>
    void SetDefaultStrategy(SelectionStrategy strategy);

    /// <summary>
    /// Set default selection context
    /// </summary>
    void SetDefaultContext(SelectionContext context);

    /// <summary>
    /// Get a provider by name
    /// </summary>
    ILLMClient? GetProvider(string name);

    /// <summary>
    /// Get provider metadata
    /// </summary>
    ProviderMetadata? GetProviderMetadata(string name);

    /// <summary>
    /// Enable/disable a provider
    /// </summary>
    void SetProviderEnabled(string name, bool enabled);

    /// <summary>
    /// Set provider priority (lower = higher priority)
    /// </summary>
    void SetProviderPriority(string name, int priority);

    /// <summary>
    /// Get total cost across all providers
    /// </summary>
    decimal GetTotalCost();

    /// <summary>
    /// Get cost breakdown by provider
    /// </summary>
    Dictionary<string, decimal> GetCostByProvider();

    /// <summary>
    /// Set budget for a provider
    /// </summary>
    void SetBudget(string providerName, decimal limit, Cost.BudgetPeriod period);

    /// <summary>
    /// Add budget alert
    /// </summary>
    void AddBudgetAlert(string providerName, double thresholdPercentage, string? message = null);

    /// <summary>
    /// Get health status for a provider
    /// </summary>
    Health.ProviderHealthStatus GetHealthStatus(string name);

    /// <summary>
    /// Get health status for all providers
    /// </summary>
    IEnumerable<Health.ProviderHealthStatus> GetAllHealthStatuses();

    /// <summary>
    /// Start health monitoring
    /// </summary>
    Task StartHealthMonitoringAsync(TimeSpan interval, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stop health monitoring
    /// </summary>
    void StopHealthMonitoring();

    /// <summary>
    /// Reset circuit breaker for a provider
    /// </summary>
    void ResetCircuitBreaker(string providerName);
}
