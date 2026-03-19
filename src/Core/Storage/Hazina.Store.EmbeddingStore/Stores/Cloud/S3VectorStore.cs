using Amazon.S3;
using Amazon.S3.Model;
using System.Text.Json;

namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Amazon S3-based vector store implementation.
/// Stores embeddings as JSON objects in S3 with optional metadata.
/// Suitable for large-scale, durable storage with geographic replication and cost-effective archival.
/// </summary>
/// <remarks>
/// Each embedding is stored as a separate S3 object with naming pattern: {bucketName}/{prefix}/{key}.json
/// Metadata (key, checksum, dimension) is stored in object metadata for fast querying.
/// </remarks>
public class S3VectorStore : IEmbeddingStore, IVectorSearchStore, IBatchEmbeddingStore
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly string _keyPrefix;
    private readonly int _dimension;

    /// <summary>
    /// Creates a new S3VectorStore using existing S3 client.
    /// </summary>
    /// <param name="s3Client">Configured Amazon S3 client</param>
    /// <param name="bucketName">S3 bucket name for embeddings</param>
    /// <param name="dimension">Embedding vector dimension (e.g., 1536 for OpenAI)</param>
    /// <param name="keyPrefix">Key prefix for all objects (default: "embeddings/")</param>
    public S3VectorStore(IAmazonS3 s3Client, string bucketName, int dimension = 1536, string keyPrefix = "embeddings/")
    {
        if (s3Client == null)
            throw new ArgumentNullException(nameof(s3Client));

        if (string.IsNullOrWhiteSpace(bucketName))
            throw new ArgumentException("Bucket name cannot be null or empty", nameof(bucketName));

        if (dimension <= 0)
            throw new ArgumentException("Dimension must be positive", nameof(dimension));

        _s3Client = s3Client;
        _bucketName = bucketName;
        _keyPrefix = keyPrefix ?? string.Empty;
        _dimension = dimension;

        // Ensure bucket exists (will throw if not accessible)
        EnsureBucketExistsAsync().GetAwaiter().GetResult();
    }

    private async Task EnsureBucketExistsAsync()
    {
        try
        {
            await _s3Client.GetBucketLocationAsync(_bucketName);
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchBucket")
        {
            throw new InvalidOperationException($"S3 bucket '{_bucketName}' does not exist. Please create it first.", ex);
        }
    }

    private string GetS3Key(string key) => $"{_keyPrefix}{key}.json";

    #region IEmbeddingStore Implementation

    public async Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (embedding == null)
            throw new ArgumentNullException(nameof(embedding));

        if (embedding.Count != _dimension)
            throw new ArgumentException($"Embedding dimension {embedding.Count} does not match expected {_dimension}");

        var s3Key = GetS3Key(key);
        var embeddingInfo = new EmbeddingInfo(key, checksum, embedding);
        var json = JsonSerializer.Serialize(embeddingInfo);

        var putRequest = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = s3Key,
            ContentBody = json,
            ContentType = "application/json",
            Metadata =
            {
                ["x-amz-meta-key"] = key,
                ["x-amz-meta-checksum"] = checksum,
                ["x-amz-meta-dimension"] = _dimension.ToString(),
                ["x-amz-meta-updated-at"] = DateTimeOffset.UtcNow.ToString("o")
            }
        };

        var response = await _s3Client.PutObjectAsync(putRequest);
        return response.HttpStatusCode == System.Net.HttpStatusCode.OK;
    }

    public async Task<EmbeddingInfo?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var s3Key = GetS3Key(key);

        try
        {
            var getRequest = new GetObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            };

            using var response = await _s3Client.GetObjectAsync(getRequest);
            using var reader = new StreamReader(response.ResponseStream);
            var json = await reader.ReadToEndAsync();

            var embeddingInfo = JsonSerializer.Deserialize<EmbeddingInfo>(json);
            return embeddingInfo;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task<bool> RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var s3Key = GetS3Key(key);

        try
        {
            var deleteRequest = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            };

            var response = await _s3Client.DeleteObjectAsync(deleteRequest);
            return response.HttpStatusCode == System.Net.HttpStatusCode.NoContent ||
                   response.HttpStatusCode == System.Net.HttpStatusCode.OK;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NoSuchKey")
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var s3Key = GetS3Key(key);

        try
        {
            var metadataRequest = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = s3Key
            };

            await _s3Client.GetObjectMetadataAsync(metadataRequest);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.ErrorCode == "NotFound")
        {
            return false;
        }
    }

    #endregion

    #region IVectorSearchStore Implementation

    /// <summary>
    /// Performs client-side vector similarity search.
    /// Note: This requires downloading all embeddings from S3, which may be slow and expensive for large datasets.
    /// For better performance, consider using Amazon OpenSearch Service with vector search capabilities.
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

        // List all objects with the prefix
        var listRequest = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = _keyPrefix
        };

        ListObjectsV2Response? listResponse;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            listResponse = await _s3Client.ListObjectsV2Async(listRequest, cancellationToken);

            foreach (var s3Object in listResponse.S3Objects)
            {
                if (!s3Object.Key.EndsWith(".json"))
                    continue;

                // Extract key from S3 key (remove prefix and .json extension)
                var key = s3Object.Key.Substring(_keyPrefix.Length, s3Object.Key.Length - _keyPrefix.Length - 5);
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

            listRequest.ContinuationToken = listResponse.NextContinuationToken;
        }
        while (listResponse.IsTruncated);

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

        // Upload objects in parallel (with reasonable concurrency limit)
        var semaphore = new SemaphoreSlim(10); // Limit to 10 concurrent uploads
        var tasks = items.Select(async item =>
        {
            var (key, embedding, checksum) = item;

            if (embedding.Count != _dimension)
                throw new ArgumentException($"Embedding dimension {embedding.Count} does not match expected {_dimension}");

            await semaphore.WaitAsync(cancellationToken);
            try
            {
                await StoreAsync(key, embedding, checksum);
            }
            finally
            {
                semaphore.Release();
            }
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

        // Download objects in parallel (with reasonable concurrency limit)
        var semaphore = new SemaphoreSlim(10); // Limit to 10 concurrent downloads
        var tasks = keyList.Select(async key =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await GetAsync(key);
            }
            finally
            {
                semaphore.Release();
            }
        });

        var embeddingInfos = await Task.WhenAll(tasks);
        return embeddingInfos.Where(x => x != null).Cast<EmbeddingInfo>().ToList();
    }

    #endregion
}
