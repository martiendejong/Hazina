using Hazina.Store.EmbeddingStore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Buffers.Binary;

namespace Hazina.Store.Sqlite;

/// <summary>
/// SQLite-based implementation of IEmbeddingStore and IVectorSearchStore.
/// Stores embeddings as BLOBs in SQLite for persistence.
/// Performs vector similarity search in-memory (loads all embeddings for search).
/// For datasets >100K embeddings, consider using PgVector or a dedicated vector database.
/// </summary>
public class SqliteEmbeddingStore : IEmbeddingStore, IVectorSearchStore
{
    private readonly string _connectionString;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteEmbeddingStore(string connectionString, ILogger? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger;

        // Initialize schema
        var schema = new SqliteSchema(connectionString, logger);
        schema.InitializeAsync().GetAwaiter().GetResult();
    }

    #region IEmbeddingStore Implementation

    public async Task<bool> StoreAsync(string key, Embedding embedding, string checksum)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Serialize embedding to BLOB
            var blob = SerializeEmbedding(embedding);

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT OR REPLACE INTO embeddings (item_id, model, dimension, vector, checksum, created_at)
                VALUES (@key, @model, @dimension, @vector, @checksum, datetime('now'));
            ";

            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@model", "unknown"); // Embedding doesn't have Model property
            cmd.Parameters.AddWithValue("@dimension", embedding.Count);
            cmd.Parameters.AddWithValue("@vector", blob);
            cmd.Parameters.AddWithValue("@checksum", checksum);

            await cmd.ExecuteNonQueryAsync();
            _logger?.LogDebug("Stored embedding for key '{Key}' (dimension: {Dim})",
                key, embedding.Count);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to store embedding for key '{Key}'", key);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<EmbeddingInfo?> GetAsync(string key)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT item_id, model, vector, checksum
            FROM embeddings
            WHERE item_id = @key
            LIMIT 1;
        ";

        cmd.Parameters.AddWithValue("@key", key);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        var itemId = reader.GetString(0);
        var model = reader.GetString(1);
        var blob = (byte[])reader.GetValue(2);
        var checksum = reader.GetString(3);

        var embedding = DeserializeEmbedding(blob);

        return new EmbeddingInfo(itemId, checksum, embedding);
    }

    public async Task<bool> RemoveAsync(string key)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM embeddings WHERE item_id = @key;";
            cmd.Parameters.AddWithValue("@key", key);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            _logger?.LogDebug("Removed {Count} embedding(s) for key '{Key}'", rowsAffected, key);

            return rowsAffected > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> ExistsAsync(string key)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM embeddings WHERE item_id = @key;";
        cmd.Parameters.AddWithValue("@key", key);

        var count = (long?)await cmd.ExecuteScalarAsync() ?? 0;
        return count > 0;
    }

    #endregion

    #region IVectorSearchStore Implementation

    public async Task<List<ScoredEmbedding>> SearchSimilarAsync(
        Embedding queryEmbedding,
        int topK = 10,
        double minSimilarity = 0.0,
        CancellationToken cancellationToken = default)
    {
        // Load all embeddings into memory
        var allEmbeddings = await LoadAllEmbeddingsAsync(cancellationToken);

        if (allEmbeddings.Count == 0)
        {
            _logger?.LogWarning("No embeddings found in database for similarity search");
            return new List<ScoredEmbedding>();
        }

        // Calculate cosine similarity for each
        var scored = new List<ScoredEmbedding>();
        foreach (var embInfo in allEmbeddings)
        {
            var similarity = queryEmbedding.CosineSimilarity(embInfo.Data);

            if (similarity >= minSimilarity)
            {
                scored.Add(new ScoredEmbedding
                {
                    Info = embInfo,
                    Similarity = similarity
                });
            }
        }

        // Sort by similarity descending and take top K
        var results = scored
            .OrderByDescending(x => x.Similarity)
            .Take(topK)
            .ToList();

        _logger?.LogDebug("Vector search: found {Count} results above threshold {Threshold:F2} (top {TopK})",
            scored.Count, minSimilarity, topK);

        return results;
    }

    #endregion

    #region Helper Methods

    private async Task<List<EmbeddingInfo>> LoadAllEmbeddingsAsync(CancellationToken cancellationToken)
    {
        var embeddings = new List<EmbeddingInfo>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT item_id, model, vector, checksum FROM embeddings;";

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var itemId = reader.GetString(0);
            var model = reader.GetString(1);
            var blob = (byte[])reader.GetValue(2);
            var checksum = reader.GetString(3);

            var embedding = DeserializeEmbedding(blob);

            embeddings.Add(new EmbeddingInfo(itemId, checksum, embedding));
        }

        return embeddings;
    }

    /// <summary>
    /// Serializes Embedding (List<double>) to byte array (little-endian).
    /// Format: [double1][double2][double3]...
    /// </summary>
    private byte[] SerializeEmbedding(Embedding embedding)
    {
        var bytes = new byte[embedding.Count * sizeof(double)];
        for (int i = 0; i < embedding.Count; i++)
        {
            BinaryPrimitives.WriteDoubleLittleEndian(bytes.AsSpan(i * sizeof(double)), embedding[i]);
        }
        return bytes;
    }

    /// <summary>
    /// Deserializes byte array to Embedding (List<double>) (little-endian).
    /// </summary>
    private Embedding DeserializeEmbedding(byte[] blob)
    {
        var doubleCount = blob.Length / sizeof(double);
        var values = new List<double>(doubleCount);
        for (int i = 0; i < doubleCount; i++)
        {
            values.Add(BinaryPrimitives.ReadDoubleLittleEndian(blob.AsSpan(i * sizeof(double))));
        }
        return new Embedding(values);
    }

    #endregion
}
