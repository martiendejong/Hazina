using System.Collections.Concurrent;
using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.Logging;

namespace Hazina.Store.Faiss;

/// <summary>
/// FAISS-compatible embedding store that implements <see cref="IEmbeddingStore"/>
/// and <see cref="IVectorSearchStore"/>.
/// </summary>
/// <remarks>
/// <para>
/// By default this store uses a thread-safe in-memory index that performs exact
/// cosine similarity search — identical semantics to what a FAISS Flat index
/// (IndexFlatIP with normalized vectors) provides.
/// </para>
/// <para>
/// For approximate-nearest-neighbour search with a native FAISS library, supply
/// an <see cref="IFaissIndexBackend"/> implementation. This keeps the adapter
/// contract stable regardless of the underlying ANN strategy.
/// </para>
/// <para>
/// <b>Persistence:</b> Call <see cref="SaveAsync"/> and <see cref="LoadAsync"/>
/// to checkpoint and restore the in-memory index to/from disk. The default backend
/// serializes vectors as raw floats (IEEE 754 little-endian).
/// </para>
/// </remarks>
public sealed class FaissEmbeddingStore : IEmbeddingStore, IVectorSearchStore
{
    private readonly IFaissIndexBackend _backend;
    private readonly ILogger? _logger;

    // Metadata sidecar — FAISS indices don't natively store arbitrary payloads
    private readonly ConcurrentDictionary<string, string> _checksums = new();

    /// <summary>
    /// Initializes a new <see cref="FaissEmbeddingStore"/> using the default
    /// in-memory brute-force backend.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public FaissEmbeddingStore(ILogger? logger = null)
        : this(new InMemoryFaissBackend(), logger)
    {
    }

    /// <summary>
    /// Initializes a new <see cref="FaissEmbeddingStore"/> using a custom backend.
    /// </summary>
    /// <param name="backend">Custom <see cref="IFaissIndexBackend"/> implementation.</param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    public FaissEmbeddingStore(IFaissIndexBackend backend, ILogger? logger = null)
    {
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
        _logger = logger;
    }

    #region IEmbeddingStore

    /// <inheritdoc/>
    public Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));
        if (embedding is null)
            throw new ArgumentNullException(nameof(embedding));

        var vector = ToFloatArray(embedding);
        _backend.Upsert(key, vector);
        _checksums[key] = checksum;

        _logger?.LogDebug("Stored embedding for key '{Key}' (dim={Dim})", key, embedding.Count);
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public Task<EmbeddingInfo?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        if (_backend is InMemoryFaissBackend inMemory && inMemory.TryGetVector(key, out var vector))
        {
            var checksum = _checksums.TryGetValue(key, out var cs) ? cs : string.Empty;
            var embedding = ToEmbedding(vector!);
            return Task.FromResult<EmbeddingInfo?>(new EmbeddingInfo(key, checksum, embedding));
        }

        return Task.FromResult<EmbeddingInfo?>(null);
    }

    /// <inheritdoc/>
    public Task<bool> RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        var removed = _backend.Remove(key);
        if (removed)
            _checksums.TryRemove(key, out _);

        return Task.FromResult(removed);
    }

    /// <inheritdoc/>
    public Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        var exists = _checksums.ContainsKey(key);
        return Task.FromResult(exists);
    }

    #endregion

    #region IVectorSearchStore

    /// <summary>
    /// Searches for the most similar embeddings using the configured backend.
    /// With the default in-memory backend this is exact cosine similarity (FAISS Flat equivalent).
    /// With a custom backend this delegates to the ANN strategy of your choice.
    /// </summary>
    /// <inheritdoc/>
    public Task<List<ScoredEmbedding>> SearchSimilarAsync(
        Embedding queryEmbedding,
        int topK = 10,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding is null)
            throw new ArgumentNullException(nameof(queryEmbedding));
        if (topK <= 0)
            throw new ArgumentException("topK must be a positive integer.", nameof(topK));

        var query = ToFloatArray(queryEmbedding);
        var rawResults = _backend.Search(query, topK);

        var results = rawResults
            .Where(r => r.similarity >= minSimilarity)
            .Select(r =>
            {
                var checksum = _checksums.TryGetValue(r.key, out var cs) ? cs : string.Empty;

                // Retrieve full embedding for the result (only possible with in-memory backend)
                Embedding embedding;
                if (_backend is InMemoryFaissBackend inMemory
                    && inMemory.TryGetVector(r.key, out var vec))
                {
                    embedding = ToEmbedding(vec!);
                }
                else
                {
                    embedding = new Embedding(); // empty when using native backend
                }

                return new ScoredEmbedding
                {
                    Info = new EmbeddingInfo(r.key, checksum, embedding),
                    Similarity = r.similarity
                };
            })
            .ToList();

        _logger?.LogDebug("FAISS search returned {Count} results (topK={TopK}, minSim={MinSim:F2})",
            results.Count, topK, minSimilarity);

        return Task.FromResult(results);
    }

    #endregion

    #region Persistence helpers

    /// <summary>
    /// Saves the index to the given path. Delegates to <see cref="IFaissIndexBackend.Save"/>.
    /// </summary>
    /// <param name="path">Destination path for the index file.</param>
    public Task SaveAsync(string path)
    {
        _backend.Save(path);
        _logger?.LogInformation("FAISS index saved to '{Path}'", path);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Loads the index from the given path. Delegates to <see cref="IFaissIndexBackend.Load"/>.
    /// </summary>
    /// <param name="path">Source path of the index file.</param>
    public Task LoadAsync(string path)
    {
        _backend.Load(path);
        _logger?.LogInformation("FAISS index loaded from '{Path}'", path);
        return Task.CompletedTask;
    }

    #endregion

    #region Helpers

    private static float[] ToFloatArray(Embedding embedding) =>
        embedding.Select(d => (float)d).ToArray();

    private static Embedding ToEmbedding(float[] vector) =>
        new(vector.Select(f => (double)f));

    #endregion
}
