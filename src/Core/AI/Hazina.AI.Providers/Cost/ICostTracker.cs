namespace Hazina.AI.Providers.Cost;

/// <summary>
/// Interface for tracking LLM costs
/// </summary>
public interface ICostTracker
{
    /// <summary>
    /// Record token usage and cost
    /// </summary>
    void RecordUsage(string providerName, TokenUsageInfo usage);

    /// <summary>
    /// Get total cost for a provider
    /// </summary>
    decimal GetTotalCost(string providerName);

    /// <summary>
    /// Get total cost across all providers
    /// </summary>
    decimal GetTotalCost();

    /// <summary>
    /// Get cost breakdown by provider
    /// </summary>
    Dictionary<string, decimal> GetCostByProvider();

    /// <summary>
    /// Get token usage for a provider
    /// </summary>
    TokenUsageInfo GetUsage(string providerName);

    /// <summary>
    /// Get total token usage across all providers
    /// </summary>
    TokenUsageInfo GetTotalUsage();

    /// <summary>
    /// Reset cost tracking
    /// </summary>
    void Reset();

    /// <summary>
    /// Reset cost tracking for a specific provider
    /// </summary>
    void Reset(string providerName);
}
