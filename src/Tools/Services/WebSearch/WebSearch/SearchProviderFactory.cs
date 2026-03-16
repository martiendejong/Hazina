using WebSearch.Core;
using WebSearch.Infrastructure;
using WebSearch.Providers;

namespace WebSearch;

/// <summary>
/// Factory for creating search providers
/// </summary>
public class SearchProviderFactory
{
    private readonly ISearchCache? _cache;
    private readonly IRateLimiter? _rateLimiter;

    public SearchProviderFactory(ISearchCache? cache = null, IRateLimiter? rateLimiter = null)
    {
        _cache = cache;
        _rateLimiter = rateLimiter;
    }

    public SearchService Create(ProviderType providerType)
    {
        ISearchProvider provider = providerType switch
        {
            ProviderType.Google => new GoogleSearchProvider(),
            ProviderType.Bing => new BingSearchProvider(),
            ProviderType.DuckDuckGo => new DuckDuckGoProvider(),
            _ => throw new NotSupportedException($"Provider {providerType} is not yet implemented")
        };

        return new SearchService(provider, _cache, _rateLimiter);
    }

    public SearchService CreateWithFallback(params ProviderType[] providers)
    {
        if (providers == null || providers.Length == 0)
        {
            throw new ArgumentException("At least one provider must be specified", nameof(providers));
        }

        var primaryProvider = Create(providers[0]);
        // TODO: Implement fallback chain logic
        return primaryProvider;
    }

    public SearchService[] CreateAll()
    {
        return new[]
        {
            Create(ProviderType.Google),
            Create(ProviderType.Bing),
            Create(ProviderType.DuckDuckGo)
        };
    }
}
