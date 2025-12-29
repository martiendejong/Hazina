namespace Hazina.AI.Providers.Cost;

/// <summary>
/// Tracks costs and token usage across providers
/// </summary>
public class CostTracker : ICostTracker
{
    private readonly Dictionary<string, TokenUsageInfo> _usageByProvider = new();
    private readonly object _lock = new();

    /// <summary>
    /// Record token usage and cost
    /// </summary>
    public void RecordUsage(string providerName, TokenUsageInfo usage)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be empty", nameof(providerName));
        if (usage == null)
            throw new ArgumentNullException(nameof(usage));

        lock (_lock)
        {
            if (_usageByProvider.TryGetValue(providerName, out var existing))
            {
                _usageByProvider[providerName] = existing + usage;
            }
            else
            {
                _usageByProvider[providerName] = usage;
            }
        }
    }

    /// <summary>
    /// Get total cost for a provider
    /// </summary>
    public decimal GetTotalCost(string providerName)
    {
        lock (_lock)
        {
            return _usageByProvider.TryGetValue(providerName, out var usage)
                ? usage.TotalCost
                : 0m;
        }
    }

    /// <summary>
    /// Get total cost across all providers
    /// </summary>
    public decimal GetTotalCost()
    {
        lock (_lock)
        {
            return _usageByProvider.Values.Sum(u => u.TotalCost);
        }
    }

    /// <summary>
    /// Get cost breakdown by provider
    /// </summary>
    public Dictionary<string, decimal> GetCostByProvider()
    {
        lock (_lock)
        {
            return _usageByProvider.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.TotalCost
            );
        }
    }

    /// <summary>
    /// Get token usage for a provider
    /// </summary>
    public TokenUsageInfo GetUsage(string providerName)
    {
        lock (_lock)
        {
            return _usageByProvider.TryGetValue(providerName, out var usage)
                ? usage
                : new TokenUsageInfo();
        }
    }

    /// <summary>
    /// Get total token usage across all providers
    /// </summary>
    public TokenUsageInfo GetTotalUsage()
    {
        lock (_lock)
        {
            var total = new TokenUsageInfo();
            foreach (var usage in _usageByProvider.Values)
            {
                total = total + usage;
            }
            return total;
        }
    }

    /// <summary>
    /// Reset cost tracking
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _usageByProvider.Clear();
        }
    }

    /// <summary>
    /// Reset cost tracking for a specific provider
    /// </summary>
    public void Reset(string providerName)
    {
        lock (_lock)
        {
            _usageByProvider.Remove(providerName);
        }
    }
}
