using Hazina.Store.EmbeddingStore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector;

namespace Hazina.Store.PgVector;

/// <summary>
/// PostgreSQL + pgvector adapter implementing <see cref="IEmbeddingStore"/>,
/// <see cref="IVectorSearchStore"/>, and <see cref="IBatchEmbeddingStore"/>.
/// Uses pgvector's native distance operators for 10–100x faster similarity search
/// compared to in-memory computation. Suitable for production workloads with
/// millions of embeddings.
/// </summary>
/// <remarks>
/// Requires the pgvector extension installed in PostgreSQL:
/// <code>CREATE EXTENSION IF NOT EXISTS vector;</code>
/// The schema is initialized automatically on construction.
/// </remarks>
public sealed class PgVectorEmbeddingStore : IEmbeddingStore, IVectorSearchStore, IBatchEmbeddingStore
{
    private readonly string _connectionString;
    private readonly int _dimension;
    private readonly ILogger? _logger;
    private readonly object _indexLock = new();
    private bool _indexCreated;

    /// <summary>
    /// Initializes a new <see cref="PgVectorEmbeddingStore"/> and ensures the database schema exists.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="dimension">
    /// Embedding vector dimension. Must match the model used (e.g. 1536 for OpenAI text-embedding-ada-002).
    /// </param>
    /// <param name="logger">Optional logger for diagnostic output.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if <paramref name="connectionString"/> is null/empty or <paramref name="dimension"/> is non-positive.
    /// </exception>
    public PgVectorEmbeddingStore(string connectionString, int dimension = 1536, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

        if (dimension <= 0)
            throw new ArgumentException("Dimension must be a positive integer.", nameof(dimension));

        _connectionString = connectionString;
        _dimension = dimension;
        _logger = logger;

        // Register pgvector type mapping globally (idempotent)
#pragma warning disable CS0618 // GlobalTypeMapper is available in Npgsql 8
        NpgsqlConnection.GlobalTypeMapper.UseVector();
#pragma warning restore CS0618

        InitializeSchema();
    }

    /// <summary>
    /// Creates a HNSW or IVFFlat vector index for efficient approximate-nearest-neighbour search.
    /// Call after bulk ingestion. Safe to call multiple times — creates the index once.
    /// </summary>
    /// <param name="indexType">Index type: <c>"hnsw"</c> (default) or <c>"ivfflat"</c>.</param>
    /// <param name="lists">Number of lists for IVFFlat (ignored for HNSW). Typical value: 100.</param>
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

            using (var cmd = new NpgsqlCommand("DROP INDEX IF EXISTS hazina_embeddings_vector_idx;", conn))
                cmd.ExecuteNonQuery();

            var sql = indexType.ToUpperInvariant() switch
            {
                "IVFFLAT" => $@"
                    CREATE INDEX hazina_embeddings_vector_idx ON hazina_embeddings
                    USING ivfflat (embedding vector_cosine_ops)
                    WITH (lists = {lists});",

                "HNSW" => @"
                    CREATE INDEX hazina_embeddings_vector_idx ON hazina_embeddings
                    USING hnsw (embedding vector_cosine_ops);",

                _ => throw new ArgumentException($"Unknown index type '{indexType}'. Use 'hnsw' or 'ivfflat'.", nameof(indexType))
            };

            using (var cmd = new NpgsqlCommand(sql, conn))
                cmd.ExecuteNonQuery();

            _indexCreated = true;
            _logger?.LogInformation("Created {IndexType} vector index on hazina_embeddings", indexType);
        }
    }

    #region IEmbeddingStore

    /// <inheritdoc/>
    public async Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        if (embedding is null)
            throw new ArgumentNullException(nameof(embedding));

        if (embedding.Count != _dimension)
            throw new ArgumentException(
                $"Embedding dimension {embedding.Count} does not match expected {_dimension}.", nameof(embedding));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO hazina_embeddings (key, checksum, embedding, updated_at)
            VALUES (@key, @checksum, @embedding, CURRENT_TIMESTAMP)
            ON CONFLICT (key) DO UPDATE
            SET checksum   = EXCLUDED.checksum,
                embedding  = EXCLUDED.embedding,
                updated_at = CURRENT_TIMESTAMP", conn);

        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@checksum", checksum);
        cmd.Parameters.AddWithValue("@embedding", new Vector(ToFloatArray(embedding)));

        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);

        _logger?.LogDebug("Stored embedding for key '{Key}' (dim={Dim})", key, embedding.Count);
        return true;
    }

    /// <inheritdoc/>
    public async Task<EmbeddingInfo?> GetAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        await using var cmd = new NpgsqlCommand(@"
            SELECT key, checksum, embedding
            FROM hazina_embeddings
            WHERE key = @key", conn);

        cmd.Parameters.AddWithValue("@key", key);

        await using var reader = await cmd.ExecuteReaderAsync().ConfigureAwait(false);
        if (!await reader.ReadAsync().ConfigureAwait(false))
            return null;

        var k = reader.GetString(0);
        var cs = reader.GetString(1);
        var vec = reader.GetFieldValue<Vector>(2);
        return new EmbeddingInfo(k, cs, ToEmbedding(vec));
    }

    /// <inheritdoc/>
    public async Task<bool> RemoveAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        await using var cmd = new NpgsqlCommand(
            "DELETE FROM hazina_embeddings WHERE key = @key", conn);
        cmd.Parameters.AddWithValue("@key", key);

        var affected = await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
        return affected > 0;
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(string key)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Key cannot be null or empty.", nameof(key));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync().ConfigureAwait(false);

        await using var cmd = new NpgsqlCommand(
            "SELECT EXISTS(SELECT 1 FROM hazina_embeddings WHERE key = @key)", conn);
        cmd.Parameters.AddWithValue("@key", key);

        var result = await cmd.ExecuteScalarAsync().ConfigureAwait(false);
        return result is bool exists && exists;
    }

    #endregion

    #region IVectorSearchStore

    /// <summary>
    /// Performs native vector similarity search using pgvector's cosine distance operator (<c>&lt;=&gt;</c>).
    /// Significantly faster than loading all embeddings into memory.
    /// </summary>
    /// <inheritdoc/>
    public async Task<List<ScoredEmbedding>> SearchSimilarAsync(
        Embedding queryEmbedding,
        int topK = 10,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding is null)
            throw new ArgumentNullException(nameof(queryEmbedding));

        if (queryEmbedding.Count != _dimension)
            throw new ArgumentException(
                $"Query dimension {queryEmbedding.Count} does not match expected {_dimension}.", nameof(queryEmbedding));

        if (topK <= 0)
            throw new ArgumentException("topK must be a positive integer.", nameof(topK));

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        var queryVector = new Vector(ToFloatArray(queryEmbedding));

        // cosine_similarity = 1 - cosine_distance (pgvector <=> operator)
        await using var cmd = new NpgsqlCommand(@"
            SELECT
                key,
                checksum,
                embedding,
                1 - (embedding <=> @query) AS similarity
            FROM hazina_embeddings
            WHERE 1 - (embedding <=> @query) >= @minSimilarity
            ORDER BY embedding <=> @query
            LIMIT @topK", conn);

        cmd.Parameters.AddWithValue("@query", queryVector);
        cmd.Parameters.AddWithValue("@minSimilarity", minSimilarity);
        cmd.Parameters.AddWithValue("@topK", topK);

        var results = new List<ScoredEmbedding>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = reader.GetString(0);
            var checksum = reader.GetString(1);
            var vec = reader.GetFieldValue<Vector>(2);
            var similarity = reader.GetDouble(3);

            results.Add(new ScoredEmbedding
            {
                Info = new EmbeddingInfo(key, checksum, ToEmbedding(vec)),
                Similarity = similarity
            });
        }

        _logger?.LogDebug("Vector search returned {Count} results (topK={TopK}, minSim={MinSim:F2})",
            results.Count, topK, minSimilarity);

        return results;
    }

    #endregion

    #region IBatchEmbeddingStore

    /// <inheritdoc/>
    public async Task<int> StoreBatchAsync(
        IEnumerable<(string key, Embedding embedding, string checksum)> batch,
        CancellationToken cancellationToken = default)
    {
        if (batch is null)
            throw new ArgumentNullException(nameof(batch));

        var items = batch.ToList();
        if (items.Count == 0)
            return 0;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var transaction = await conn.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            foreach (var (key, embedding, checksum) in items)
            {
                if (embedding.Count != _dimension)
                    throw new ArgumentException(
                        $"Embedding dimension {embedding.Count} does not match expected {_dimension}.");

                await using var cmd = new NpgsqlCommand(@"
                    INSERT INTO hazina_embeddings (key, checksum, embedding, updated_at)
                    VALUES (@key, @checksum, @embedding, CURRENT_TIMESTAMP)
                    ON CONFLICT (key) DO UPDATE
                    SET checksum   = EXCLUDED.checksum,
                        embedding  = EXCLUDED.embedding,
                        updated_at = CURRENT_TIMESTAMP", conn, transaction);

                cmd.Parameters.AddWithValue("@key", key);
                cmd.Parameters.AddWithValue("@checksum", checksum);
                cmd.Parameters.AddWithValue("@embedding", new Vector(ToFloatArray(embedding)));

                await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogDebug("Batch stored {Count} embeddings", items.Count);
            return items.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<List<EmbeddingInfo>> GetBatchAsync(
        IEnumerable<string> keys,
        CancellationToken cancellationToken = default)
    {
        if (keys is null)
            throw new ArgumentNullException(nameof(keys));

        var keyList = keys.ToList();
        if (keyList.Count == 0)
            return new List<EmbeddingInfo>();

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var cmd = new NpgsqlCommand(@"
            SELECT key, checksum, embedding
            FROM hazina_embeddings
            WHERE key = ANY(@keys)", conn);

        cmd.Parameters.AddWithValue("@keys", keyList.ToArray());

        var results = new List<EmbeddingInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var key = reader.GetString(0);
            var cs = reader.GetString(1);
            var vec = reader.GetFieldValue<Vector>(2);
            results.Add(new EmbeddingInfo(key, cs, ToEmbedding(vec)));
        }

        return results;
    }

    #endregion

    #region Schema

    private void InitializeSchema()
    {
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();

        using (var cmd = new NpgsqlCommand("CREATE EXTENSION IF NOT EXISTS vector;", conn))
            cmd.ExecuteNonQuery();

        using (var cmd = new NpgsqlCommand($@"
            CREATE TABLE IF NOT EXISTS hazina_embeddings (
                key        TEXT PRIMARY KEY,
                checksum   TEXT        NOT NULL,
                embedding  VECTOR({_dimension}) NOT NULL,
                created_at TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP   NOT NULL DEFAULT CURRENT_TIMESTAMP
            );", conn))
        {
            cmd.ExecuteNonQuery();
        }

        _logger?.LogInformation("PgVector schema initialized (dim={Dimension})", _dimension);
    }

    #endregion

    #region Helpers

    private static float[] ToFloatArray(Embedding embedding) =>
        embedding.Select(d => (float)d).ToArray();

    private static Embedding ToEmbedding(Vector vec) =>
        new(vec.ToArray().Select(f => (double)f));

    #endregion
}
