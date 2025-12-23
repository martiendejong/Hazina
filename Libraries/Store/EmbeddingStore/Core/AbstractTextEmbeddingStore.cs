/// <summary>
/// Legacy abstract base class for text embedding storage.
/// </summary>
/// <remarks>
/// This class is obsolete. Use the new architecture instead:
/// - Implement IEmbeddingStore for storage operations
/// - Implement IVectorSearchStore for similarity search
/// - Use EmbeddingService for orchestration with checksum-based caching
/// </remarks>
[Obsolete("Use IEmbeddingStore implementations with EmbeddingService instead.")]
public abstract class AbstractTextEmbeddingStore : /*AbstractStore<string>,*/ ITextEmbeddingStore
{
    public ILLMClient? EmbeddingProvider { get; set; }

    public abstract EmbeddingInfo[] Embeddings { get; }

    protected abstract Task AddEmbedding(EmbeddingInfo embeddingInfo);
    protected abstract Task UpdateEmbedding(EmbeddingInfo embedding);
    public abstract Task<EmbeddingInfo?> GetEmbedding(string key);
    public abstract Task<bool> RemoveEmbedding(string key);

    //protected abstract Task AddValue(string key, string value);
    //protected abstract Task UpdateValue(string key, string value);
    //public abstract Task<string> GetValue(string key);

    public AbstractTextEmbeddingStore(ILLMClient? embeddingProvider = null)
    {
        EmbeddingProvider = embeddingProvider;
    }

    public async Task<bool> StoreEmbedding(string key, string value)
    {
        if (EmbeddingProvider == null) throw new Exception("No embeddingprovider");

        var embedding = await GetEmbedding(key);
        var checksum = Checksum.CalculateChecksumFromString(value);
        if (embedding == null)
        {
            var embeddingData = await EmbeddingProvider.GenerateEmbedding($"key:\n{key}\nvalue:\n{value}");
            await AddEmbedding(new EmbeddingInfo(key, checksum, embeddingData));
            //await AddValue(embedding, value);
        }
        else if (checksum == embedding.Checksum)
        {
            return false;
        }
        else
        {
            var embeddingData = await EmbeddingProvider.GenerateEmbedding($"key:\n{key}\nvalue:\n{value}");
            embedding.Checksum = checksum;
            embedding.Data = embeddingData;
            await UpdateEmbedding(embedding);
            //await UpdateValue(embedding, value);
        }
        return true;
    }
}
