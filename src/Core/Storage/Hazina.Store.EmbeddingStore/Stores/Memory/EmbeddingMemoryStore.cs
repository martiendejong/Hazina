namespace DevGPT.Store.EmbeddingStore;

/// <summary>
/// In-memory embedding store. Data is lost when the process terminates.
/// Useful for testing, caching, or temporary storage scenarios.
/// This is the refactored version replacing TextEmbeddingMemoryStore.
/// </summary>
public class EmbeddingMemoryStore : IEmbeddingStore, IEnumerableEmbeddingStore, IVectorSearchStore, IBatchEmbeddingStore
{
    private readonly Dictionary<string, EmbeddingInfo> _embeddings = new Dictionary<string, EmbeddingInfo>();
    private readonly object _lock = new object();

    /// <summary>
    /// Gets the current count of embeddings in the store.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _embeddings.Count;
            }
        }
    }

    /// <summary>
    /// Clears all embeddings from the store.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _embeddings.Clear();
        }
    }

    #region IEmbeddingStore Implementation

    public Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (embedding == null)
            throw new ArgumentNullException(nameof(embedding));

        lock (_lock)
        {
            _embeddings[key] = new EmbeddingInfo(key, checksum, embedding);
        }

        return Task.FromResult(true);
    }

    public Task<EmbeddingInfo?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        lock (_lock)
        {
            _embeddings.TryGetValue(key, out var embedding);
            return Task.FromResult(embedding);
        }
    }

    public Task<bool> RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        lock (_lock)
        {
            return Task.FromResult(_embeddings.Remove(key));
        }
    }

    public Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        lock (_lock)
        {
            return Task.FromResult(_embeddings.ContainsKey(key));
        }
    }

    #endregion

    #region IEnumerableEmbeddingStore Implementation

    public async IAsyncEnumerable<EmbeddingInfo> GetAllAsync(CancellationToken cancellationToken = default)
    {
        List<EmbeddingInfo> snapshot;
        lock (_lock)
        {
            snapshot = _embeddings.Values.ToList(); // Create snapshot to avoid holding lock
        }

        foreach (var embedding in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return embedding;
        }
    }

    #endregion

    #region IVectorSearchStore Implementation

    /// <summary>
    /// In-memory vector search using cosine similarity.
    /// Fast for small to medium datasets (< 100k embeddings).
    /// </summary>
    public Task<List<ScoredEmbedding>> SearchSimilarAsync(
        Embedding queryEmbedding,
        int topK = 10,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding == null)
            throw new ArgumentNullException(nameof(queryEmbedding));

        if (topK <= 0)
            throw new ArgumentException("topK must be positive", nameof(topK));

        List<ScoredEmbedding> results;
        lock (_lock)
        {
            results = _embeddings.Values
                .Select(info =>
                {
                    var similarity = info.Data.CosineSimilarity(queryEmbedding);
                    return new ScoredEmbedding
                    {
                        Info = info,
                        Similarity = similarity
                    };
                })
                .Where(scored => scored.Similarity >= minSimilarity)
                .OrderByDescending(scored => scored.Similarity)
                .Take(topK)
                .ToList();
        }

        return Task.FromResult(results);
    }

    #endregion

    #region IBatchEmbeddingStore Implementation

    public Task<int> StoreBatchAsync(
        IEnumerable<(string key, Embedding embedding, string checksum)> batch,
        CancellationToken cancellationToken = default)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));

        var items = batch.ToList();
        if (items.Count == 0)
            return Task.FromResult(0);

        lock (_lock)
        {
            foreach (var (key, embedding, checksum) in items)
            {
                _embeddings[key] = new EmbeddingInfo(key, checksum, embedding);
            }
        }

        return Task.FromResult(items.Count);
    }

    public Task<List<EmbeddingInfo>> GetBatchAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        var keyList = keys.ToList();
        if (keyList.Count == 0)
            return Task.FromResult(new List<EmbeddingInfo>());

        lock (_lock)
        {
            var results = keyList
                .Where(key => _embeddings.ContainsKey(key))
                .Select(key => _embeddings[key])
                .ToList();

            return Task.FromResult(results);
        }
    }

    #endregion
}
