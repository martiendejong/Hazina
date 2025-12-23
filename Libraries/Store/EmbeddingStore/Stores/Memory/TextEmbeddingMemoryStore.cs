/// <summary>
/// Legacy in-memory embedding store.
/// </summary>
/// <remarks>
/// This class is obsolete. Use EmbeddingMemoryStore instead for:
/// - Better separation of concerns (no embedding generation in storage)
/// - Dictionary-based storage for faster lookups
/// - Batch operations support
/// - Native vector search capability
/// - Thread-safe operations
/// </remarks>
[Obsolete("Use EmbeddingMemoryStore with EmbeddingService instead. See Store/EmbeddingStore/EmbeddingMemoryStore.cs")]
public class TextEmbeddingMemoryStore : AbstractTextEmbeddingStore, ITextEmbeddingStore
{
    public TextEmbeddingMemoryStore(ILLMClient embeddingProvider) : base(embeddingProvider) { }

    public override EmbeddingInfo[] Embeddings
    {
        get
        {
            return _embeddings.ToArray();
        }
    }

    public List<EmbeddingInfo> _embeddings = new List<EmbeddingInfo>();

    protected override async Task UpdateEmbedding(EmbeddingInfo embedding) { }
    protected override async Task AddEmbedding(EmbeddingInfo embeddingInfo)
    {
        _embeddings.Add(embeddingInfo);
    }

    public override async Task<EmbeddingInfo?> GetEmbedding(string key)
    {
        return _embeddings.FirstOrDefault(e => e.Key == key);
    }

    public override async Task<bool> RemoveEmbedding(string key)
    {
        var embedding = _embeddings.FirstOrDefault(e => e.Key == key);
        if (embedding == null) return false;
        _embeddings.Remove(embedding);
        return true;
    }
}
