using Hazina.Store.EmbeddingStore;

namespace Hazina.AI.Memory.Core;

/// <summary>
/// Formalized interface for semantic memory - long-term embeddings and chunks.
/// This is a convenience wrapper over existing embedding store infrastructure.
/// </summary>
public interface ISemanticMemoryStore
{
    /// <summary>
    /// Store a memory chunk with embedding
    /// </summary>
    /// <param name="key">Unique identifier</param>
    /// <param name="text">Text content</param>
    /// <param name="embedding">Embedding vector</param>
    /// <param name="metadata">Optional metadata</param>
    Task StoreAsync(string key, string text, Embedding embedding, Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Search for similar memories
    /// </summary>
    /// <param name="queryEmbedding">Query embedding</param>
    /// <param name="topK">Number of results</param>
    /// <param name="minSimilarity">Minimum similarity threshold</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<List<SemanticMemory>> SearchAsync(
        Embedding queryEmbedding,
        int topK = 5,
        double minSimilarity = 0.7,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific memory by key
    /// </summary>
    Task<SemanticMemory?> GetAsync(string key);

    /// <summary>
    /// Remove a memory
    /// </summary>
    Task<bool> RemoveAsync(string key);
}

/// <summary>
/// Semantic memory item retrieved from storage
/// </summary>
public class SemanticMemory
{
    public required string Key { get; init; }
    public required string Text { get; init; }
    public required Embedding Embedding { get; init; }
    public double Similarity { get; init; }
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Adapter to use IVectorSearchStore + IEmbeddingStore as ISemanticMemoryStore
/// </summary>
public class SemanticMemoryStoreAdapter : ISemanticMemoryStore
{
    private readonly IEmbeddingStore _embeddingStore;
    private readonly IVectorSearchStore _vectorSearchStore;

    public SemanticMemoryStoreAdapter(
        IEmbeddingStore embeddingStore,
        IVectorSearchStore vectorSearchStore)
    {
        _embeddingStore = embeddingStore ?? throw new ArgumentNullException(nameof(embeddingStore));
        _vectorSearchStore = vectorSearchStore ?? throw new ArgumentNullException(nameof(vectorSearchStore));
    }

    public async Task StoreAsync(
        string key,
        string text,
        Embedding embedding,
        Dictionary<string, object>? metadata = null)
    {
        var checksum = ComputeChecksum(text);

        var fullMetadata = metadata ?? new Dictionary<string, object>();
        fullMetadata["text"] = text;

        await _embeddingStore.StoreAsync(key, embedding, checksum);
    }

    public async Task<List<SemanticMemory>> SearchAsync(
        Embedding queryEmbedding,
        int topK = 5,
        double minSimilarity = 0.7,
        CancellationToken cancellationToken = default)
    {
        var results = await _vectorSearchStore.SearchSimilarAsync(
            queryEmbedding,
            topK,
            minSimilarity,
            cancellationToken
        );

        return results.Select(r => new SemanticMemory
        {
            Key = r.Info.Key,
            Text = GetTextFromMetadata(r),
            Embedding = r.Info.Data,
            Similarity = r.Similarity,
            Metadata = r.Metadata
        }).ToList();
    }

    public async Task<SemanticMemory?> GetAsync(string key)
    {
        var info = await _embeddingStore.GetAsync(key);
        if (info == null)
            return null;

        return new SemanticMemory
        {
            Key = info.Key,
            Text = info.Checksum,
            Embedding = info.Data,
            Similarity = 1.0,
            Metadata = null
        };
    }

    public async Task<bool> RemoveAsync(string key)
    {
        return await _embeddingStore.RemoveAsync(key);
    }

    private static string GetTextFromMetadata(ScoredEmbedding scored)
    {
        if (scored.Metadata != null &&
            scored.Metadata.TryGetValue("text", out var textObj) &&
            textObj is string text)
        {
            return text;
        }

        return scored.Info.Checksum;
    }

    private static string ComputeChecksum(string text)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(hash).Substring(0, 16);
    }
}
