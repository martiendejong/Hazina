using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;

namespace Hazina.AI.Providers.Resilience;

/// <summary>
/// Handles automatic failover between providers
/// </summary>
public class FailoverHandler
{
    private readonly IProviderSelector _selector;
    private readonly Dictionary<string, CircuitBreaker> _circuitBreakers = new();
    private readonly object _lock = new();

    public FailoverHandler(IProviderSelector selector)
    {
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
    }

    /// <summary>
    /// Execute action with automatic failover
    /// </summary>
    public async Task<LLMResponse<T>> ExecuteWithFailoverAsync<T>(
        Func<ILLMClient, Task<LLMResponse<T>>> action,
        SelectionStrategy strategy = SelectionStrategy.Priority,
        SelectionContext? context = null,
        int maxFailovers = 3,
        CancellationToken cancellationToken = default)
    {
        // Get fallback chain
        var providers = _selector.SelectProviders(strategy, maxFailovers, context).ToList();

        if (!providers.Any())
        {
            throw new FailoverException("No providers available");
        }

        List<Exception> exceptions = new();

        foreach (var provider in providers)
        {
            if (provider.ProviderName == null || provider.Provider == null)
                continue;

            var circuitBreaker = GetOrCreateCircuitBreaker(provider.ProviderName);

            try
            {
                return await circuitBreaker.ExecuteAsync(
                    () => action(provider.Provider),
                    cancellationToken
                );
            }
            catch (CircuitBreakerOpenException)
            {
                // Circuit breaker is open, try next provider
                exceptions.Add(new Exception($"Circuit breaker open for {provider.ProviderName}"));
                continue;
            }
            catch (Exception ex)
            {
                // Provider failed, try next one
                exceptions.Add(ex);
                continue;
            }
        }

        // All providers failed
        throw new FailoverException(
            "All providers failed",
            new AggregateException(exceptions)
        );
    }

    /// <summary>
    /// Reset circuit breaker for a provider
    /// </summary>
    public void ResetCircuitBreaker(string providerName)
    {
        lock (_lock)
        {
            if (_circuitBreakers.TryGetValue(providerName, out var breaker))
            {
                breaker.Reset();
            }
        }
    }

    /// <summary>
    /// Reset all circuit breakers
    /// </summary>
    public void ResetAllCircuitBreakers()
    {
        lock (_lock)
        {
            foreach (var breaker in _circuitBreakers.Values)
            {
                breaker.Reset();
            }
        }
    }

    /// <summary>
    /// Get circuit breaker metrics for a provider
    /// </summary>
    public CircuitBreakerMetrics? GetCircuitBreakerMetrics(string providerName)
    {
        lock (_lock)
        {
            return _circuitBreakers.TryGetValue(providerName, out var breaker)
                ? breaker.GetMetrics()
                : null;
        }
    }

    /// <summary>
    /// Get all circuit breaker metrics
    /// </summary>
    public Dictionary<string, CircuitBreakerMetrics> GetAllCircuitBreakerMetrics()
    {
        lock (_lock)
        {
            return _circuitBreakers.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.GetMetrics()
            );
        }
    }

    private CircuitBreaker GetOrCreateCircuitBreaker(string providerName)
    {
        lock (_lock)
        {
            if (!_circuitBreakers.TryGetValue(providerName, out var breaker))
            {
                breaker = new CircuitBreaker();
                _circuitBreakers[providerName] = breaker;
            }
            return breaker;
        }
    }
}

/// <summary>
/// Exception thrown when all failover attempts fail
/// </summary>
public class FailoverException : Exception
{
    public FailoverException(string message) : base(message) { }
    public FailoverException(string message, Exception innerException) : base(message, innerException) { }
}
