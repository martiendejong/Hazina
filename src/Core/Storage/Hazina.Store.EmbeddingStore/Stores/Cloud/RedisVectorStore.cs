using StackExchange.Redis;
using System.Text.Json;

namespace Hazina.Store.EmbeddingStore;

/// <summary>
/// Redis-based vector store implementation with optional RediSearch vector similarity support.
/// Provides fast in-memory storage with persistence and distributed caching capabilities.
/// </summary>
/// <remarks>
/// Supports two modes:
/// - Basic mode: Uses Redis hashes for storage with client-side similarity search
/// - RediSearch mode: Uses RediSearch vector similarity for server-side search (requires RediSearch module)
/// </remarks>
public class RedisVectorStore : IEmbeddingStore, IVectorSearchStore, IBatchEmbeddingStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _db;
    private readonly string _keyPrefix;
    private readonly int _dimension;
    private readonly bool _useRediSearch;
    private readonly string _indexName;

    /// <summary>
    /// Creates a new RedisVectorStore.
    /// </summary>
    /// <param name="connectionString">Redis connection string (e.g., "localhost:6379")</param>
    /// <param name="dimension">Embedding vector dimension (e.g., 1536 for OpenAI)</param>
    /// <param name="keyPrefix">Prefix for all Redis keys (default: "embedding:")</param>
    /// <param name="database">Redis database number (default: 0)</param>
    /// <param name="useRediSearch">Enable RediSearch vector similarity (requires RediSearch module)</param>
    /// <param name="indexName">RediSearch index name (default: "embeddings_idx")</param>
    public RedisVectorStore(
        string connectionString,
        int dimension = 1536,
        string keyPrefix = "embedding:",
        int database = 0,
        bool useRediSearch = false,
        string indexName = "embeddings_idx")
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

        if (dimension <= 0)
            throw new ArgumentException("Dimension must be positive", nameof(dimension));

        _redis = ConnectionMultiplexer.Connect(connectionString);
        _db = _redis.GetDatabase(database);
        _keyPrefix = keyPrefix;
        _dimension = dimension;
        _useRediSearch = useRediSearch;
        _indexName = indexName;

        if (_useRediSearch)
        {
            InitializeRediSearchIndex();
        }
    }

    /// <summary>
    /// Creates a RedisVectorStore with an existing connection multiplexer.
    /// </summary>
    public RedisVectorStore(
        IConnectionMultiplexer redis,
        int dimension = 1536,
        string keyPrefix = "embedding:",
        int database = 0,
        bool useRediSearch = false,
        string indexName = "embeddings_idx")
    {
        if (redis == null)
            throw new ArgumentNullException(nameof(redis));

        if (dimension <= 0)
            throw new ArgumentException("Dimension must be positive", nameof(dimension));

        _redis = redis;
        _db = _redis.GetDatabase(database);
        _keyPrefix = keyPrefix;
        _dimension = dimension;
        _useRediSearch = useRediSearch;
        _indexName = indexName;

        if (_useRediSearch)
        {
            InitializeRediSearchIndex();
        }
    }

    private void InitializeRediSearchIndex()
    {
        // RediSearch index creation would go here
        // Note: This requires RediSearch module and FT.CREATE command
        // Implementation omitted as it requires additional RediSearch-specific logic
    }

    private string GetRedisKey(string key) => $"{_keyPrefix}{key}";

    #region IEmbeddingStore Implementation

    public async Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (embedding == null)
            throw new ArgumentNullException(nameof(embedding));

        if (embedding.Count != _dimension)
            throw new ArgumentException($"Embedding dimension {embedding.Count} does not match expected {_dimension}");

        var redisKey = GetRedisKey(key);
        var embeddingInfo = new EmbeddingInfo(key, checksum, embedding);

        // Serialize to JSON and store as Redis hash
        var hashEntries = new HashEntry[]
        {
            new HashEntry("key", key),
            new HashEntry("checksum", checksum),
            new HashEntry("embedding", JsonSerializer.Serialize(embedding.ToArray())),
            new HashEntry("dimension", _dimension),
            new HashEntry("updated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await _db.HashSetAsync(redisKey, hashEntries);
        return true;
    }

    public async Task<EmbeddingInfo?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var redisKey = GetRedisKey(key);
        var hashEntries = await _db.HashGetAllAsync(redisKey);

        if (hashEntries.Length == 0)
            return null;

        var hash = hashEntries.ToDictionary(
            x => x.Name.ToString(),
            x => x.Value.ToString()
        );

        if (!hash.ContainsKey("key") || !hash.ContainsKey("checksum") || !hash.ContainsKey("embedding"))
            return null;

        var embeddingArray = JsonSerializer.Deserialize<double[]>(hash["embedding"]);
        if (embeddingArray == null)
            return null;

        return new EmbeddingInfo(
            hash["key"],
            hash["checksum"],
            new Embedding(embeddingArray)
        );
    }

    public async Task<bool> RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var redisKey = GetRedisKey(key);
        return await _db.KeyDeleteAsync(redisKey);
    }

    public async Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        var redisKey = GetRedisKey(key);
        return await _db.KeyExistsAsync(redisKey);
    }

    #endregion

    #region IVectorSearchStore Implementation

    /// <summary>
    /// Performs vector similarity search.
    /// Uses RediSearch if enabled, otherwise falls back to client-side search.
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

        if (_useRediSearch)
        {
            // RediSearch vector similarity would be used here
            // Requires FT.SEARCH command with vector similarity
            throw new NotImplementedException("RediSearch vector similarity not yet implemented. Use useRediSearch=false for client-side search.");
        }

        // Fallback to client-side similarity search
        return await ClientSideSimilaritySearchAsync(queryEmbedding, topK, minSimilarity, cancellationToken);
    }

    private async Task<List<ScoredEmbedding>> ClientSideSimilaritySearchAsync(
        Embedding queryEmbedding,
        int topK,
        double minSimilarity,
        CancellationToken cancellationToken)
    {
        var results = new List<ScoredEmbedding>();

        // Scan all embedding keys
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var pattern = $"{_keyPrefix}*";

        await foreach (var redisKey in server.KeysAsync(pattern: pattern))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var embeddingInfo = await GetAsync(redisKey.ToString().Substring(_keyPrefix.Length));
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

        // Use Redis transaction for batch insert
        var transaction = _db.CreateTransaction();
        var tasks = new List<Task<bool>>();

        foreach (var (key, embedding, checksum) in items)
        {
            if (embedding.Count != _dimension)
                throw new ArgumentException($"Embedding dimension {embedding.Count} does not match expected {_dimension}");

            var redisKey = GetRedisKey(key);
            var hashEntries = new HashEntry[]
            {
                new HashEntry("key", key),
                new HashEntry("checksum", checksum),
                new HashEntry("embedding", JsonSerializer.Serialize(embedding.ToArray())),
                new HashEntry("dimension", _dimension),
                new HashEntry("updated_at", DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            tasks.Add(transaction.HashSetAsync(redisKey, hashEntries).ContinueWith(_ => true));
        }

        var committed = await transaction.ExecuteAsync();
        if (!committed)
            throw new InvalidOperationException("Redis transaction failed to commit");

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

        var results = new List<EmbeddingInfo>();
        var tasks = keyList.Select(async key =>
        {
            var embeddingInfo = await GetAsync(key);
            return embeddingInfo;
        });

        var embeddingInfos = await Task.WhenAll(tasks);
        return embeddingInfos.Where(x => x != null).Cast<EmbeddingInfo>().ToList();
    }

    #endregion

    public void Dispose()
    {
        _redis?.Dispose();
    }
}
