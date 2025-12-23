/// <summary>
/// Legacy interface for text embedding storage.
/// </summary>
/// <remarks>
/// This interface is obsolete. Use the new architecture instead:
/// - IEmbeddingStore for storage operations
/// - IVectorSearchStore for similarity search
/// - IEmbeddingGenerator for embedding generation
/// - EmbeddingService for orchestration
///
/// For backward compatibility, use LegacyTextEmbeddingStoreAdapter to wrap the new components.
/// </remarks>
[Obsolete("Use IEmbeddingStore + IVectorSearchStore + IEmbeddingGenerator instead. See LegacyTextEmbeddingStoreAdapter for migration.")]
public interface ITextEmbeddingStore /*: IDictionary<string, string>*/
{
    public Task<bool> StoreEmbedding(string key, string value);
    public Task<EmbeddingInfo?> GetEmbedding(string key);
    public Task<bool> RemoveEmbedding(string key);
    //public Task<string> GetValue(string key);
    public EmbeddingInfo[] Embeddings { get; }
}
