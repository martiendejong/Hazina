using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Hazina.Tools.Models;
using Hazina.Tools.Services.Store;
using Hazina.Tools.Services.Web.Abstractions;

namespace Hazina.Tools.Services.Store.Extensions
{
    /// <summary>
    /// Adds web search tools to the chat context.
    /// Allows the AI to search the internet and fetch web pages for additional context.
    /// </summary>
    public static class ToolsContextWebSearchExtensions
    {
        /// <summary>
        /// Adds web search tools for gathering online information during chat.
        /// </summary>
        /// <param name="context">The tools context to extend</param>
        /// <param name="searchService">The web search service instance</param>
        /// <returns>The extended tools context</returns>
        public static StoreToolsContext AddWebSearchTools(
            this StoreToolsContext context,
            IWebSearchService searchService)
        {
            if (searchService == null)
            {
                Console.WriteLine("[WebSearchTools] WARNING: No search service provided, web search tools disabled");
                return context;
            }

            // Tool 1: web_search
            var webSearchTool = new HazinaChatTool(
                "web_search",
                "Search the internet for information. Use this to find current data, verify facts, research topics, or gather external context. Returns a list of search results with titles, URLs, and snippets.",
                new List<ChatToolParameter>
                {
                    new ChatToolParameter
                    {
                        Name = "query",
                        Description = "The search query. Be specific about what you're looking for. Examples: 'latest social media trends 2026', 'best practices for Instagram marketing', 'how to write compelling brand stories'",
                        Type = "string",
                        Required = true
                    },
                    new ChatToolParameter
                    {
                        Name = "count",
                        Description = "Number of results to return (1-10, default: 5)",
                        Type = "number",
                        Required = false
                    }
                },
                async (messages, toolCall, cancel) =>
                {
                    try
                    {
                        using var args = JsonDocument.Parse(toolCall.FunctionArguments);

                        var query = GetStringProperty(args, "query");
                        if (string.IsNullOrWhiteSpace(query))
                            return JsonError("query parameter is required");

                        var count = GetIntProperty(args, "count", 5);
                        count = Math.Max(1, Math.Min(count, 10)); // Clamp to 1-10

                        var result = await searchService.SearchAsync(query, count);

                        if (!result.Success)
                        {
                            return JsonError(result.Error ?? "Web search failed");
                        }

                        if (result.Results.Count == 0)
                        {
                            return JsonSerializer.Serialize(new
                            {
                                success = true,
                                query = result.Query,
                                result_count = 0,
                                message = "No results found for this query. Try a different search term.",
                                results = new List<object>()
                            });
                        }

                        // Format results for the LLM
                        var sb = new StringBuilder();
                        sb.AppendLine($"Web Search Results for: {query}");
                        sb.AppendLine($"Source: {result.Source}");
                        sb.AppendLine($"Found {result.ResultCount} results:");
                        sb.AppendLine();

                        int i = 0;
                        foreach (var item in result.Results)
                        {
                            i++;
                            sb.AppendLine($"{i}. {item.Title}");
                            sb.AppendLine($"   URL: {item.Url}");
                            if (!string.IsNullOrWhiteSpace(item.Snippet))
                            {
                                sb.AppendLine($"   {item.Snippet}");
                            }
                            sb.AppendLine();
                        }

                        sb.AppendLine("💡 Tip: Use fetch_url to read full content of relevant pages.");

                        return JsonSerializer.Serialize(new
                        {
                            success = true,
                            query = result.Query,
                            result_count = result.ResultCount,
                            source = result.Source,
                            message = sb.ToString(),
                            results = result.Results.Select(r => new
                            {
                                title = r.Title,
                                url = r.Url,
                                snippet = r.Snippet
                            }).ToList()
                        });
                    }
                    catch (Exception ex)
                    {
                        return JsonError($"Error performing web search: {ex.Message}");
                    }
                });

            context.Tools.Add(webSearchTool);

            // Tool 2: fetch_url
            var fetchUrlTool = new HazinaChatTool(
                "fetch_url",
                "Fetch and read the full content of a web page. Use this after web_search to get detailed information from specific URLs. The HTML will be converted to plain text for easy reading.",
                new List<ChatToolParameter>
                {
                    new ChatToolParameter
                    {
                        Name = "url",
                        Description = "The URL to fetch. Must be a valid http:// or https:// URL.",
                        Type = "string",
                        Required = true
                    },
                    new ChatToolParameter
                    {
                        Name = "max_chars",
                        Description = "Maximum characters to return (default: 15000). The content will be truncated if longer.",
                        Type = "number",
                        Required = false
                    }
                },
                async (messages, toolCall, cancel) =>
                {
                    try
                    {
                        using var args = JsonDocument.Parse(toolCall.FunctionArguments);

                        var url = GetStringProperty(args, "url");
                        if (string.IsNullOrWhiteSpace(url))
                            return JsonError("url parameter is required");

                        // Validate URL
                        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                        {
                            return JsonError("Invalid URL. Must be a valid http:// or https:// URL.");
                        }

                        var maxChars = GetIntProperty(args, "max_chars", 15000);
                        maxChars = Math.Max(1000, Math.Min(maxChars, 50000)); // Clamp to 1000-50000

                        var result = await searchService.FetchUrlAsync(url, maxChars);

                        if (!result.Success)
                        {
                            return JsonError(result.Error ?? "Failed to fetch URL");
                        }

                        return JsonSerializer.Serialize(new
                        {
                            success = true,
                            url = result.Url,
                            content_length = result.Content?.Length ?? 0,
                            content = result.Content
                        });
                    }
                    catch (Exception ex)
                    {
                        return JsonError($"Error fetching URL: {ex.Message}");
                    }
                });

            context.Tools.Add(fetchUrlTool);

            Console.WriteLine("[WebSearchTools] Added 2 web search tools to context (web_search, fetch_url)");

            return context;
        }

        private static string? GetStringProperty(JsonDocument doc, string name)
        {
            if (doc.RootElement.TryGetProperty(name, out var elem) &&
                elem.ValueKind == JsonValueKind.String)
            {
                return elem.GetString();
            }
            return null;
        }

        private static int GetIntProperty(JsonDocument doc, string name, int defaultValue)
        {
            if (doc.RootElement.TryGetProperty(name, out var elem))
            {
                if (elem.ValueKind == JsonValueKind.Number)
                {
                    return elem.GetInt32();
                }
                if (elem.ValueKind == JsonValueKind.String && int.TryParse(elem.GetString(), out var val))
                {
                    return val;
                }
            }
            return defaultValue;
        }

        private static string JsonError(string message)
        {
            return JsonSerializer.Serialize(new { success = false, error = message });
        }
    }
}
