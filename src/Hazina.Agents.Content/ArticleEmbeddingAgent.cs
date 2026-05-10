using Hazina.Agents.Content.Models;
using Hazina.Store.EmbeddingStore;

namespace Hazina.Agents.Content;

/// <summary>
/// Agent that generates and manages embeddings for articles.
/// Uses OpenAI text-embedding-3-small via the Hazina EmbeddingService.
/// </summary>
public class ArticleEmbeddingAgent
{
    private readonly EmbeddingService _embeddingService;

    public ArticleEmbeddingAgent(EmbeddingService embeddingService)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
    }

    /// <summary>
    /// Generates and stores an embedding for a single article.
    /// Uses checksum-based caching: unchanged articles skip re-embedding.
    /// </summary>
    /// <param name="article">The article to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if a new embedding was generated, false if cached</returns>
    public async Task<bool> EmbedArticleAsync(Article article, CancellationToken cancellationToken = default)
    {
        if (article == null) throw new ArgumentNullException(nameof(article));
        if (string.IsNullOrEmpty(article.Id)) throw new ArgumentException("Article must have an Id");

        var key = BuildEmbeddingKey(article.Id);
        var text = article.ToEmbeddingText();

        return await _embeddingService.StoreTextAsync(key, text, cancellationToken);
    }

    /// <summary>
    /// Generates and stores embeddings for multiple articles in batch.
    /// More efficient than calling EmbedArticleAsync in a loop.
    /// </summary>
    /// <param name="articles">Articles to embed</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of new embeddings generated (excludes cached)</returns>
    public async Task<int> EmbedBatchAsync(IEnumerable<Article> articles, CancellationToken cancellationToken = default)
    {
        if (articles == null) throw new ArgumentNullException(nameof(articles));

        var items = articles
            .Where(a => !string.IsNullOrEmpty(a.Id))
            .Select(a => (key: BuildEmbeddingKey(a.Id), value: a.ToEmbeddingText()));

        return await _embeddingService.StoreBatchAsync(items, cancellationToken);
    }

    /// <summary>
    /// Retrieves the stored embedding for an article.
    /// </summary>
    /// <param name="articleId">The article ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The embedding info if found, null otherwise</returns>
    public async Task<EmbeddingInfo?> GetEmbeddingAsync(string articleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(articleId)) throw new ArgumentException("articleId cannot be null or empty");
        return await _embeddingService.GetAsync(BuildEmbeddingKey(articleId), cancellationToken);
    }

    /// <summary>
    /// Calculates cosine similarity between two articles.
    /// Both articles must have been embedded first.
    /// </summary>
    /// <param name="articleIdA">First article ID</param>
    /// <param name="articleIdB">Second article ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cosine similarity score (0.0 to 1.0), or null if either embedding is missing</returns>
    public async Task<double?> CalculateSimilarityAsync(
        string articleIdA,
        string articleIdB,
        CancellationToken cancellationToken = default)
    {
        var embA = await GetEmbeddingAsync(articleIdA, cancellationToken);
        var embB = await GetEmbeddingAsync(articleIdB, cancellationToken);

        if (embA?.Data == null || embB?.Data == null)
            return null;

        return embA.Data.CosineSimilarity(embB.Data);
    }

    /// <summary>
    /// Removes the stored embedding for an article.
    /// </summary>
    public async Task<bool> RemoveEmbeddingAsync(string articleId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(articleId)) throw new ArgumentException("articleId cannot be null or empty");
        return await _embeddingService.RemoveAsync(BuildEmbeddingKey(articleId), cancellationToken);
    }

    private static string BuildEmbeddingKey(string articleId) => $"article:{articleId}";
}
