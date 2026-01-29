using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Tool for searching the web (like Claude Code's WebSearch)
/// Uses DuckDuckGo HTML search (no API key required)
/// </summary>
public static class WebSearchTool
{
    private static readonly HttpClient _httpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    static WebSearchTool()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "web_search",
            description: "Search the web for current information. Returns search results with titles, URLs, and snippets.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "query",
                    Type = "string",
                    Required = true,
                    Description = "The search query"
                },
                new()
                {
                    Name = "max_results",
                    Type = "number",
                    Required = false,
                    Description = "Maximum number of results to return (default: 10, max: 20)"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("query", out var queryElement))
                        return "Error: query parameter is required";

                    var query = queryElement.GetString();
                    if (string.IsNullOrWhiteSpace(query))
                        return "Error: query cannot be empty";

                    int maxResults = 10;
                    if (root.TryGetProperty("max_results", out var maxElement))
                    {
                        maxResults = Math.Min(maxElement.GetInt32(), 20);
                    }

                    // Use DuckDuckGo HTML search
                    var encodedQuery = HttpUtility.UrlEncode(query);
                    var url = $"https://html.duckduckgo.com/html/?q={encodedQuery}";

                    var response = await _httpClient.GetAsync(url, cancel);
                    response.EnsureSuccessStatusCode();

                    var html = await response.Content.ReadAsStringAsync(cancel);
                    var results = ParseDuckDuckGoResults(html, maxResults);

                    if (results.Count == 0)
                    {
                        return $"No results found for: {query}";
                    }

                    var sb = new StringBuilder();
                    sb.AppendLine($"Search results for: {query}");
                    sb.AppendLine($"Found {results.Count} results:");
                    sb.AppendLine();

                    for (int i = 0; i < results.Count; i++)
                    {
                        var result = results[i];
                        sb.AppendLine($"{i + 1}. {result.Title}");
                        sb.AppendLine($"   URL: {result.Url}");
                        if (!string.IsNullOrEmpty(result.Snippet))
                        {
                            sb.AppendLine($"   {result.Snippet}");
                        }
                        sb.AppendLine();
                    }

                    return sb.ToString();
                }
                catch (HttpRequestException ex)
                {
                    return $"Search error: {ex.Message}";
                }
                catch (Exception ex)
                {
                    return $"Error performing search: {ex.Message}";
                }
            }
        );
    }

    private static List<SearchResult> ParseDuckDuckGoResults(string html, int maxResults)
    {
        var results = new List<SearchResult>();

        // Parse DuckDuckGo HTML results
        // Results are in <a class="result__a"> tags
        var resultPattern = @"<a[^>]*class=""result__a""[^>]*href=""([^""]*)""[^>]*>([^<]*)</a>";
        var snippetPattern = @"<a[^>]*class=""result__snippet""[^>]*>([^<]*(?:<[^>]*>[^<]*)*)</a>";

        var resultMatches = Regex.Matches(html, resultPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var snippetMatches = Regex.Matches(html, snippetPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        for (int i = 0; i < Math.Min(resultMatches.Count, maxResults); i++)
        {
            var match = resultMatches[i];
            var url = match.Groups[1].Value;
            var title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value.Trim());

            // DuckDuckGo uses redirect URLs, extract actual URL
            if (url.Contains("uddg="))
            {
                var uddgMatch = Regex.Match(url, @"uddg=([^&]+)");
                if (uddgMatch.Success)
                {
                    url = HttpUtility.UrlDecode(uddgMatch.Groups[1].Value);
                }
            }

            var snippet = "";
            if (i < snippetMatches.Count)
            {
                snippet = StripHtml(snippetMatches[i].Groups[1].Value);
                snippet = System.Net.WebUtility.HtmlDecode(snippet).Trim();
            }

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(url))
            {
                results.Add(new SearchResult
                {
                    Title = title,
                    Url = url,
                    Snippet = snippet
                });
            }
        }

        return results;
    }

    private static string StripHtml(string html)
    {
        return Regex.Replace(html, @"<[^>]+>", " ").Trim();
    }

    private class SearchResult
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Snippet { get; set; } = "";
    }
}
