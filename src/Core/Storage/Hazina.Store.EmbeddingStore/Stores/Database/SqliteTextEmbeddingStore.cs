/// <summary>
/// Placeholder SQLite embedding store (never fully implemented).
/// </summary>
/// <remarks>
/// This class was a placeholder and is now obsolete.
/// If you need SQLite storage, implement IEmbeddingStore with SQLite backend.
/// Consider using EmbeddingJsonFileStore or PgVectorStore for production use.
/// </remarks>
[Obsolete("This was never fully implemented. Use EmbeddingJsonFileStore or implement SQLite support using IEmbeddingStore interface.")]
public class SqliteTextEmbeddingStore : AbstractTextEmbeddingStore, ITextEmbeddingStore
{
    private readonly string _connectionStringOrPath;
    private readonly List<EmbeddingInfo> _embeddings = new();

    public SqliteTextEmbeddingStore(string connectionStringOrPath, ILLMClient embeddingProvider) : base(embeddingProvider)
    {
        _connectionStringOrPath = connectionStringOrPath;
    }

    public override EmbeddingInfo[] Embeddings => _embeddings.ToArray();

    public override async Task<EmbeddingInfo?> GetEmbedding(string key)
    {
        // Placeholder; implement SQLite read by key
        return _embeddings.FirstOrDefault(e => e.Key == key);
    }

    public override async Task<bool> RemoveEmbedding(string key)
    {
        // Placeholder; implement SQLite delete
        var e = _embeddings.FirstOrDefault(x => x.Key == key);
        if (e == null) return false;
        _embeddings.Remove(e);
        return true;
    }

    protected override async Task UpdateEmbedding(EmbeddingInfo embedding)
    {
        // Placeholder; implement SQLite upsert
        var idx = _embeddings.FindIndex(e => e.Key == embedding.Key);
        if (idx >= 0) _embeddings[idx] = embedding; else _embeddings.Add(embedding);
    }

    protected override async Task AddEmbedding(EmbeddingInfo embeddingInfo)
    {
        // Placeholder; implement SQLite insert
        _embeddings.Add(embeddingInfo);
    }
}

