using Hazina.AI.Compression.Interfaces;

namespace Hazina.AI.Compression.TokenCounting;

/// <summary>
/// Factory for creating provider-specific token counters
/// </summary>
public class TokenCounterFactory
{
    private static readonly Lazy<TokenCounterFactory> _instance = new(() => new TokenCounterFactory());
    private readonly Dictionary<string, ITokenCounter> _counters = new();
    private readonly ITokenCounter _fallbackCounter;

    public static TokenCounterFactory Instance => _instance.Value;

    private TokenCounterFactory()
    {
        // Register provider-specific counters
        _counters["openai"] = new OpenAITokenCounter();
        _counters["claude"] = new ClaudeTokenCounter();
        _counters["anthropic"] = new ClaudeTokenCounter();
        _counters["gemini"] = new GeminiTokenCounter();
        _counters["google"] = new GeminiTokenCounter();

        // Fallback for unknown providers
        _fallbackCounter = new ApproximateTokenCounter("Unknown", 4.0);
    }

    /// <summary>
    /// Get token counter for a specific provider or model
    /// </summary>
    public ITokenCounter GetCounter(string? providerOrModel)
    {
        if (string.IsNullOrEmpty(providerOrModel))
            return _fallbackCounter;

        var normalized = providerOrModel.ToLowerInvariant();

        // Try exact provider match first
        foreach (var kvp in _counters)
        {
            if (normalized.Contains(kvp.Key))
                return kvp.Value;
        }

        // Try model name match
        foreach (var counter in _counters.Values)
        {
            if (counter.SupportsModel(providerOrModel))
                return counter;
        }

        // Fallback to approximation
        return _fallbackCounter;
    }

    /// <summary>
    /// Register a custom token counter
    /// </summary>
    public void RegisterCounter(string provider, ITokenCounter counter)
    {
        _counters[provider.ToLowerInvariant()] = counter;
    }
}
