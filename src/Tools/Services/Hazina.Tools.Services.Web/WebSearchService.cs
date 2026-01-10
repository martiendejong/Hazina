using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Hazina.Tools.Services.Web.Abstractions;
using Hazina.Tools.Services.Web.Models;

namespace Hazina.Tools.Services.Web
{
    /// <summary>
    /// Service for performing web searches using DuckDuckGo or Bing.
    /// Generic implementation suitable for any .NET application.
    /// </summary>
    public class WebSearchService : IWebSearchService
    {
        private readonly HttpClient _webClient;
        private readonly string? _bingApiKey;
        private readonly Action<string>? _logInfo;
        private readonly Action<Exception, string>? _logError;

        /// <summary>
        /// Creates a new WebSearchService instance.
        /// </summary>
        /// <param name="bingApiKey">Optional Bing Search API key. If null, uses DuckDuckGo fallback.</param>
        /// <param name="httpClient">Optional HttpClient. If null, creates a new one.</param>
        /// <param name="logInfo">Optional info logger</param>
        /// <param name="logError">Optional error logger</param>
        public WebSearchService(
            string? bingApiKey = null,
            HttpClient? httpClient = null,
            Action<string>? logInfo = null,
            Action<Exception, string>? logError = null)
        {
            _bingApiKey = bingApiKey;
            _logInfo = logInfo;
            _logError = logError;

            _webClient = httpClient ?? new HttpClient();
            if (httpClient == null)
            {
                _webClient.DefaultRequestHeaders.Add("User-Agent",
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                _webClient.Timeout = TimeSpan.FromSeconds(30);
            }
        }

        /// <summary>
        /// Search the web for information.
        /// Uses Bing API if key is configured, otherwise falls back to DuckDuckGo HTML scraping.
        /// </summary>
        public async Task<WebSearchResult> SearchAsync(string query, int count = 5)
        {
            try
            {
                count = Math.Max(1, Math.Min(count, 10)); // Limit 1-10 results

                // Try Bing Search API if key is available
                if (!string.IsNullOrWhiteSpace(_bingApiKey))
                {
                    _logInfo?.Invoke($"Performing web search with Bing API: {query}");
                    return await SearchWithBingAsync(query, count);
                }

                // Fallback to DuckDuckGo HTML scraping (no API key needed)
                _logInfo?.Invoke($"Performing web search with DuckDuckGo: {query}");
                return await SearchWithDuckDuckGoAsync(query, count);
            }
            catch (Exception ex)
            {
                _logError?.Invoke(ex, $"Web search failed for query: {query}");
                return new WebSearchResult
                {
                    Success = false,
                    Error = $"Web search failed: {ex.Message}",
                    Results = new List<WebSearchResultItem>()
                };
            }
        }

        /// <summary>
        /// Fetch and read the content of a web page.
        /// </summary>
        public async Task<WebPageContent> FetchUrlAsync(string url, int maxChars = 15000)
        {
            try
            {
                _logInfo?.Invoke($"Fetching URL: {url}");

                var response = await _webClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();

                // Convert HTML to plain text (basic extraction)
                var text = ConvertHtmlToText(html);

                if (text.Length > maxChars)
                {
                    text = text.Substring(0, maxChars) + $"\n\n[... truncated, {text.Length - maxChars} more characters ...]";
                }

                return new WebPageContent
                {
                    Success = true,
                    Url = url,
                    Content = text
                };
            }
            catch (Exception ex)
            {
                _logError?.Invoke(ex, $"Failed to fetch URL: {url}");
                return new WebPageContent
                {
                    Success = false,
                    Url = url,
                    Error = $"Failed to fetch URL: {ex.Message}"
                };
            }
        }

        private async Task<WebSearchResult> SearchWithBingAsync(string query, int count)
        {
            var url = $"https://api.bing.microsoft.com/v7.0/search?q={Uri.EscapeDataString(query)}&count={count}";

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Ocp-Apim-Subscription-Key", _bingApiKey);

            var response = await _webClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            var results = new List<WebSearchResultItem>();

            if (doc.RootElement.TryGetProperty("webPages", out var webPages) &&
                webPages.TryGetProperty("value", out var resultElements))
            {
                int i = 0;
                foreach (var result in resultElements.EnumerateArray())
                {
                    if (i >= count) break;
                    i++;

                    results.Add(new WebSearchResultItem
                    {
                        Title = result.GetProperty("name").GetString() ?? "",
                        Url = result.GetProperty("url").GetString() ?? "",
                        Snippet = result.GetProperty("snippet").GetString() ?? ""
                    });
                }
            }

            return new WebSearchResult
            {
                Success = true,
                Query = query,
                ResultCount = results.Count,
                Results = results,
                Source = "Bing"
            };
        }

        private async Task<WebSearchResult> SearchWithDuckDuckGoAsync(string query, int count)
        {
            // Use DuckDuckGo HTML page and extract results
            var url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";

            var response = await _webClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            var results = new List<WebSearchResultItem>();

            // Parse results from DuckDuckGo HTML
            // Results are in <a class="result__a" href="...">title</a>
            // Snippets are in <a class="result__snippet">...</a>
            var resultPattern = new Regex(
                @"<a[^>]*class=""result__a""[^>]*href=""([^""]*)""[^>]*>([^<]*)</a>.*?<a[^>]*class=""result__snippet""[^>]*>([^<]*)</a>",
                RegexOptions.Singleline | RegexOptions.IgnoreCase);

            var matches = resultPattern.Matches(html);
            int i = 0;

            foreach (Match match in matches)
            {
                if (i >= count) break;
                i++;

                var resultUrl = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
                var title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value);
                var snippet = System.Net.WebUtility.HtmlDecode(match.Groups[3].Value);

                // DuckDuckGo uses redirect URLs, extract the actual URL
                if (resultUrl.Contains("uddg="))
                {
                    var uddgMatch = Regex.Match(resultUrl, @"uddg=([^&]+)");
                    if (uddgMatch.Success)
                    {
                        resultUrl = Uri.UnescapeDataString(uddgMatch.Groups[1].Value);
                    }
                }

                results.Add(new WebSearchResultItem
                {
                    Title = title,
                    Url = resultUrl,
                    Snippet = snippet
                });
            }

            if (results.Count == 0)
            {
                // Try simpler pattern if the complex one didn't work
                var simplePattern = new Regex(@"<a[^>]*class=""result__url""[^>]*href=""([^""]*)"">([^<]*)</a>", RegexOptions.IgnoreCase);
                var simpleMatches = simplePattern.Matches(html);

                foreach (Match match in simpleMatches)
                {
                    if (i >= count) break;
                    i++;

                    var resultUrl = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
                    var title = System.Net.WebUtility.HtmlDecode(match.Groups[2].Value);

                    results.Add(new WebSearchResultItem
                    {
                        Title = title,
                        Url = resultUrl,
                        Snippet = ""
                    });
                }
            }

            return new WebSearchResult
            {
                Success = results.Count > 0,
                Query = query,
                ResultCount = results.Count,
                Results = results,
                Source = "DuckDuckGo",
                Error = results.Count == 0 ? "No results found. DuckDuckGo may have changed their HTML structure." : null
            };
        }

        private static string ConvertHtmlToText(string html)
        {
            // Remove script and style elements
            html = Regex.Replace(html, @"<script[^>]*>[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<style[^>]*>[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<head[^>]*>[\s\S]*?</head>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<nav[^>]*>[\s\S]*?</nav>", "", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<footer[^>]*>[\s\S]*?</footer>", "", RegexOptions.IgnoreCase);

            // Replace common block elements with newlines
            html = Regex.Replace(html, @"<(p|div|br|li|h[1-6])[^>]*>", "\n", RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"</(p|div|li|h[1-6])>", "\n", RegexOptions.IgnoreCase);

            // Remove all remaining HTML tags
            html = Regex.Replace(html, @"<[^>]+>", " ");

            // Decode HTML entities
            html = System.Net.WebUtility.HtmlDecode(html);

            // Clean up whitespace
            html = Regex.Replace(html, @"[ \t]+", " ");
            html = Regex.Replace(html, @"\n[ \t]+", "\n");
            html = Regex.Replace(html, @"[ \t]+\n", "\n");
            html = Regex.Replace(html, @"\n{3,}", "\n\n");

            return html.Trim();
        }
    }
}
