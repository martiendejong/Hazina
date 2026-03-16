namespace WebSearch.Core;

/// <summary>
/// Interface for search engine providers
/// </summary>
public interface ISearchProvider
{
    /// <summary>
    /// Get the name of this provider
    /// </summary>
    string GetProviderName();

    /// <summary>
    /// Check if the provider is currently available
    /// </summary>
    /// <returns>True if provider can be used, false if blocked/unavailable</returns>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Perform a search query
    /// </summary>
    /// <param name="query">Search query string</param>
    /// <param name="options">Search options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of search results</returns>
    Task<SearchResult[]> SearchAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default);
}
