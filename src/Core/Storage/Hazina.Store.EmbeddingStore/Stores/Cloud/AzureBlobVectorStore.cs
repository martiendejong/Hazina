using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System.Text.Json;

namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Azure Blob Storage-based vector store implementation.
/// Stores embeddings as JSON blobs in Azure Blob Storage with optional metadata.
/// Suitable for large-scale, durable storage with geographic replication.
/// </summary>
/// <remarks>
/// Each embedding is stored as a separate blob with naming pattern: {containerName}/{key}.json
/// Metadata (key, checksum, dimension) is stored in blob metadata for fast querying.
/// </remarks>
public class AzureBlobVectorStore : IEmbeddingStore, IVectorSearchStore, IBatchEmbeddingStore
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobContainerClient _containerClient;
    private readonly string _containerName;
    private readonly int _dimension;

    /// <summary>
    /// Creates a new AzureBlobVectorStore using connection string.
    /// </summary>
    /// <param name="connectionString">Azure Storage connection string</param>
    /// <param name="containerName">Container name for embeddings (will be created if not exists)</param>
    /// <param name="dimension">Embedding vector dimension (e.g., 1536 for OpenAI)</param>
    public AzureBlobVectorStore(string connectionString, string containerName, int dimension = 1536)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

        if (string.IsNullOrWhiteSpace(containerName))
            throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));

        if (dimension <= 0)
            throw new ArgumentException("Dimension must be positive", nameof(dimension));

        _blobServiceClient = new BlobServiceClient(connectionString);
        _containerName = containerName;
        _dimension = dimension;
        _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Create container if it doesn't exist
        _containerClient.CreateIfNotExists(PublicAccessType.None);
    }

    /// <summary>
    /// Creates a new AzureBlobVectorStore using existing BlobServiceClient.
    /// </summary>
    public AzureBlobVectorStore(BlobServiceClient blobServiceClient, string containerName, int dimension = 1536)
    {
        if (blobServiceClient == null)
            throw new ArgumentNullException(nameof(blobServiceClient));

        if (string.IsNullOrWhiteSpace(containerName))
            throw new ArgumentException("Container name cannot be null or empty", nameof(containerName));

        if (dimension <= 0)
            throw new ArgumentException("Dimension must be positive", nameof(dimension));

        _blobServiceClient = blobServiceClient;
        _containerName = containerName;
        _dimension = dimension;
        _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

        // Create container if it doesn't exist
        _containerClient.CreateIfNotExists(PublicAccessType.None);
    }

    private string GetBlobName(string key) => $"{key}.json";

    #region IEmbeddingStore Implementation

    public async Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (embedding == null)
            throw new ArgumentNullException(nameof(embedding));

        if (embedding.Count != _dimension)
            throw new ArgumentException($"Embedding dimension {embedding.Count} does not match expected {_dimension}");

        var blobName = GetBlobName(key);
        var blobClient = _containerClient.GetBlobClient(blobName);

        var embeddingInfo = new EmbeddingInfo(key, checksum, embedding);
        var json = JsonSerializer.Serialize(embeddingInfo);

        // Upload with metadata
        var metadata = new Dictionary<string, string>
        {
            ["key"] = key,
            ["checksum"] = checksum,
            ["dimension"] = _dimension.ToString(),
            ["updated_at"] = DateTimeOffset.UtcNow.ToString("o")
        };

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        await blobClient.UploadAsync(stream, new BlobUploadOptions
        {
            Metadata = metadata,
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "application/json"
            }
        });

        return true;
    }

    public async Task<EmbeddingInfo?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var blobName = GetBlobName(key);
        var blobClient = _containerClient.GetBlobClient(blobName);

        if (!await blobClient.ExistsAsync())
            return null;

        var response = await blobClient.DownloadContentAsync();
        var json = response.Value.Content.ToString();

        var embeddingInfo = JsonSerializer.Deserialize<EmbeddingInfo>(json);
        return embeddingInfo;
    }

    public async Task<bool> RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var blobName = GetBlobName(key);
        var blobClient = _containerClient.GetBlobClient(blobName);

        var response = await blobClient.DeleteIfExistsAsync();
        return response.Value;
    }

    public async Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var blobName = GetBlobName(key);
        var blobClient = _containerClient.GetBlobClient(blobName);

        return await blobClient.ExistsAsync();
    }

    #endregion

    #region IVectorSearchStore Implementation

    /// <summary>
    /// Performs client-side vector similarity search.
    /// Note: This requires loading all embeddings into memory, which may be slow for large datasets.
    /// For better performance, consider using Azure Cognitive Search with vector search capabilities.
    /// </summary>
    public async Task<List<ScoredEmbedding>> SearchSimilarAsync(
        Embedding queryEmbedding,
        int topK = 10,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding == null)
            throw new ArgumentNullException(nameof(queryEmbedding));

        if (queryEmbedding.Count != _dimension)
            throw new ArgumentException($"Query embedding dimension {queryEmbedding.Count} does not match expected {_dimension}");

        if (topK <= 0)
            throw new ArgumentException("topK must be positive", nameof(topK));

        var results = new List<ScoredEmbedding>();

        // Enumerate all blobs and compute similarity
        await foreach (var blobItem in _containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!blobItem.Name.EndsWith(".json"))
                continue;

            var key = blobItem.Name.Substring(0, blobItem.Name.Length - 5); // Remove .json
            var embeddingInfo = await GetAsync(key);

            if (embeddingInfo == null)
                continue;

            var similarity = embeddingInfo.Data.CosineSimilarity(queryEmbedding);
            if (similarity >= minSimilarity)
            {
                results.Add(new ScoredEmbedding
                {
                    Info = embeddingInfo,
                    Similarity = similarity
                });
            }
        }

        return results
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .ToList();
    }

    #endregion

    #region IBatchEmbeddingStore Implementation

    public async Task<int> StoreBatchAsync(
        IEnumerable<(string key, Embedding embedding, string checksum)> batch,
        CancellationToken cancellationToken = default)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));

        var items = batch.ToList();
        if (items.Count == 0)
            return 0;

        // Upload blobs in parallel
        var tasks = items.Select(async item =>
        {
            var (key, embedding, checksum) = item;

            if (embedding.Count != _dimension)
                throw new ArgumentException($"Embedding dimension {embedding.Count} does not match expected {_dimension}");

            await StoreAsync(key, embedding, checksum);
        });

        await Task.WhenAll(tasks);
        return items.Count;
    }

    public async Task<List<EmbeddingInfo>> GetBatchAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys == null)
            throw new ArgumentNullException(nameof(keys));

        var keyList = keys.ToList();
        if (keyList.Count == 0)
            return new List<EmbeddingInfo>();

        // Download blobs in parallel
        var tasks = keyList.Select(async key =>
        {
            var embeddingInfo = await GetAsync(key);
            return embeddingInfo;
        });

        var embeddingInfos = await Task.WhenAll(tasks);
        return embeddingInfos.Where(x => x != null).Cast<EmbeddingInfo>().ToList();
    }

    #endregion
}
