using AngleSharp;
using System.Web;
using WebSearch.Core;

namespace WebSearch.Providers;

/// <summary>
/// DuckDuckGo Search provider
/// </summary>
public class DuckDuckGoProvider : ISearchProvider
{
    private static readonly HttpClient _httpClient = new();

    public string GetProviderName() => "DuckDuckGo";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, "https://html.duckduckgo.com");
            var response = await _httpClient.SendAsync(request, cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<SearchResult[]> SearchAsync(
        string query,
        SearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("Query cannot be empty", nameof(query));

        options ??= new SearchOptions();

        // Use DuckDuckGo HTML version (no JavaScript required)
        var searchUrl = BuildSearchUrl(query, options);
        var html = await FetchSearchPageAsync(searchUrl, cancellationToken);
        var results = await ParseSearchResultsAsync(html, cancellationToken);

        return results.Take(options.MaxResults).ToArray();
    }

    private string BuildSearchUrl(string query, SearchOptions options)
    {
        var baseUrl = "https://html.duckduckgo.com/html/";
        var parameters = new Dictionary<string, string>
        {
            ["q"] = query
        };

        if (options.SafeSearch)
            parameters["kp"] = "1"; // Strict safe search

        var queryString = string.Join("&", parameters.Select(kvp =>
            $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        return $"{baseUrl}?{queryString}";
    }

    private async Task<string> FetchSearchPageAsync(string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"DuckDuckGo search failed with status {response.StatusCode}");
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private async Task<List<SearchResult>> ParseSearchResultsAsync(string html, CancellationToken cancellationToken)
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(html), cancellationToken);

        var results = new List<SearchResult>();
        var rank = 1;

        // DuckDuckGo HTML structure:
        // Results: div.result or div.web-result
        // Title: a.result__a
        // Snippet: a.result__snippet

        var resultContainers = document.QuerySelectorAll("div.result, div.web-result");

        foreach (var container in resultContainers)
        {
            try
            {
                var titleElement = container.QuerySelector("a.result__a, h2.result__title a");
                if (titleElement == null) continue;
                var title = titleElement.TextContent.Trim();
                if (string.IsNullOrEmpty(title)) continue;

                var url = titleElement.GetAttribute("href");
                if (string.IsNullOrEmpty(url)) continue;

                // DuckDuckGo uses redirect URLs, extract actual URL
                if (url.Contains("uddg="))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(url, @"uddg=([^&]+)");
                    if (match.Success)
                    {
                        url = HttpUtility.UrlDecode(match.Groups[1].Value);
                    }
                }

                var snippetElement = container.QuerySelector("a.result__snippet, div.result__snippet");
                var snippet = snippetElement?.TextContent.Trim();

                results.Add(new SearchResult
                {
                    Title = title,
                    Url = url,
                    Snippet = snippet,
                    Source = GetProviderName(),
                    Rank = rank++,
                    FetchedAt = DateTime.UtcNow
                });
            }
            catch
            {
                continue;
            }
        }

        return results;
    }
}
