using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hazina.Tools.Services.Web.Abstractions;
using Hazina.Tools.Services.Web.Models;

namespace Hazina.Tools.Services.Web
{
    /// <summary>
    /// Service for FireCrawl MCP web scraping operations.
    /// Provides web scraping, branding extraction, site mapping, and structured data extraction.
    /// </summary>
    public class FireCrawlService : IFireCrawlService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _apiKey;
        private readonly string _baseUrl;
        private readonly Action<string>? _logInfo;
        private readonly Action<Exception, string>? _logError;

        /// <summary>
        /// Creates a new FireCrawlService instance.
        /// </summary>
        /// <param name="config">FireCrawl configuration (API key, base URL)</param>
        /// <param name="httpClient">Optional HttpClient. If null, creates a new one.</param>
        /// <param name="logInfo">Optional info logger</param>
        /// <param name="logError">Optional error logger</param>
        public FireCrawlService(
            FireCrawlConfig config,
            HttpClient? httpClient = null,
            Action<string>? logInfo = null,
            Action<Exception, string>? logError = null)
        {
            _apiKey = config.ApiKey;
            _baseUrl = config.BaseUrl;
            _logInfo = logInfo;
            _logError = logError;

            _httpClient = httpClient ?? new HttpClient();
            if (httpClient == null)
            {
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "Hazina-FireCrawl/1.0");
                _httpClient.Timeout = TimeSpan.FromSeconds(60);
            }

            if (!string.IsNullOrWhiteSpace(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", _apiKey);
            }
        }

        /// <summary>
        /// Scrape a single webpage - pulls content + metadata.
        /// </summary>
        public async Task<FireCrawlScrapeResult> ScrapeAsync(string url, bool includeHtml = false)
        {
            try
            {
                _logInfo?.Invoke($"FireCrawl: Scraping {url}");

                var requestBody = new
                {
                    url,
                    formats = includeHtml ? new[] { "markdown", "html" } : new[] { "markdown" }
                };

                var response = await PostAsync<dynamic>("/scrape", requestBody);

                return new FireCrawlScrapeResult
                {
                    Success = true,
                    Url = url,
                    Markdown = response?.markdown?.ToString(),
                    Html = includeHtml ? response?.html?.ToString() : null,
                    Content = response?.markdown?.ToString(), // Alias for compatibility
                    Metadata = ExtractMetadata(response)
                };
            }
            catch (Exception ex)
            {
                _logError?.Invoke(ex, $"FireCrawl scrape failed for: {url}");
                return new FireCrawlScrapeResult
                {
                    Success = false,
                    Url = url,
                    Error = $"Scrape failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Map website structure - discovers all subpages and internal links.
        /// </summary>
        public async Task<FireCrawlMapResult> MapAsync(string baseUrl, int maxDepth = 2)
        {
            try
            {
                _logInfo?.Invoke($"FireCrawl: Mapping {baseUrl} (depth: {maxDepth})");

                var requestBody = new
                {
                    url = baseUrl,
                    maxDepth
                };

                var response = await PostAsync<dynamic>("/map", requestBody);

                var links = new List<string>();
                if (response?.links != null)
                {
                    foreach (var link in response.links)
                    {
                        links.Add(link.ToString());
                    }
                }

                return new FireCrawlMapResult
                {
                    Success = true,
                    BaseUrl = baseUrl,
                    Links = links,
                    Structure = ExtractStructure(response)
                };
            }
            catch (Exception ex)
            {
                _logError?.Invoke(ex, $"FireCrawl map failed for: {baseUrl}");
                return new FireCrawlMapResult
                {
                    Success = false,
                    BaseUrl = baseUrl,
                    Error = $"Map failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Crawl multiple pages - traverses linked pages and collects content.
        /// </summary>
        public async Task<FireCrawlCrawlResult> CrawlAsync(FireCrawlCrawlRequest request)
        {
            try
            {
                _logInfo?.Invoke($"FireCrawl: Crawling {request.Url} (depth: {request.MaxDepth}, max pages: {request.MaxPages})");

                var requestBody = new
                {
                    url = request.Url,
                    maxDepth = request.MaxDepth,
                    limit = request.MaxPages,
                    screenshot = request.IncludeScreenshots,
                    allowedDomains = request.AllowedDomains
                };

                var response = await PostAsync<dynamic>("/crawl", requestBody);

                var pages = new List<FireCrawlPageResult>();
                if (response?.data != null)
                {
                    foreach (var page in response.data)
                    {
                        pages.Add(new FireCrawlPageResult
                        {
                            Url = page?.url?.ToString() ?? "",
                            Title = page?.title?.ToString(),
                            Content = page?.markdown?.ToString(),
                            Markdown = page?.markdown?.ToString(),
                            Screenshot = page?.screenshot?.ToString(),
                            Metadata = ExtractMetadata(page)
                        });
                    }
                }

                return new FireCrawlCrawlResult
                {
                    Success = true,
                    StartUrl = request.Url,
                    PagesCrawled = pages.Count,
                    Pages = pages
                };
            }
            catch (Exception ex)
            {
                _logError?.Invoke(ex, $"FireCrawl crawl failed for: {request.Url}");
                return new FireCrawlCrawlResult
                {
                    Success = false,
                    StartUrl = request.Url,
                    Error = $"Crawl failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Extract structured data from webpage using AI.
        /// </summary>
        public async Task<FireCrawlExtractResult> ExtractAsync(FireCrawlExtractRequest request)
        {
            try
            {
                _logInfo?.Invoke($"FireCrawl: Extracting data from {request.Url}");

                var requestBody = new
                {
                    url = request.Url,
                    prompt = request.ExtractionPrompt,
                    schema = request.Schema
                };

                var response = await PostAsync<dynamic>("/extract", requestBody);

                return new FireCrawlExtractResult
                {
                    Success = true,
                    Url = request.Url,
                    ExtractedData = ParseExtractedData(response)
                };
            }
            catch (Exception ex)
            {
                _logError?.Invoke(ex, $"FireCrawl extract failed for: {request.Url}");
                return new FireCrawlExtractResult
                {
                    Success = false,
                    Url = request.Url,
                    Error = $"Extract failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Extract branding data specifically (colors, fonts, typography, logo).
        /// </summary>
        public async Task<FireCrawlExtractResult> ExtractBrandingAsync(string url)
        {
            var request = new FireCrawlExtractRequest
            {
                Url = url,
                ExtractionPrompt = @"Extract the brand identity elements from this webpage:
                    - Primary and secondary colors (hex codes)
                    - Font families used (typography)
                    - Logo URL
                    - Brand color palette
                    Return structured JSON with: colors[], fonts[], primaryColor, secondaryColor, logoUrl, typography{}",
                Schema = new Dictionary<string, string>
                {
                    { "colors", "array of hex color codes" },
                    { "fonts", "array of font family names" },
                    { "primaryColor", "hex code of primary brand color" },
                    { "secondaryColor", "hex code of secondary brand color" },
                    { "logoUrl", "URL of company logo image" },
                    { "typography", "object with heading/body font assignments" }
                }
            };

            var result = await ExtractAsync(request);

            if (result.Success && result.ExtractedData.Count > 0)
            {
                result.Branding = ParseBrandingData(result.ExtractedData);
            }

            return result;
        }

        /// <summary>
        /// Capture full-page screenshot of a webpage.
        /// </summary>
        public async Task<FireCrawlScreenshotResult> ScreenshotAsync(FireCrawlScreenshotRequest request)
        {
            try
            {
                _logInfo?.Invoke($"FireCrawl: Capturing screenshot of {request.Url}");

                var requestBody = new
                {
                    url = request.Url,
                    fullPage = request.FullPage,
                    viewport = new
                    {
                        width = request.Width,
                        height = request.Height
                    }
                };

                var response = await PostAsync<dynamic>("/screenshot", requestBody);

                return new FireCrawlScreenshotResult
                {
                    Success = true,
                    Url = request.Url,
                    Screenshot = response?.screenshot?.ToString(),
                    MimeType = "image/png"
                };
            }
            catch (Exception ex)
            {
                _logError?.Invoke(ex, $"FireCrawl screenshot failed for: {request.Url}");
                return new FireCrawlScreenshotResult
                {
                    Success = false,
                    Url = request.Url,
                    Error = $"Screenshot failed: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// Search within a website or domain for specific content.
        /// </summary>
        public async Task<FireCrawlSearchResult> SearchAsync(FireCrawlSearchRequest request)
        {
            try
            {
                _logInfo?.Invoke($"FireCrawl: Searching for '{request.Query}' in {request.Domain ?? "web"}");

                var requestBody = new
                {
                    query = request.Query,
                    domain = request.Domain,
                    limit = request.MaxResults
                };

                var response = await PostAsync<dynamic>("/search", requestBody);

                var results = new List<FireCrawlSearchResultItem>();
                if (response?.results != null)
                {
                    foreach (var item in response.results)
                    {
                        results.Add(new FireCrawlSearchResultItem
                        {
                            Url = item?.url?.ToString() ?? "",
                            Title = item?.title?.ToString(),
                            Snippet = item?.snippet?.ToString(),
                            Relevance = item?.relevance != null ? (double)item.relevance : 0.0
                        });
                    }
                }

                return new FireCrawlSearchResult
                {
                    Success = true,
                    Query = request.Query,
                    ResultCount = results.Count,
                    Results = results
                };
            }
            catch (Exception ex)
            {
                _logError?.Invoke(ex, $"FireCrawl search failed for: {request.Query}");
                return new FireCrawlSearchResult
                {
                    Success = false,
                    Query = request.Query,
                    Error = $"Search failed: {ex.Message}"
                };
            }
        }

        // Private helper methods

        private async Task<T?> PostAsync<T>(string endpoint, object requestBody)
        {
            var url = $"{_baseUrl.TrimEnd('/')}{endpoint}";
            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(responseJson);
        }

        private static Dictionary<string, object> ExtractMetadata(dynamic? response)
        {
            var metadata = new Dictionary<string, object>();

            if (response == null) return metadata;

            try
            {
                if (response.metadata != null)
                {
                    var metadataJson = response.metadata.ToString();
                    var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(metadataJson);
                    if (parsed != null)
                    {
                        foreach (var kvp in parsed)
                        {
                            metadata[kvp.Key] = kvp.Value.ToString();
                        }
                    }
                }
            }
            catch
            {
                // Ignore metadata extraction errors
            }

            return metadata;
        }

        private static Dictionary<string, List<string>> ExtractStructure(dynamic? response)
        {
            var structure = new Dictionary<string, List<string>>();

            if (response?.structure == null) return structure;

            try
            {
                var structureJson = response.structure.ToString();
                structure = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(structureJson)
                    ?? new Dictionary<string, List<string>>();
            }
            catch
            {
                // Ignore structure extraction errors
            }

            return structure;
        }

        private static Dictionary<string, object> ParseExtractedData(dynamic? response)
        {
            var data = new Dictionary<string, object>();

            if (response == null) return data;

            try
            {
                var dataJson = response.ToString();
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(dataJson);
                if (parsed != null)
                {
                    foreach (var kvp in parsed)
                    {
                        data[kvp.Key] = kvp.Value.ToString();
                    }
                }
            }
            catch
            {
                // Ignore extraction errors
            }

            return data;
        }

        private static BrandingData ParseBrandingData(Dictionary<string, object> extractedData)
        {
            var branding = new BrandingData();

            try
            {
                if (extractedData.TryGetValue("colors", out var colors))
                {
                    var colorList = JsonSerializer.Deserialize<List<string>>(colors.ToString() ?? "[]");
                    if (colorList != null) branding.Colors = colorList;
                }

                if (extractedData.TryGetValue("fonts", out var fonts))
                {
                    var fontList = JsonSerializer.Deserialize<List<string>>(fonts.ToString() ?? "[]");
                    if (fontList != null) branding.Fonts = fontList;
                }

                if (extractedData.TryGetValue("primaryColor", out var primary))
                {
                    branding.PrimaryColor = primary.ToString();
                }

                if (extractedData.TryGetValue("secondaryColor", out var secondary))
                {
                    branding.SecondaryColor = secondary.ToString();
                }

                if (extractedData.TryGetValue("logoUrl", out var logo))
                {
                    branding.LogoUrl = logo.ToString();
                }

                if (extractedData.TryGetValue("typography", out var typography))
                {
                    var typographyDict = JsonSerializer.Deserialize<Dictionary<string, string>>(typography.ToString() ?? "{}");
                    if (typographyDict != null) branding.Typography = typographyDict;
                }
            }
            catch
            {
                // Ignore parsing errors, return partial data
            }

            return branding;
        }
    }
}
