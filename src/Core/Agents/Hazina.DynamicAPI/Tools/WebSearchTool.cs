using System.Text.Json;
using Hazina.DynamicAPI.Core;

namespace Hazina.DynamicAPI.Tools;

/// <summary>
/// Web search tool for finding API documentation and technical information.
/// Uses Bing Search API to find relevant documentation.
/// </summary>
public class WebSearchTool
{
    private readonly CredentialStore _credentialStore;
    private readonly HttpClient _httpClient;
    private const string BingSearchEndpoint = "https://api.bing.microsoft.com/v7.0/search";

    public WebSearchTool(CredentialStore credentialStore, HttpClient? httpClient = null)
    {
        _credentialStore = credentialStore;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Search the web for information
    /// </summary>
    /// <param name="query">Search query</param>
    /// <param name="count">Number of results to return (max 50)</param>
    /// <returns>Search results</returns>
    public async Task<WebSearchResult> Search(string query, int count = 5)
    {
        try
        {
            var apiKey = await _credentialStore.GetCredential("bing", "api_key");

            var url = $"{BingSearchEndpoint}?q={Uri.EscapeDataString(query)}&count={Math.Min(count, 50)}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);

            var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var searchResponse = JsonSerializer.Deserialize<BingSearchResponse>(json);

            if (searchResponse?.webPages?.value == null)
            {
                return new WebSearchResult
                {
                    Query = query,
                    Results = new List<SearchResultItem>()
                };
            }

            return new WebSearchResult
            {
                Query = query,
                Results = searchResponse.webPages.value.Select(v => new SearchResultItem
                {
                    Title = v.name ?? string.Empty,
                    Url = v.url ?? string.Empty,
                    Snippet = v.snippet ?? string.Empty
                }).ToList()
            };
        }
        catch (CredentialNotFoundException)
        {
            throw new Exception("Bing Search API key not found. Please set it in credentials/bing.json or environment variable BING_API_KEY");
        }
        catch (Exception ex)
        {
            throw new Exception($"Web search failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Search specifically for API documentation
    /// </summary>
    public async Task<WebSearchResult> SearchApiDocumentation(string serviceName, string topic = "")
    {
        var query = string.IsNullOrEmpty(topic)
            ? $"{serviceName} API documentation"
            : $"{serviceName} API documentation {topic}";

        return await Search(query, 10);
    }

    /// <summary>
    /// Fetch the content of a specific URL
    /// </summary>
    public async Task<string> FetchUrl(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to fetch URL {url}: {ex.Message}", ex);
        }
    }
}

public class WebSearchResult
{
    public string Query { get; set; } = string.Empty;
    public List<SearchResultItem> Results { get; set; } = new();
}

public class SearchResultItem
{
    public string Title { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}

// Bing Search API response models
#pragma warning disable IDE1006 // Naming Styles (Bing uses lowercase)
internal class BingSearchResponse
{
    public WebPages? webPages { get; set; }
}

internal class WebPages
{
    public List<WebPage>? value { get; set; }
}

internal class WebPage
{
    public string? name { get; set; }
    public string? url { get; set; }
    public string? snippet { get; set; }
}
#pragma warning restore IDE1006
