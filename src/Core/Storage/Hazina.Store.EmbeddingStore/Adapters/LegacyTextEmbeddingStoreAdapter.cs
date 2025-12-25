namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Backward compatibility adapter that wraps new interfaces to provide the old ITextEmbeddingStore interface.
/// This allows gradual migration from the old architecture to the new one.
/// Mark as [Obsolete] after migration is complete.
/// </summary>
public class LegacyTextEmbeddingStoreAdapter : ITextEmbeddingStore
{
    private readonly EmbeddingService _embeddingService;
    private readonly IEnumerableEmbeddingStore? _enumerableStore;
    private readonly IEmbeddingStore _store;

    /// <summary>
    /// Creates an adapter from the new architecture components.
    /// </summary>
    /// <param name="store">The embedding store</param>
    /// <param name="generator">The embedding generator</param>
    public LegacyTextEmbeddingStoreAdapter(IEmbeddingStore store, IEmbeddingGenerator generator)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _embeddingService = new EmbeddingService(store, generator);
        _enumerableStore = store as IEnumerableEmbeddingStore;
    }

    /// <summary>
    /// Creates an adapter with an existing EmbeddingService.
    /// </summary>
    public LegacyTextEmbeddingStoreAdapter(EmbeddingService embeddingService, IEmbeddingStore store)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _enumerableStore = store as IEnumerableEmbeddingStore;
    }

    /// <summary>
    /// Legacy method: Stores text by generating its embedding.
    /// Maps to EmbeddingService.StoreTextAsync.
    /// </summary>
    public Task<bool> StoreEmbedding(string key, string value)
    {
        return _embeddingService.StoreTextAsync(key, value);
    }

    /// <summary>
    /// Legacy method: Retrieves an embedding by key.
    /// Maps to IEmbeddingStore.GetAsync.
    /// </summary>
    public Task<EmbeddingInfo?> GetEmbedding(string key)
    {
        return _store.GetAsync(key);
    }

    /// <summary>
    /// Legacy method: Removes an embedding.
    /// Maps to IEmbeddingStore.RemoveAsync.
    /// </summary>
    public Task<bool> RemoveEmbedding(string key)
    {
        return _store.RemoveAsync(key);
    }

    /// <summary>
    /// Legacy property: Returns all embeddings as an array.
    /// Only works if the underlying store implements IEnumerableEmbeddingStore.
    /// WARNING: This loads all embeddings into memory - not suitable for large datasets.
    /// </summary>
    public EmbeddingInfo[] Embeddings
    {
        get
        {
            if (_enumerableStore == null)
            {
                throw new NotSupportedException(
                    $"The underlying store ({_store.GetType().Name}) does not support enumeration. " +
                    "This is expected for large-scale stores like PgVectorStore. " +
                    "Use IVectorSearchStore.SearchSimilarAsync instead of accessing all embeddings.");
            }

            // Blocking call - convert async enumerable to array
            return _enumerableStore.GetAllAsync().ToBlockingEnumerable().ToArray();
        }
    }
}
