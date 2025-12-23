using System.Text;
using System.Text.Json;

namespace DevGPT.DynamicAPI.Tools;

/// <summary>
/// DevGPTChatTool wrapper for web search functionality.
/// Allows agents to search for API documentation and technical information.
/// </summary>
public class WebSearchDevGPTTool : DevGPTChatTool
{
    private readonly WebSearchTool _searchTool;

    public WebSearchDevGPTTool(WebSearchTool searchTool) : base(
        "web_search",
        "Search the web for information, API documentation, code examples, or technical details. " +
        "Use this to find documentation for APIs you want to call, or to learn about services and their endpoints.",
        new List<ChatToolParameter>
        {
            new ChatToolParameter
            {
                Name = "query",
                Description = "The search query. Be specific about what you're looking for (e.g., 'Stripe API create payment intent', 'Google Analytics API authentication')",
                Type = "string",
                Required = true
            },
            new ChatToolParameter
            {
                Name = "count",
                Description = "Number of search results to return (1-50, default: 5)",
                Type = "number",
                Required = false
            }
        },
        async (messages, call, cancel) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(call.FunctionArguments);
                var root = doc.RootElement;

                if (!root.TryGetProperty("query", out var queryProp))
                {
                    return "Error: 'query' parameter is required";
                }

                var query = queryProp.GetString();
                if (string.IsNullOrEmpty(query))
                {
                    return "Error: query cannot be empty";
                }

                var count = root.TryGetProperty("count", out var countProp)
                    ? countProp.GetInt32()
                    : 5;

                count = Math.Max(1, Math.Min(count, 50));

                var result = await searchTool.Search(query, count);

                if (result.Results.Count == 0)
                {
                    return $"No results found for query: {query}";
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Search Results for: {query}");
                sb.AppendLine($"Found {result.Results.Count} results:\n");

                for (int i = 0; i < result.Results.Count; i++)
                {
                    var item = result.Results[i];
                    sb.AppendLine($"{i + 1}. {item.Title}");
                    sb.AppendLine($"   URL: {item.Url}");
                    sb.AppendLine($"   {item.Snippet}");
                    sb.AppendLine();
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                return $"Web search failed: {ex.Message}";
            }
        }
    )
    {
        _searchTool = searchTool;
    }
}

/// <summary>
/// DevGPTChatTool for fetching content from URLs found in search results
/// </summary>
public class FetchUrlDevGPTTool : DevGPTChatTool
{
    private readonly WebSearchTool _searchTool;

    public FetchUrlDevGPTTool(WebSearchTool searchTool) : base(
        "fetch_url",
        "Fetch and read the full content of a web page. Use this after web_search to get detailed documentation from specific URLs.",
        new List<ChatToolParameter>
        {
            new ChatToolParameter
            {
                Name = "url",
                Description = "The URL to fetch",
                Type = "string",
                Required = true
            }
        },
        async (messages, call, cancel) =>
        {
            try
            {
                using var doc = JsonDocument.Parse(call.FunctionArguments);
                var root = doc.RootElement;

                if (!root.TryGetProperty("url", out var urlProp))
                {
                    return "Error: 'url' parameter is required";
                }

                var url = urlProp.GetString();
                if (string.IsNullOrEmpty(url))
                {
                    return "Error: url cannot be empty";
                }

                var content = await searchTool.FetchUrl(url);

                // Limit content size to avoid context overflow
                const int maxLength = 50000;
                if (content.Length > maxLength)
                {
                    content = content.Substring(0, maxLength) + "\n\n[Content truncated due to length]";
                }

                return $"Content from {url}:\n\n{content}";
            }
            catch (Exception ex)
            {
                return $"Failed to fetch URL: {ex.Message}";
            }
        }
    )
    {
        _searchTool = searchTool;
    }
}
