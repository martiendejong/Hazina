using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;
using Hazina.Tools.Services.Social.Services;
using Hazina.Tools.Services.Chat.Tools;

namespace Hazina.Tools.Services.Social.Tools;

/// <summary>
/// Tool for querying imported social content (posts, articles).
/// Provides search and filtering capabilities for AI agents.
/// </summary>
public class SocialQueryTool
{
    private readonly ILogger<SocialQueryTool> _logger;
    private readonly ISocialContentStore _contentStore;
    private readonly SocialEmbeddingService? _embeddingService;

    public SocialQueryTool(
        ILogger<SocialQueryTool> logger,
        ISocialContentStore contentStore,
        SocialEmbeddingService? embeddingService = null)
    {
        _logger = logger;
        _contentStore = contentStore;
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Gets the tool definition for LLM function calling.
    /// </summary>
    public static IToolDefinition GetToolDefinition()
    {
        return new ToolDefinition
        {
            Name = "query_social_content",
            Description = "Search and query imported social media content including LinkedIn posts and articles. Use this to find user's past content, analyze content patterns, or retrieve specific posts.",
            Parameters = JsonSerializer.Deserialize<JsonElement>(ToolSchema)
        };
    }

    /// <summary>
    /// Executes a social content query.
    /// </summary>
    public async Task<IToolResult> ExecuteAsync(
        string argumentsJson,
        string projectId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<QueryArgs>(argumentsJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (args == null)
            {
                return new ToolResult { Success = false, Error = "Invalid query arguments" };
            }

            _logger.LogInformation("Executing social content query for project {ProjectId}: {QueryType}",
                projectId, args.QueryType);

            return args.QueryType switch
            {
                "text_search" => await ExecuteTextSearchAsync(projectId, args, cancellationToken),
                "semantic_search" => await ExecuteSemanticSearchAsync(projectId, args, cancellationToken),
                "list" => await ExecuteListAsync(projectId, args, cancellationToken),
                "stats" => await ExecuteStatsAsync(projectId, args, cancellationToken),
                _ => new ToolResult { Success = false, Error = $"Unknown query type: {args.QueryType}" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing social content query");
            return new ToolResult { Success = false, Error = $"Query failed: {ex.Message}" };
        }
    }

    private async Task<IToolResult> ExecuteTextSearchAsync(
        string projectId,
        QueryArgs args,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(args.Query))
        {
            return new ToolResult { Success = false, Error = "Query text is required for text_search" };
        }

        var options = new SearchOptions
        {
            Limit = args.Limit,
            SemanticSearch = false,
            ContentTypes = args.ContentTypes ?? new HashSet<string> { "posts", "articles" }
        };

        var results = await _contentStore.SearchAsync(
            projectId,
            args.Query,
            options,
            cancellationToken);

        var formatted = results.Select(r => FormatSearchResult(r)).ToList();

        return new ToolResult
        {
            Success = true,
            Result = new
            {
                query = args.Query,
                resultCount = formatted.Count,
                results = formatted
            },
            TokensUsed = EstimateTokens(formatted)
        };
    }

    private async Task<IToolResult> ExecuteSemanticSearchAsync(
        string projectId,
        QueryArgs args,
        CancellationToken cancellationToken)
    {
        if (_embeddingService == null)
        {
            return new ToolResult { Success = false, Error = "Semantic search is not available" };
        }

        if (string.IsNullOrEmpty(args.Query))
        {
            return new ToolResult { Success = false, Error = "Query text is required for semantic_search" };
        }

        var options = new SemanticSearchOptions
        {
            MaxResults = args.Limit,
            MinSimilarity = args.MinSimilarity ?? 0.5,
            ContentTypes = args.ContentTypes ?? new HashSet<string> { "posts", "articles" }
        };

        var results = await _embeddingService.SemanticSearchAsync(
            projectId,
            args.Query,
            options,
            cancellationToken);

        var formatted = results.Select(r => new
        {
            type = r.ContentType,
            id = r.Id,
            text = r.Text,
            snippet = r.Snippet,
            createdAt = r.CreatedAt,
            url = r.Url,
            score = r.Score
        }).ToList();

        return new ToolResult
        {
            Success = true,
            Result = new
            {
                query = args.Query,
                searchType = "semantic",
                resultCount = formatted.Count,
                results = formatted
            },
            TokensUsed = EstimateTokens(formatted)
        };
    }

    private async Task<IToolResult> ExecuteListAsync(
        string projectId,
        QueryArgs args,
        CancellationToken cancellationToken)
    {
        var results = new List<object>();

        if (args.ContentTypes == null || args.ContentTypes.Contains("posts"))
        {
            var posts = await _contentStore.GetPostsAsync(
                projectId,
                accountId: args.AccountId,
                limit: args.Limit,
                cancellationToken: cancellationToken);

            results.AddRange(posts.Select(p => new
            {
                type = "post",
                id = p.Id,
                accountId = p.AccountId,
                content = TruncateText(p.Content, 300),
                createdAt = p.CreatedAt,
                likes = p.LikeCount,
                comments = p.CommentCount,
                shares = p.ShareCount,
                url = p.Url
            }));
        }

        if (args.ContentTypes == null || args.ContentTypes.Contains("articles"))
        {
            var articles = await _contentStore.GetArticlesAsync(
                projectId,
                accountId: args.AccountId,
                limit: args.Limit,
                cancellationToken: cancellationToken);

            results.AddRange(articles.Select(a => new
            {
                type = "article",
                id = a.Id,
                accountId = a.AccountId,
                title = a.Title,
                summary = TruncateText(a.Summary ?? a.Content, 300),
                createdAt = a.CreatedAt,
                views = a.ViewCount,
                url = a.Url
            }));
        }

        // Sort by date descending
        var sorted = results
            .OrderByDescending(r => ((dynamic)r).createdAt)
            .Take(args.Limit)
            .ToList();

        return new ToolResult
        {
            Success = true,
            Result = new
            {
                totalCount = sorted.Count,
                items = sorted
            },
            TokensUsed = EstimateTokens(sorted)
        };
    }

    private async Task<IToolResult> ExecuteStatsAsync(
        string projectId,
        QueryArgs args,
        CancellationToken cancellationToken)
    {
        var posts = await _contentStore.GetPostsAsync(projectId, limit: 10000, cancellationToken: cancellationToken);
        var articles = await _contentStore.GetArticlesAsync(projectId, limit: 10000, cancellationToken: cancellationToken);

        var totalPosts = posts.Count;
        var totalArticles = articles.Count;

        var postStats = new
        {
            count = totalPosts,
            totalLikes = posts.Sum(p => p.LikeCount),
            totalComments = posts.Sum(p => p.CommentCount),
            totalShares = posts.Sum(p => p.ShareCount),
            avgLikes = totalPosts > 0 ? posts.Average(p => p.LikeCount) : 0,
            avgComments = totalPosts > 0 ? posts.Average(p => p.CommentCount) : 0,
            avgShares = totalPosts > 0 ? posts.Average(p => p.ShareCount) : 0
        };

        var articleStats = new
        {
            count = totalArticles,
            totalViews = articles.Sum(a => a.ViewCount),
            avgViews = totalArticles > 0 ? articles.Average(a => a.ViewCount) : 0
        };

        // Content patterns
        var postsByMonth = posts
            .GroupBy(p => new { p.CreatedAt.Year, p.CreatedAt.Month })
            .Select(g => new { year = g.Key.Year, month = g.Key.Month, count = g.Count() })
            .OrderByDescending(x => x.year).ThenByDescending(x => x.month)
            .Take(12)
            .ToList();

        return new ToolResult
        {
            Success = true,
            Result = new
            {
                summary = new
                {
                    totalPosts,
                    totalArticles,
                    totalContent = totalPosts + totalArticles
                },
                posts = postStats,
                articles = articleStats,
                postsByMonth
            },
            TokensUsed = 200 // Fixed estimate for stats
        };
    }

    private static object FormatSearchResult(SocialContentSearchResult result)
    {
        return new
        {
            type = result.ContentType,
            id = result.Id,
            text = result.Text,
            snippet = result.Snippet,
            createdAt = result.CreatedAt,
            url = result.Url,
            score = result.Score
        };
    }

    private static string TruncateText(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Length <= maxLength) return text;
        return text.Substring(0, maxLength) + "...";
    }

    private static int EstimateTokens<T>(List<T> items)
    {
        // Rough estimate based on serialized size
        var json = JsonSerializer.Serialize(items);
        return json.Length / 4;
    }

    private const string ToolSchema = @"{
        ""type"": ""object"",
        ""properties"": {
            ""query_type"": {
                ""type"": ""string"",
                ""enum"": [""text_search"", ""semantic_search"", ""list"", ""stats""],
                ""description"": ""Type of query: text_search for keyword search, semantic_search for meaning-based search, list for browsing content, stats for aggregate statistics""
            },
            ""query"": {
                ""type"": ""string"",
                ""description"": ""Search query text (required for text_search and semantic_search)""
            },
            ""content_types"": {
                ""type"": ""array"",
                ""items"": {
                    ""type"": ""string"",
                    ""enum"": [""posts"", ""articles""]
                },
                ""description"": ""Filter by content type. Omit to include all types.""
            },
            ""account_id"": {
                ""type"": ""string"",
                ""description"": ""Filter by specific connected account ID""
            },
            ""limit"": {
                ""type"": ""integer"",
                ""default"": 20,
                ""maximum"": 100,
                ""description"": ""Maximum number of results to return""
            },
            ""min_similarity"": {
                ""type"": ""number"",
                ""default"": 0.5,
                ""minimum"": 0.0,
                ""maximum"": 1.0,
                ""description"": ""Minimum similarity threshold for semantic search (0.0 to 1.0)""
            }
        },
        ""required"": [""query_type""]
    }";

    private class QueryArgs
    {
        [JsonPropertyName("query_type")]
        public string QueryType { get; set; } = "list";

        [JsonPropertyName("query")]
        public string? Query { get; set; }

        [JsonPropertyName("content_types")]
        public HashSet<string>? ContentTypes { get; set; }

        [JsonPropertyName("account_id")]
        public string? AccountId { get; set; }

        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 20;

        [JsonPropertyName("min_similarity")]
        public double? MinSimilarity { get; set; }
    }
}
