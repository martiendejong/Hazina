using AngleSharp;
using AngleSharp.Dom;
using System.Web;
using WebSearch.Core;

namespace WebSearch.Providers;

/// <summary>
/// Google Search provider using HTML scraping
/// </summary>
public class GoogleSearchProvider : ISearchProvider
{
    private static readonly HttpClient _httpClient = new();
    private static readonly string[] _userAgents = new[]
    {
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:134.0) Gecko/20100101 Firefox/134.0",
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 14.7; rv:134.0) Gecko/20100101 Firefox/134.0"
    };

    private static int _userAgentIndex = 0;

    public string GetProviderName() => "Google";

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Head, "https://www.google.com");
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

        // Build Google search URL
        var searchUrl = BuildSearchUrl(query, options);

        // Fetch HTML
        var html = await FetchSearchPageAsync(searchUrl, cancellationToken);

        // Parse results
        var results = await ParseSearchResultsAsync(html, cancellationToken);

        // Limit to max results
        return results.Take(options.MaxResults).ToArray();
    }

    private string BuildSearchUrl(string query, SearchOptions options)
    {
        var baseUrl = "https://www.google.com/search";
        var parameters = new Dictionary<string, string>
        {
            ["q"] = query,
            ["num"] = Math.Min(options.MaxResults, 100).ToString() // Google max is 100
        };

        if (!string.IsNullOrEmpty(options.Language))
            parameters["hl"] = options.Language;

        if (!string.IsNullOrEmpty(options.Region))
            parameters["gl"] = options.Region;

        if (options.SafeSearch)
            parameters["safe"] = "active";

        if (!string.IsNullOrEmpty(options.TimeRange))
            parameters["tbs"] = $"qdr:{options.TimeRange}";

        if (options.Page > 0)
            parameters["start"] = (options.Page * options.MaxResults).ToString();

        var queryString = string.Join("&", parameters.Select(kvp =>
            $"{HttpUtility.UrlEncode(kvp.Key)}={HttpUtility.UrlEncode(kvp.Value)}"));

        return $"{baseUrl}?{queryString}";
    }

    private async Task<string> FetchSearchPageAsync(string url, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Mimic browser headers
        request.Headers.Add("User-Agent", GetNextUserAgent());
        request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
        request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
        request.Headers.Add("DNT", "1");
        request.Headers.Add("Connection", "keep-alive");
        request.Headers.Add("Upgrade-Insecure-Requests", "1");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Google search failed with status {response.StatusCode}. " +
                "This might indicate rate limiting or CAPTCHA challenge.");
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

        // Google SERP structure (as of 2026):
        // Main results container: div#search
        // Individual results: div.g or div[data-sokoban-container]
        // Title: h3
        // URL: cite element or data-url attribute
        // Snippet: div.VwiC3b or div[data-content-feature]

        var resultContainers = document.QuerySelectorAll("div.g, div[data-sokoban-container]");

        foreach (var container in resultContainers)
        {
            try
            {
                // Extract title
                var titleElement = container.QuerySelector("h3");
                if (titleElement == null) continue;
                var title = titleElement.TextContent.Trim();
                if (string.IsNullOrEmpty(title)) continue;

                // Extract URL
                var linkElement = container.QuerySelector("a");
                if (linkElement == null) continue;
                var url = linkElement.GetAttribute("href");
                if (string.IsNullOrEmpty(url) || url.StartsWith("/search")) continue;

                // Clean Google redirect URLs
                if (url.Contains("/url?q="))
                {
                    var urlMatch = System.Text.RegularExpressions.Regex.Match(url, @"/url\?q=([^&]+)");
                    if (urlMatch.Success)
                    {
                        url = HttpUtility.UrlDecode(urlMatch.Groups[1].Value);
                    }
                }

                // Extract snippet
                var snippetElement = container.QuerySelector("div.VwiC3b, div[data-content-feature], div.IsZvec");
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
                // Skip malformed results
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
