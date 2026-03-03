using Hazina.App.HazinaCoder.Core.Events;

namespace Hazina.App.HazinaCoder.Core.Providers;

/// <summary>
/// Intelligent provider routing with failover and optimization
/// </summary>
public class ProviderRouter
{
    private readonly List<ILLMProvider> _providers;
    private readonly IEventBus? _eventBus;
    private readonly ProviderRoutingStrategy _strategy;

    public ProviderRouter(
        List<ILLMProvider> providers,
        ProviderRoutingStrategy strategy = ProviderRoutingStrategy.Balanced,
        IEventBus? eventBus = null)
    {
        _providers = providers.Where(p => p.IsAvailable).ToList();
        _strategy = strategy;
        _eventBus = eventBus;

        if (!_providers.Any())
        {
            throw new InvalidOperationException("No LLM providers available. Configure at least one provider.");
        }
    }

    /// <summary>
    /// Generate with automatic provider selection and failover
    /// </summary>
    public async Task<LLMResponse> GenerateAsync(
        LLMRequest request,
        ProviderPreferences? preferences = null,
        CancellationToken cancellationToken = default)
    {
        preferences ??= new ProviderPreferences();

        // Select providers based on strategy and preferences
        var candidateProviders = SelectProviders(request, preferences);

        // Try providers in order until success
        Exception? lastError = null;

        foreach (var provider in candidateProviders)
        {
            try
            {
                _eventBus?.Publish(new ProviderSelectedEvent(
                    provider.ProviderName,
                    request.Model,
                    _strategy.ToString()));

                var response = await provider.GenerateAsync(request, cancellationToken);

                if (response.Success)
                {
                    _eventBus?.Publish(new ProviderSuccessEvent(
                        provider.ProviderName,
                        request.Model,
                        response.Duration,
                        response.Usage.Cost));

                    return response;
                }

                // Provider returned error, try next
                lastError = new Exception(response.Error);
            }
            catch (Exception ex)
            {
                lastError = ex;

                _eventBus?.Publish(new ProviderFailureEvent(
                    provider.ProviderName,
                    request.Model,
                    ex));

                // Continue to next provider
            }
        }

        // All providers failed
        return new LLMResponse
        {
            Success = false,
            Error = $"All providers failed. Last error: {lastError?.Message}",
            Provider = "None"
        };
    }

    /// <summary>
    /// Generate with streaming and failover
    /// </summary>
    public async IAsyncEnumerable<LLMStreamChunk> GenerateStreamAsync(
        LLMRequest request,
        ProviderPreferences? preferences = null,
        CancellationToken cancellationToken = default)
    {
        preferences ??= new ProviderPreferences();

        var candidateProviders = SelectProviders(request, preferences)
            .Where(p => p.Capabilities.SupportsStreaming)
            .ToList();

        if (!candidateProviders.Any())
        {
            yield return new LLMStreamChunk
            {
                Content = string.Empty,
                IsComplete = true,
                Error = "No streaming-capable providers available"
            };
            yield break;
        }

        // Try first provider that supports streaming
        var provider = candidateProviders.First();

        _eventBus?.Publish(new ProviderSelectedEvent(
            provider.ProviderName,
            request.Model,
            "Streaming"));

        await foreach (var chunk in provider.GenerateStreamAsync(request, cancellationToken))
        {
            yield return chunk;

            if (chunk.IsComplete && chunk.Usage != null)
            {
                _eventBus?.Publish(new ProviderSuccessEvent(
                    provider.ProviderName,
                    request.Model,
                    TimeSpan.Zero,
                    chunk.Usage.Cost));
            }
        }
    }

    /// <summary>
    /// Select providers based on strategy and preferences
    /// </summary>
    private List<ILLMProvider> SelectProviders(LLMRequest request, ProviderPreferences preferences)
    {
        var candidates = _providers.AsEnumerable();

        // Filter by capabilities
        if (request.Messages.Any(m => m.Contents?.Any(c => c.Type == "image") == true))
        {
            candidates = candidates.Where(p => p.Capabilities.SupportsVision);
        }

        if (request.Tools?.Any() == true)
        {
            candidates = candidates.Where(p => p.Capabilities.SupportsToolUse);
        }

        // Filter by model if specified
        if (!string.IsNullOrEmpty(preferences.PreferredProvider))
        {
            var preferredProvider = candidates.FirstOrDefault(
                p => p.ProviderName.Equals(preferences.PreferredProvider, StringComparison.OrdinalIgnoreCase));

            if (preferredProvider != null)
            {
                // Put preferred provider first
                candidates = new[] { preferredProvider }
                    .Concat(candidates.Where(p => p != preferredProvider));
            }
        }

        // Sort by strategy
        candidates = _strategy switch
        {
            ProviderRoutingStrategy.CostOptimized => SortByCost(candidates, request),
            ProviderRoutingStrategy.QualityOptimized => SortByQuality(candidates),
            ProviderRoutingStrategy.LatencyOptimized => SortByLatency(candidates),
            ProviderRoutingStrategy.Balanced => SortByBalance(candidates, request),
            _ => candidates
        };

        return candidates.ToList();
    }

    /// <summary>
    /// Sort providers by cost (cheapest first)
    /// </summary>
    private IEnumerable<ILLMProvider> SortByCost(IEnumerable<ILLMProvider> providers, LLMRequest request)
    {
        return providers.OrderBy(p =>
        {
            var pricing = p.GetPricing(request.Model);
            var estimatedTokens = request.MaxTokens;
            return pricing.CalculateCost(1000, estimatedTokens); // Rough estimate
        });
    }

    /// <summary>
    /// Sort providers by quality (best first)
    /// </summary>
    private IEnumerable<ILLMProvider> SortByQuality(IEnumerable<ILLMProvider> providers)
    {
        // Quality ranking: Anthropic > OpenAI > Google > Local
        var qualityRank = new Dictionary<string, int>
        {
            ["Anthropic"] = 1,
            ["OpenAI"] = 2,
            ["Google"] = 3,
            ["Ollama"] = 4,
            ["HuggingFace"] = 5
        };

        return providers.OrderBy(p => qualityRank.GetValueOrDefault(p.ProviderName, 99));
    }

    /// <summary>
    /// Sort providers by latency (fastest first)
    /// </summary>
    private IEnumerable<ILLMProvider> SortByLatency(IEnumerable<ILLMProvider> providers)
    {
        // Local models typically fastest, then cloud providers
        var latencyRank = new Dictionary<string, int>
        {
            ["Ollama"] = 1,
            ["OpenAI"] = 2,
            ["Anthropic"] = 3,
            ["Google"] = 4
        };

        return providers.OrderBy(p => latencyRank.GetValueOrDefault(p.ProviderName, 99));
    }

    /// <summary>
    /// Sort providers by balance of cost, quality, and latency
    /// </summary>
    private IEnumerable<ILLMProvider> SortByBalance(IEnumerable<ILLMProvider> providers, LLMRequest request)
    {
        // For simple tasks, prefer cheaper/faster providers
        // For complex tasks, prefer quality providers
        var isComplexTask = request.MaxTokens > 2000 ||
                           request.Messages.Count > 5 ||
                           request.Tools?.Any() == true;

        return isComplexTask
            ? SortByQuality(providers)
            : SortByCost(providers, request);
    }

    /// <summary>
    /// Get all available providers
    /// </summary>
    public List<ILLMProvider> GetAvailableProviders()
    {
        return _providers.ToList();
    }

    /// <summary>
    /// Get provider by name
    /// </summary>
    public ILLMProvider? GetProvider(string providerName)
    {
        return _providers.FirstOrDefault(p =>
            p.ProviderName.Equals(providerName, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Check health of all providers
    /// </summary>
    public async Task<Dictionary<string, ProviderHealth>> CheckAllHealthAsync()
    {
        var healthChecks = new Dictionary<string, ProviderHealth>();

        foreach (var provider in _providers)
        {
            try
            {
                var health = await provider.GetHealthAsync();
                healthChecks[provider.ProviderName] = health;
            }
            catch (Exception ex)
            {
                healthChecks[provider.ProviderName] = new ProviderHealth
                {
                    IsHealthy = false,
                    Status = "error",
                    Message = ex.Message,
                    LastCheck = DateTime.UtcNow
                };
            }
        }

        return healthChecks;
    }
}

/// <summary>
/// Provider routing strategy
/// </summary>
public enum ProviderRoutingStrategy
{
    /// <summary>
    /// Balance cost, quality, and latency based on task complexity
    /// </summary>
    Balanced,

    /// <summary>
    /// Minimize cost - use cheapest providers first
    /// </summary>
    CostOptimized,

    /// <summary>
    /// Maximize quality - use best models first
    /// </summary>
    QualityOptimized,

    /// <summary>
    /// Minimize latency - use fastest providers first
    /// </summary>
    LatencyOptimized
}

/// <summary>
/// Provider preferences for routing
/// </summary>
public class ProviderPreferences
{
    public string? PreferredProvider { get; set; }
    public string? PreferredModel { get; set; }
    public double? MaxCostPerRequest { get; set; }
    public TimeSpan? MaxLatency { get; set; }
    public bool RequireVision { get; set; }
    public bool RequireToolUse { get; set; }
    public bool RequireStreaming { get; set; }
}
