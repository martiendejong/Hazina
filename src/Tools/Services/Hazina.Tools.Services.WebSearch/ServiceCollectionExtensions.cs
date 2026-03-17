using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using WebSearch;
using WebSearch.Core;
using WebSearch.Infrastructure;

namespace Hazina.Tools.Services.WebSearch;

/// <summary>
/// DI registration extensions for Hazina WebSearch integration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Hazina WebSearch service and its dependencies.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional configuration callback.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <example>
    /// <code>
    /// services.AddHazinaWebSearch(options => {
    ///     options.DefaultProvider = ProviderType.DuckDuckGo;
    ///     options.EnableCaching = true;
    ///     options.MaxResults = 10;
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddHazinaWebSearch(
        this IServiceCollection services,
        Action<HazinaWebSearchOptions>? configure = null)
    {
        var options = new HazinaWebSearchOptions();
        configure?.Invoke(options);

        // Register the options instance
        services.AddSingleton(options);

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
        else
        {
            // Register a no-op cache stub so factory can still be constructed
            services.AddSingleton<ISearchCache>(sp =>
            {
                var mc = sp.GetRequiredService<IMemoryCache>();
                return new InMemorySearchCache(mc);
            });
        }

        // Register rate limiter
        if (options.RequestsPerMinute > 0)
        {
            services.AddSingleton<IRateLimiter>(sp =>
                new TokenBucketRateLimiter(options.RequestsPerMinute));
        }
        else
        {
            // Register a passthrough rate limiter (no throttling)
            services.AddSingleton<IRateLimiter, NoOpRateLimiter>();
        }

        // Register provider factory
        services.AddSingleton<SearchProviderFactory>();

        // Register main Hazina search service
        services.AddTransient<HazinaSearchService>();

        return services;
    }
}

/// <summary>
/// Configuration options for the Hazina WebSearch integration.
/// </summary>
public class HazinaWebSearchOptions
{
    /// <summary>
    /// Default search provider (default: DuckDuckGo - no API key required).
    /// </summary>
    public ProviderType DefaultProvider { get; set; } = ProviderType.DuckDuckGo;

    /// <summary>
    /// Enable result caching (default: true).
    /// </summary>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Cache TTL in seconds (default: 3600 = 1 hour).
    /// </summary>
    public int CacheTtlSeconds { get; set; } = 3600;

    /// <summary>
    /// Requests per minute for rate limiting (default: 30, 0 = disabled).
    /// </summary>
    public int RequestsPerMinute { get; set; } = 30;

    /// <summary>
    /// Default maximum number of results per search (default: 10).
    /// </summary>
    public int MaxResults { get; set; } = 10;
}

/// <summary>
/// Passthrough rate limiter that imposes no delays.
/// Used when rate limiting is disabled (RequestsPerMinute = 0).
/// </summary>
internal sealed class NoOpRateLimiter : IRateLimiter
{
    public Task WaitAsync(string providerName, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public bool IsAllowed(string providerName) => true;
}
