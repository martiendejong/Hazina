using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using WebSearch.Core;
using WebSearch.Infrastructure;

namespace WebSearch;

/// <summary>
/// Dependency injection extensions for WebSearch
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWebSearch(
        this IServiceCollection services,
        Action<WebSearchOptions>? configure = null)
    {
        var options = new WebSearchOptions();
        configure?.Invoke(options);

        // Register memory cache if not already registered
        if (!services.Any(x => x.ServiceType == typeof(IMemoryCache)))
        {
            services.AddMemoryCache();
        }

        // Register cache
        if (options.EnableCaching)
        {
            services.AddSingleton<ISearchCache, InMemorySearchCache>();
        }

        // Register rate limiter
        if (options.RequestsPerMinute > 0)
        {
            services.AddSingleton<IRateLimiter>(sp =>
                new TokenBucketRateLimiter(options.RequestsPerMinute));
        }

        // Register factory
        services.AddSingleton<SearchProviderFactory>();

        // Register default search service
        services.AddTransient(sp =>
        {
            var factory = sp.GetRequiredService<SearchProviderFactory>();
            return factory.Create(options.DefaultProvider);
        });

        return services;
    }
}

/// <summary>
/// Configuration options for WebSearch
/// </summary>
public class WebSearchOptions
{
    /// <summary>
    /// Default search provider (default: Google)
    /// </summary>
    public ProviderType DefaultProvider { get; set; } = ProviderType.Google;

    /// <summary>
    /// Enable result caching (default: true)
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache TTL in seconds (default: 3600 = 1 hour)
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 3600;

    /// <summary>
    /// Requests per minute for rate limiting (default: 10, 0 = disabled)
    /// </summary>
    public int RequestsPerMinute { get; set; } = 10;
}
