namespace DevGPT.Store.EmbeddingStore;

/// <summary>
/// Orchestration service that combines embedding generation and storage.
/// This replaces the logic in AbstractTextEmbeddingStore, properly separating concerns.
/// </summary>
public class EmbeddingService
{
    private readonly IEmbeddingStore _store;
    private readonly IEmbeddingGenerator _generator;

    /// <summary>
    /// Creates a new EmbeddingService.
    /// </summary>
    /// <param name="store">The store to persist embeddings</param>
    /// <param name="generator">The generator to create embeddings</param>
    public EmbeddingService(IEmbeddingStore store, IEmbeddingGenerator generator)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
    }

    /// <summary>
    /// Stores text by generating its embedding. Uses checksum-based caching to avoid regenerating unchanged content.
    /// </summary>
    /// <param name="key">Unique identifier for the text</param>
    /// <param name="value">The text content to embed and store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if embedding was generated/updated, false if cached (no change)</returns>
    public async Task<bool> StoreTextAsync(
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (value == null)
            throw new ArgumentNullException(nameof(value));

        // Calculate checksum of the source text
        var checksum = Checksum.CalculateChecksumFromString(value);

        // Check if we already have this embedding with the same checksum
        var existing = await _store.GetAsync(key);
        if (existing != null && existing.Checksum == checksum)
        {
            return false; // No change, cached
        }

        // Generate embedding from consistent format (matches original behavior)
        var embeddingText = $"key:\n{key}\nvalue:\n{value}";
        var embedding = await _generator.GenerateAsync(embeddingText, cancellationToken);

        // Store with the checksum matching the source text (not the formatted text)
        await _store.StoreAsync(key, embedding, checksum);

        return true; // New or updated
    }

    /// <summary>
    /// Stores pre-computed embedding directly without generation.
    /// Useful when embeddings are computed externally or in batch.
    /// </summary>
    /// <param name="key">Unique identifier</param>
    /// <param name="embedding">Pre-computed embedding vector</param>
    /// <param name="checksum">Checksum for cache validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<bool> StoreEmbeddingAsync(
        string key,
        Embedding embedding,
        string checksum,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (embedding == null)
            throw new ArgumentNullException(nameof(embedding));

        if (string.IsNullOrEmpty(checksum))
            throw new ArgumentException("Checksum cannot be null or empty", nameof(checksum));

        // Validate dimensions
        if (embedding.Count != _generator.Dimensions)
        {
            throw new ArgumentException(
                $"Embedding dimension {embedding.Count} does not match expected {_generator.Dimensions}",
                nameof(embedding));
        }

        return await _store.StoreAsync(key, embedding, checksum);
    }

    /// <summary>
    /// Stores multiple texts in batch, generating embeddings for each.
    /// More efficient than calling StoreTextAsync in a loop.
    /// </summary>
    /// <param name="items">Collection of key-value pairs to store</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of embeddings generated (excludes cached items)</returns>
    public async Task<int> StoreBatchAsync(
        IEnumerable<(string key, string value)> items,
        CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));

        var itemList = items.ToList();
        if (itemList.Count == 0)
            return 0;

        // Check which items need embedding generation
        var toGenerate = new List<(string key, string value, string checksum)>();

        foreach (var (key, value) in itemList)
        {
            var checksum = Checksum.CalculateChecksumFromString(value);
            var existing = await _store.GetAsync(key);

            if (existing == null || existing.Checksum != checksum)
            {
                toGenerate.Add((key, value, checksum));
            }
        }

        if (toGenerate.Count == 0)
            return 0; // All cached

        // Generate embeddings in batch
        var textsToEmbed = toGenerate.Select(x => $"key:\n{x.key}\nvalue:\n{x.value}").ToList();
        var embeddings = await _generator.GenerateBatchAsync(textsToEmbed, cancellationToken);

        // Store based on whether the store supports batch operations
        if (_store is IBatchEmbeddingStore batchStore)
        {
            var batch = toGenerate.Zip(embeddings, (item, emb) => (item.key, emb, item.checksum));
            await batchStore.StoreBatchAsync(batch, cancellationToken);
        }
        else
        {
            // Fallback to sequential storage
            for (int i = 0; i < toGenerate.Count; i++)
            {
                await _store.StoreAsync(toGenerate[i].key, embeddings[i], toGenerate[i].checksum);
            }
        }

        return toGenerate.Count;
    }

    /// <summary>
    /// Retrieves an embedding by key.
    /// </summary>
    public Task<EmbeddingInfo?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        return _store.GetAsync(key);
    }

    /// <summary>
    /// Removes an embedding from the store.
    /// </summary>
    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        return _store.RemoveAsync(key);
    }

    /// <summary>
    /// Checks if an embedding exists.
    /// </summary>
    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        return _store.ExistsAsync(key);
    }

    /// <summary>
    /// Generates an embedding for a query without storing it.
    /// Useful for search operations.
    /// </summary>
    /// <param name="query">The query text</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The generated embedding</returns>
    public Task<Embedding> GenerateQueryEmbeddingAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(query))
            throw new ArgumentException("Query cannot be null or empty", nameof(query));

        return _generator.GenerateAsync(query, cancellationToken);
    }
}
