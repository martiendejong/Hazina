using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Health;

namespace Hazina.AI.Providers.Selection;

/// <summary>
/// Strategy for selecting which provider to use
/// </summary>
public enum SelectionStrategy
{
    /// <summary>
    /// Use the first available healthy provider by priority
    /// </summary>
    Priority,

    /// <summary>
    /// Use the cheapest provider that meets requirements
    /// </summary>
    LeastCost,

    /// <summary>
    /// Use the fastest provider based on recent response times
    /// </summary>
    FastestResponse,

    /// <summary>
    /// Round-robin across healthy providers
    /// </summary>
    RoundRobin,

    /// <summary>
    /// Random selection from healthy providers
    /// </summary>
    Random,

    /// <summary>
    /// Use specific provider by name
    /// </summary>
    Specific,

    /// <summary>
    /// Custom selection logic
    /// </summary>
    Custom
}

/// <summary>
/// Context for provider selection
/// </summary>
public class SelectionContext
{
    /// <summary>
    /// Required capabilities
    /// </summary>
    public ProviderCapabilities? RequiredCapabilities { get; set; }

    /// <summary>
    /// Specific provider name (for Specific strategy)
    /// </summary>
    public string? SpecificProvider { get; set; }

    /// <summary>
    /// Maximum acceptable cost per 1K tokens
    /// </summary>
    public decimal? MaxCostPer1KTokens { get; set; }

    /// <summary>
    /// Maximum acceptable response time
    /// </summary>
    public TimeSpan? MaxResponseTime { get; set; }

    /// <summary>
    /// Minimum required success rate (0-1)
    /// </summary>
    public double? MinSuccessRate { get; set; }

    /// <summary>
    /// Custom selection function
    /// </summary>
    public Func<IEnumerable<(string Name, ILLMClient Client, ProviderMetadata Metadata, ProviderHealthStatus Health)>, string?>? CustomSelector { get; set; }

    /// <summary>
    /// Exclude specific providers
    /// </summary>
    public HashSet<string> ExcludedProviders { get; set; } = new();
}

/// <summary>
/// Result of provider selection
/// </summary>
public class SelectionResult
{
    public bool Success { get; set; }
    public string? ProviderName { get; set; }
    public ILLMClient? Provider { get; set; }
    public ProviderMetadata? Metadata { get; set; }
    public ProviderHealthStatus? HealthStatus { get; set; }
    public string? FailureReason { get; set; }
}
