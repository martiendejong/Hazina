using Npgsql;
using Pgvector;

/// <summary>
/// Legacy PostgreSQL + pgvector embedding store.
/// </summary>
/// <remarks>
/// This class is obsolete. Use PgVectorStore instead for:
/// - Better separation of concerns (no embedding generation in storage)
/// - Native vector search with pgvector operators (10-100x faster)
/// - No memory overhead from caching all embeddings
/// - Support for vector indices (HNSW, IVFFlat)
/// </remarks>
[Obsolete("Use PgVectorStore with EmbeddingService instead. See Store/EmbeddingStore/PgVectorStore.cs")]
public class PgVectorTextEmbeddingStore : AbstractTextEmbeddingStore, ITextEmbeddingStore
{
    private readonly string _connectionString;
    private readonly int _dimension;
    private readonly List<EmbeddingInfo> _embeddings = new();

    public PgVectorTextEmbeddingStore(string connectionString, ILLMClient embeddingProvider, int dimension = 1536) : base(embeddingProvider)
    {
        _connectionString = connectionString;
        _dimension = dimension;

        // Ensure vector type is registered
        NpgsqlConnection.GlobalTypeMapper.UseVector();

        // Ensure schema and warm local cache
        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using (var cmd = new NpgsqlCommand(@"CREATE EXTENSION IF NOT EXISTS vector;", conn))
            cmd.ExecuteNonQuery();

        using (var cmd = new NpgsqlCommand($@"
            CREATE TABLE IF NOT EXISTS embeddings (
                key TEXT PRIMARY KEY,
                checksum TEXT NOT NULL,
                embedding VECTOR({_dimension}) NOT NULL
            );", conn))
            cmd.ExecuteNonQuery();

        LoadAll(conn);
    }

    private void LoadAll(NpgsqlConnection existingConn)
    {
        _embeddings.Clear();
        using var cmd = new NpgsqlCommand("SELECT key, checksum, embedding FROM embeddings", existingConn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var key = reader.GetString(0);
            var checksum = reader.GetString(1);
            var vec = reader.GetFieldValue<Vector>(2);
            var doubles = vec.ToArray().Select(f => (double)f).ToArray();
            _embeddings.Add(new EmbeddingInfo(key, checksum, new Embedding(doubles)));
        }
    }

    public override EmbeddingInfo[] Embeddings => _embeddings.ToArray();

    public override async Task<EmbeddingInfo?> GetEmbedding(string key)
    {
        var existing = _embeddings.FirstOrDefault(e => e.Key == key);
        if (existing != null) return existing;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT key, checksum, embedding FROM embeddings WHERE key = @key", conn);
        cmd.Parameters.AddWithValue("@key", key);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var k = reader.GetString(0);
            var checksum = reader.GetString(1);
            var vec = reader.GetFieldValue<Vector>(2);
            var doubles = vec.ToArray().Select(f => (double)f).ToArray();
            var info = new EmbeddingInfo(k, checksum, new Embedding(doubles));
            var idx = _embeddings.FindIndex(e => e.Key == k);
            if (idx >= 0) _embeddings[idx] = info; else _embeddings.Add(info);
            return info;
        }
        return null;
    }

    public override async Task<bool> RemoveEmbedding(string key)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM embeddings WHERE key = @key", conn);
        cmd.Parameters.AddWithValue("@key", key);
        var affected = await cmd.ExecuteNonQueryAsync();
        var local = _embeddings.FindIndex(x => x.Key == key);
        if (local >= 0) _embeddings.RemoveAt(local);
        return affected > 0;
    }

    protected override async Task UpdateEmbedding(EmbeddingInfo embedding)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"UPDATE embeddings
                                                  SET checksum = @checksum,
                                                      embedding = @embedding
                                                  WHERE key = @key", conn);
        cmd.Parameters.AddWithValue("@key", embedding.Key);
        cmd.Parameters.AddWithValue("@checksum", embedding.Checksum);
        var floats = embedding.Data.Select(d => (float)d).ToArray();
        cmd.Parameters.AddWithValue("@embedding", new Vector(floats));
        await cmd.ExecuteNonQueryAsync();

        var idx = _embeddings.FindIndex(e => e.Key == embedding.Key);
        if (idx >= 0) _embeddings[idx] = embedding; else _embeddings.Add(embedding);
    }

    protected override async Task AddEmbedding(EmbeddingInfo embeddingInfo)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"INSERT INTO embeddings (key, checksum, embedding)
                                                  VALUES (@key, @checksum, @embedding)
                                                  ON CONFLICT (key) DO UPDATE
                                                  SET checksum = EXCLUDED.checksum,
                                                      embedding = EXCLUDED.embedding", conn);
        cmd.Parameters.AddWithValue("@key", embeddingInfo.Key);
        cmd.Parameters.AddWithValue("@checksum", embeddingInfo.Checksum);
        var floats = embeddingInfo.Data.Select(d => (float)d).ToArray();
        cmd.Parameters.AddWithValue("@embedding", new Vector(floats));
        await cmd.ExecuteNonQueryAsync();

        var idx = _embeddings.FindIndex(e => e.Key == embeddingInfo.Key);
        if (idx >= 0) _embeddings[idx] = embeddingInfo; else _embeddings.Add(embeddingInfo);
    }
}
