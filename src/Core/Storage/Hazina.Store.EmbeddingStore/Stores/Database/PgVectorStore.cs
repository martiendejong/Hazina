using Npgsql;
using Pgvector;

namespace DevGPT.Store.EmbeddingStore;

/// <summary>
/// PostgreSQL + pgvector implementation with native vector search support.
/// Implements efficient similarity search using pgvector's distance operators.
/// This is the refactored version replacing PgVectorTextEmbeddingStore.
/// </summary>
public class PgVectorStore : IEmbeddingStore, IVectorSearchStore, IBatchEmbeddingStore
{
    private readonly string _connectionString;
    private readonly int _dimension;
    private readonly object _indexLock = new object();
    private bool _indexCreated = false;

    /// <summary>
    /// Creates a new PgVectorStore.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="dimension">Embedding vector dimension (e.g., 1536 for OpenAI)</param>
    public PgVectorStore(string connectionString, int dimension = 1536)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty", nameof(connectionString));

        if (dimension <= 0)
            throw new ArgumentException("Dimension must be positive", nameof(dimension));

        _connectionString = connectionString;
        _dimension = dimension;

        // Register pgvector type globally
        NpgsqlConnection.GlobalTypeMapper.UseVector();

        // Initialize schema
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        // Enable pgvector extension
        using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", conn))
        {
            cmd.ExecuteNonQuery();
        }

        // Create embeddings table
        using (var cmd = new NpgsqlCommand($@"
            CREATE TABLE IF NOT EXISTS embeddings (
                key TEXT PRIMARY KEY,
                checksum TEXT NOT NULL,
                embedding VECTOR({_dimension}) NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            );", conn))
        {
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Creates a vector index for efficient similarity search.
    /// Call this after bulk ingestion for optimal search performance.
    /// </summary>
    /// <param name="indexType">Type of index: "ivfflat" or "hnsw"</param>
    /// <param name="lists">Number of lists for IVFFlat (ignored for HNSW)</param>
    public async Task CreateIndexAsync(string indexType = "hnsw", int lists = 100)
    {
        if (_indexCreated)
            return;

        lock (_indexLock)
        {
            if (_indexCreated)
                return;

            using var conn = new NpgsqlConnection(_connectionString);
            conn.Open();

            // Drop existing index if present
            using (var cmd = new NpgsqlCommand(@"
                DROP INDEX IF EXISTS embeddings_embedding_idx;", conn))
            {
                cmd.ExecuteNonQuery();
            }

            // Create appropriate index
            string createIndexSql = indexType.ToLower() switch
            {
                "ivfflat" => $@"
                    CREATE INDEX embeddings_embedding_idx ON embeddings
                    USING ivfflat (embedding vector_cosine_ops)
                    WITH (lists = {lists});",

                "hnsw" => $@"
                    CREATE INDEX embeddings_embedding_idx ON embeddings
                    USING hnsw (embedding vector_cosine_ops);",

                _ => throw new ArgumentException($"Unknown index type: {indexType}")
            };

            using (var cmd = new NpgsqlCommand(createIndexSql, conn))
            {
                cmd.ExecuteNonQuery();
            }

            _indexCreated = true;
        }
    }

    #region IEmbeddingStore Implementation

    public async Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        if (embedding == null)
            throw new ArgumentNullException(nameof(embedding));

        if (embedding.Count != _dimension)
            throw new ArgumentException($"Embedding dimension {embedding.Count} does not match expected {_dimension}");

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO embeddings (key, checksum, embedding, updated_at)
            VALUES (@key, @checksum, @embedding, CURRENT_TIMESTAMP)
            ON CONFLICT (key) DO UPDATE
            SET checksum = EXCLUDED.checksum,
                embedding = EXCLUDED.embedding,
                updated_at = CURRENT_TIMESTAMP", conn);

        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@checksum", checksum);
        var floats = embedding.Select(d => (float)d).ToArray();
        cmd.Parameters.AddWithValue("@embedding", new Vector(floats));

        await cmd.ExecuteNonQueryAsync();
        return true;
    }

    public async Task<EmbeddingInfo?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT key, checksum, embedding
            FROM embeddings
            WHERE key = @key", conn);

        cmd.Parameters.AddWithValue("@key", key);

        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var k = reader.GetString(0);
            var checksum = reader.GetString(1);
            var vec = reader.GetFieldValue<Vector>(2);
            var doubles = vec.ToArray().Select(f => (double)f).ToArray();
            return new EmbeddingInfo(k, checksum, new Embedding(doubles));
        }

        return null;
    }

    public async Task<bool> RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            DELETE FROM embeddings
            WHERE key = @key", conn);

        cmd.Parameters.AddWithValue("@key", key);

        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    public async Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty", nameof(key));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new NpgsqlCommand(@"
            SELECT EXISTS(SELECT 1 FROM embeddings WHERE key = @key)", conn);

        cmd.Parameters.AddWithValue("@key", key);

        var result = await cmd.ExecuteScalarAsync();
        return result is bool exists && exists;
    }

    #endregion

    #region IVectorSearchStore Implementation

    /// <summary>
    /// Performs native vector similarity search using pgvector's cosine distance operator.
    /// This is 10-100x faster than loading all embeddings and computing similarity in-memory.
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

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        var queryVector = new Vector(queryEmbedding.Select(d => (float)d).ToArray());

        // Use pgvector's <=> operator for cosine distance
        // cosine_similarity = 1 - cosine_distance
        await using var cmd = new NpgsqlCommand($@"
            SELECT
                key,
                checksum,
                embedding,
                1 - (embedding <=> @query) AS similarity
            FROM embeddings
            WHERE 1 - (embedding <=> @query) >= @minSimilarity
            ORDER BY embedding <=> @query
            LIMIT @topK", conn);

        cmd.Parameters.AddWithValue("@query", queryVector);
        cmd.Parameters.AddWithValue("@minSimilarity", minSimilarity);
        cmd.Parameters.AddWithValue("@topK", topK);

        var results = new List<ScoredEmbedding>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(0);
            var checksum = reader.GetString(1);
            var vec = reader.GetFieldValue<Vector>(2);
            var similarity = reader.GetDouble(3);

            var embedding = new Embedding(vec.ToArray().Select(f => (double)f));
            results.Add(new ScoredEmbedding
            {
                Info = new EmbeddingInfo(key, checksum, embedding),
                Similarity = similarity
            });
        }

        return results;
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

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        // Use a transaction for batch insert
        await using var transaction = await conn.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var (key, embedding, checksum) in items)
            {
                if (embedding.Count != _dimension)
                    throw new ArgumentException($"Embedding dimension {embedding.Count} does not match expected {_dimension}");

                await using var cmd = new NpgsqlCommand(@"
                    INSERT INTO embeddings (key, checksum, embedding, updated_at)
                    VALUES (@key, @checksum, @embedding, CURRENT_TIMESTAMP)
                    ON CONFLICT (key) DO UPDATE
                    SET checksum = EXCLUDED.checksum,
                        embedding = EXCLUDED.embedding,
                        updated_at = CURRENT_TIMESTAMP", conn, transaction);

                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@checksum", checksum);
                var floats = embedding.Select(d => (float)d).ToArray();
                cmd.Parameters.AddWithValue("@embedding", new Vector(floats));

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return items.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
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

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = new NpgsqlCommand(@"
            SELECT key, checksum, embedding
            FROM embeddings
            WHERE key = ANY(@keys)", conn);

        cmd.Parameters.AddWithValue("@keys", keyList.ToArray());

        var results = new List<EmbeddingInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var key = reader.GetString(0);
            var checksum = reader.GetString(1);
            var vec = reader.GetFieldValue<Vector>(2);
            var doubles = vec.ToArray().Select(f => (double)f).ToArray();
            results.Add(new EmbeddingInfo(key, checksum, new Embedding(doubles)));
        }

        return results;
    }

    #endregion
}
