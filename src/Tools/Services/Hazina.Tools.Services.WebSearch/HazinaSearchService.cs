using Microsoft.Extensions.Logging;
using WebSearch;
using WebSearch.Core;

namespace Hazina.Tools.Services.WebSearch;

/// <summary>
/// Hazina-integrated web search service with structured logging, error handling, and DI support.
/// Wraps the WebSearch library and provides a Hazina-aware interface.
/// </summary>
public class HazinaSearchService
{
    private readonly SearchProviderFactory _factory;
    private readonly ILogger<HazinaSearchService> _logger;
    private readonly HazinaWebSearchOptions _options;

    public HazinaSearchService(
        SearchProviderFactory factory,
        ILogger<HazinaSearchService> logger,
        HazinaWebSearchOptions options)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Performs a web search using the configured default provider.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="maxResults">Maximum number of results (default uses options value).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of search results, or empty array on failure.</returns>
    public async Task<SearchResult[]> SearchAsync(
        string query,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        return await SearchWithProviderAsync(query, _options.DefaultProvider, maxResults, cancellationToken);
    }

    /// <summary>
    /// Performs a web search using the specified provider.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="provider">The search provider to use.</param>
    /// <param name="maxResults">Maximum number of results (default uses options value).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Array of search results, or empty array on failure.</returns>
    public async Task<SearchResult[]> SearchWithProviderAsync(
        string query,
        ProviderType provider,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("SearchAsync called with empty query");
            return Array.Empty<SearchResult>();
        }

        var resultCount = maxResults ?? _options.MaxResults;
        _logger.LogInformation("Performing web search via {Provider}: {Query} (max {MaxResults} results)",
            provider, query, resultCount);

        try
        {
            var options = new SearchOptions { MaxResults = resultCount };
            var service = _factory.Create(provider);
            var results = await service.SearchAsync(query, options, cancellationToken);

            _logger.LogInformation("Web search completed: {ResultCount} results for query '{Query}'",
                results.Length, query);

            return results;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Web search cancelled for query: {Query}", query);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Web search failed for query '{Query}' using provider {Provider}", query, provider);
            return Array.Empty<SearchResult>();
        }
    }

    /// <summary>
    /// Gets the default provider configured for this service.
    /// </summary>
    public ProviderType DefaultProvider => _options.DefaultProvider;
}
