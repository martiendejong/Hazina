using AngleSharp;
using AngleSharp.Dom;
using System.Web;
using WebSearch.Core;

namespace WebSearch.Providers;

/// <summary>
/// Bing Search provider using HTML scraping
/// </summary>
public class BingSearchProvider : ISearchProvider
{
    private static readonly HttpClient _httpClient = new();
    private static readonly string[] _userAgents = new[]
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36"
    };

    private static int _userAgentIndex = 0;

    public string GetProviderName() => "Bing";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, "https://www.bing.com");
            request.Headers.Add("User-Agent", GetNextUserAgent());

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

        var searchUrl = BuildSearchUrl(query, options);
        var html = await FetchSearchPageAsync(searchUrl, cancellationToken);
        var results = await ParseSearchResultsAsync(html, cancellationToken);

        return results.Take(options.MaxResults).ToArray();
    }

    private string BuildSearchUrl(string query, SearchOptions options)
    {
        var baseUrl = "https://www.bing.com/search";
        var parameters = new Dictionary<string, string>
        {
            ["q"] = query,
            ["count"] = Math.Min(options.MaxResults, 50).ToString()
        };

        if (!string.IsNullOrEmpty(options.Language))
            parameters["setlang"] = options.Language;

        if (options.SafeSearch)
            parameters["safesearch"] = "strict";

        if (options.Page > 0)
            parameters["first"] = (options.Page * options.MaxResults + 1).ToString();

        var queryString = string.Join("&", parameters.Select(kvp =>
            $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        return $"{baseUrl}?{queryString}";
    }

    private async Task<string> FetchSearchPageAsync(string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Add("User-Agent", GetNextUserAgent());
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        request.Headers.Add("Accept-Encoding", "gzip, deflate, br");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Bing search failed with status {response.StatusCode}");
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

        // Bing SERP structure:
        // Results: li.b_algo
        // Title: h2 > a
        // URL: cite
        // Snippet: div.b_caption p

        var resultContainers = document.QuerySelectorAll("li.b_algo");

        foreach (var container in resultContainers)
        {
            try
            {
                var titleElement = container.QuerySelector("h2 a, h2");
                if (titleElement == null) continue;
                var title = titleElement.TextContent.Trim();
                if (string.IsNullOrEmpty(title)) continue;

                var linkElement = container.QuerySelector("h2 a, a");
                if (linkElement == null) continue;
                var url = linkElement.GetAttribute("href");
                if (string.IsNullOrEmpty(url)) continue;

                var snippetElement = container.QuerySelector("div.b_caption p, p");
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

    private static string GetNextUserAgent()
    {
        var index = Interlocked.Increment(ref _userAgentIndex) % _userAgents.Length;
        return _userAgents[index];
    }
}
