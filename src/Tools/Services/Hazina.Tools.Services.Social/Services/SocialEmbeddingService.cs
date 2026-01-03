using Microsoft.Extensions.Logging;
using Hazina.Store.EmbeddingStore;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Services;

/// <summary>
/// Integrates embedding generation and semantic search with social content.
/// </summary>
public class SocialEmbeddingService
{
    private readonly ILogger<SocialEmbeddingService> _logger;
    private readonly ISocialContentStore _contentStore;
    private readonly Func<string, EmbeddingService> _embeddingServiceFactory;
    private readonly Func<string, IVectorSearchStore> _searchStoreFactory;

    public SocialEmbeddingService(
        ILogger<SocialEmbeddingService> logger,
        ISocialContentStore contentStore,
        Func<string, EmbeddingService> embeddingServiceFactory,
        Func<string, IVectorSearchStore> searchStoreFactory)
    {
        _logger = logger;
        _contentStore = contentStore;
        _embeddingServiceFactory = embeddingServiceFactory;
        _searchStoreFactory = searchStoreFactory;
    }

    /// <summary>
    /// Generates embeddings for newly imported posts.
    /// </summary>
    public async Task EmbedPostsAsync(
        string projectId,
        IEnumerable<SocialPost> posts,
        CancellationToken cancellationToken = default)
    {
        var embeddingService = _embeddingServiceFactory(projectId);
        var items = posts
            .Where(p => !string.IsNullOrEmpty(p.Content))
            .Select(p => ($"social:post:{p.Id}", p.Content));

        var count = await embeddingService.StoreBatchAsync(items, cancellationToken);
        _logger.LogInformation("Generated {Count} embeddings for posts in project {ProjectId}", count, projectId);
    }

    /// <summary>
    /// Generates embeddings for newly imported articles.
    /// </summary>
    public async Task EmbedArticlesAsync(
        string projectId,
        IEnumerable<SocialArticle> articles,
        CancellationToken cancellationToken = default)
    {
        var embeddingService = _embeddingServiceFactory(projectId);

        // For articles, embed title + summary + content for better search
        var items = articles
            .Where(a => !string.IsNullOrEmpty(a.Title) || !string.IsNullOrEmpty(a.Content))
            .Select(a =>
            {
                var text = $"{a.Title}\n{a.Summary}\n{a.Content}".Trim();
                return ($"social:article:{a.Id}", text);
            });

        var count = await embeddingService.StoreBatchAsync(items, cancellationToken);
        _logger.LogInformation("Generated {Count} embeddings for articles in project {ProjectId}", count, projectId);
    }

    /// <summary>
    /// Performs semantic search over social content.
    /// </summary>
    public async Task<List<SocialContentSearchResult>> SemanticSearchAsync(
        string projectId,
        string query,
        SemanticSearchOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SemanticSearchOptions();

        var embeddingService = _embeddingServiceFactory(projectId);
        var searchStore = _searchStoreFactory(projectId);

        // Generate query embedding
        var queryEmbedding = await embeddingService.GenerateQueryEmbeddingAsync(query, cancellationToken);

        // Search for similar embeddings
        var scoredResults = await searchStore.SearchSimilarAsync(
            queryEmbedding,
            options.MaxResults,
            options.MinSimilarity,
            cancellationToken);

        // Map scored embeddings to social content
        var results = new List<SocialContentSearchResult>();
        foreach (var scored in scoredResults)
        {
            var key = scored.Info.Key;
            if (key.StartsWith("social:post:"))
            {
                var postId = key.Substring("social:post:".Length);
                var posts = await _contentStore.GetPostsAsync(projectId, limit: 1, cancellationToken: cancellationToken);
                var post = posts.FirstOrDefault(p => p.Id == postId);
                if (post != null)
                {
                    results.Add(new SocialContentSearchResult
                    {
                        ContentType = "post",
                        Id = post.Id,
                        AccountId = post.AccountId,
                        Text = post.Content,
                        Snippet = TruncateWithHighlight(post.Content, query, 200),
                        CreatedAt = post.CreatedAt,
                        Url = post.Url,
                        Score = (float)scored.Similarity
                    });
                }
            }
            else if (key.StartsWith("social:article:"))
            {
                var articleId = key.Substring("social:article:".Length);
                var articles = await _contentStore.GetArticlesAsync(projectId, limit: 1, cancellationToken: cancellationToken);
                var article = articles.FirstOrDefault(a => a.Id == articleId);
                if (article != null)
                {
                    results.Add(new SocialContentSearchResult
                    {
                        ContentType = "article",
                        Id = article.Id,
                        AccountId = article.AccountId,
                        Text = article.Title,
                        Snippet = TruncateWithHighlight(article.Content, query, 200),
                        CreatedAt = article.CreatedAt,
                        Url = article.Url,
                        Score = (float)scored.Similarity
                    });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Removes embeddings for deleted content.
    /// </summary>
    public async Task RemoveEmbeddingsForAccountAsync(
        string projectId,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        var embeddingService = _embeddingServiceFactory(projectId);

        // Get all content for the account and remove embeddings
        var posts = await _contentStore.GetPostsAsync(projectId, accountId, limit: 10000, cancellationToken: cancellationToken);
        foreach (var post in posts)
        {
            await embeddingService.RemoveAsync($"social:post:{post.Id}", cancellationToken);
        }

        var articles = await _contentStore.GetArticlesAsync(projectId, accountId, limit: 10000, cancellationToken: cancellationToken);
        foreach (var article in articles)
        {
            await embeddingService.RemoveAsync($"social:article:{article.Id}", cancellationToken);
        }

        _logger.LogInformation("Removed embeddings for account {AccountId} in project {ProjectId}",
            accountId, projectId);
    }

    private static string TruncateWithHighlight(string text, string query, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Find query occurrence
        var queryIndex = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        int startIndex;

        if (queryIndex >= 0)
        {
            // Center around the query
            startIndex = Math.Max(0, queryIndex - (maxLength / 4));
        }
        else
        {
            startIndex = 0;
        }

        var length = Math.Min(maxLength, text.Length - startIndex);
        var snippet = text.Substring(startIndex, length);

        if (startIndex > 0) snippet = "..." + snippet;
        if (startIndex + length < text.Length) snippet = snippet + "...";

        return snippet;
    }
}

/// <summary>
/// Options for semantic search.
/// </summary>
public class SemanticSearchOptions
{
    /// <summary>
    /// Maximum results to return.
    /// </summary>
    public int MaxResults { get; set; } = 10;

    /// <summary>
    /// Minimum similarity threshold (0.0 to 1.0).
    /// </summary>
    public double MinSimilarity { get; set; } = 0.5;

    /// <summary>
    /// Content types to search.
    /// </summary>
    public HashSet<string> ContentTypes { get; set; } = new() { "posts", "articles" };

    /// <summary>
    /// Account IDs to filter by.
    /// </summary>
    public List<string>? AccountIds { get; set; }
}
