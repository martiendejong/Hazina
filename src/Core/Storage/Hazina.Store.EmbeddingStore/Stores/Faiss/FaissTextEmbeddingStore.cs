/// <summary>
/// Placeholder FAISS embedding store (never fully implemented).
/// </summary>
/// <remarks>
/// This class was a placeholder and is now obsolete.
/// If you need FAISS vector search, implement IEmbeddingStore + IVectorSearchStore with FAISS backend.
/// Consider using PgVectorStore with HNSW indices for production vector search.
/// </remarks>
[Obsolete("This was never fully implemented. Use PgVectorStore or implement FAISS support using IEmbeddingStore + IVectorSearchStore interfaces.")]
public class FaissTextEmbeddingStore : AbstractTextEmbeddingStore, ITextEmbeddingStore
{
    private readonly string _indexPathOrSpec;
    private readonly List<EmbeddingInfo> _embeddings = new();

    public FaissTextEmbeddingStore(string indexPathOrSpec, ILLMClient embeddingProvider) : base(embeddingProvider)
    {
        _indexPathOrSpec = indexPathOrSpec;
    }

    public override EmbeddingInfo[] Embeddings => _embeddings.ToArray();

    public override async Task<EmbeddingInfo?> GetEmbedding(string key)
    {
        // Placeholder; implement faiss metadata lookup by key
        return _embeddings.FirstOrDefault(e => e.Key == key);
    }

    public override async Task<bool> RemoveEmbedding(string key)
    {
        // Placeholder; implement removal from index/sidecar
        var e = _embeddings.FirstOrDefault(x => x.Key == key);
        if (e == null) return false;
        _embeddings.Remove(e);
        return true;
    }

    protected override async Task UpdateEmbedding(EmbeddingInfo embedding)
    {
        // Placeholder; implement update in FAISS sidecar and index
        var idx = _embeddings.FindIndex(e => e.Key == embedding.Key);
        if (idx >= 0) _embeddings[idx] = embedding; else _embeddings.Add(embedding);
    }

    protected override async Task AddEmbedding(EmbeddingInfo embeddingInfo)
    {
        // Placeholder; implement add to FAISS index + sidecar storage
        _embeddings.Add(embeddingInfo);
    }
}

