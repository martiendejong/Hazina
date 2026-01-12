using Hazina.Store.FactsStore.Interfaces;
using Hazina.Store.FactsStore.Models;
using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Json;

namespace Hazina.Store.FactsStore.Implementations;

/// <summary>
/// SQLite implementation of IFactsStore.
/// Stores facts with optional embeddings for semantic search.
/// Uses JSON for metadata and tags storage.
/// </summary>
public class SqliteFactsStore : IFactsStore, IDisposable
{
    private readonly string _connectionString;
    private readonly SqliteConnection _connection;
    private bool _initialized = false;

    /// <summary>
    /// Creates a new SQLite facts store
    /// </summary>
    /// <param name="databasePath">Path to the SQLite database file</param>
    public SqliteFactsStore(string databasePath)
    {
        _connectionString = $"Data Source={databasePath}";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
    }

    /// <summary>
    /// Initialize database schema
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS Facts (
                Id TEXT PRIMARY KEY,
                Content TEXT NOT NULL,
                Type TEXT NOT NULL,
                MetadataJson TEXT,
                TagsJson TEXT,
                Created TEXT NOT NULL,
                Updated TEXT,
                Embedding BLOB,
                RelevanceScore REAL DEFAULT 1.0
            );

            CREATE INDEX IF NOT EXISTS idx_facts_type ON Facts(Type);
            CREATE INDEX IF NOT EXISTS idx_facts_relevance ON Facts(RelevanceScore DESC);
            CREATE INDEX IF NOT EXISTS idx_facts_created ON Facts(Created DESC);
        ";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = createTableSql;
        await cmd.ExecuteNonQueryAsync();

        _initialized = true;
    }

    /// <inheritdoc />
    public async Task<string> AddAsync(Fact fact, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Facts (Id, Content, Type, MetadataJson, TagsJson, Created, Updated, Embedding, RelevanceScore)
            VALUES (@id, @content, @type, @metadata, @tags, @created, @updated, @embedding, @relevance)
        ";

        AddFactParameters(cmd, fact);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        return fact.Id;
    }

    /// <inheritdoc />
    public async Task<List<string>> AddBatchAsync(List<Fact> facts, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        using var transaction = _connection.BeginTransaction();
        try
        {
            foreach (var fact in facts)
            {
                using var cmd = _connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO Facts (Id, Content, Type, MetadataJson, TagsJson, Created, Updated, Embedding, RelevanceScore)
                    VALUES (@id, @content, @type, @metadata, @tags, @created, @updated, @embedding, @relevance)
                ";

                AddFactParameters(cmd, fact);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
            return facts.Select(f => f.Id).ToList();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Fact fact, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        fact.Updated = DateTime.UtcNow;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE Facts
            SET Content = @content,
                Type = @type,
                MetadataJson = @metadata,
                TagsJson = @tags,
                Updated = @updated,
                Embedding = @embedding,
                RelevanceScore = @relevance
            WHERE Id = @id
        ";

        AddFactParameters(cmd, fact);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);

        return affected > 0;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Facts WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0;
    }

    /// <inheritdoc />
    public async Task<Fact?> GetByIdAsync(string id, bool includeEmbedding = false, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        var selectColumns = includeEmbedding
            ? "Id, Content, Type, MetadataJson, TagsJson, Created, Updated, Embedding, RelevanceScore"
            : "Id, Content, Type, MetadataJson, TagsJson, Created, Updated, NULL as Embedding, RelevanceScore";

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT {selectColumns} FROM Facts WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadFact(reader);
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<List<Fact>> QueryAsync(FactQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        var sqlBuilder = new StringBuilder();
        var selectColumns = query.IncludeEmbeddings
            ? "Id, Content, Type, MetadataJson, TagsJson, Created, Updated, Embedding, RelevanceScore"
            : "Id, Content, Type, MetadataJson, TagsJson, Created, Updated, NULL as Embedding, RelevanceScore";

        sqlBuilder.Append($"SELECT {selectColumns} FROM Facts WHERE 1=1");

        using var cmd = _connection.CreateCommand();

        // Filter by IDs
        if (query.Ids != null && query.Ids.Any())
        {
            var idParams = string.Join(",", query.Ids.Select((_, i) => $"@id{i}"));
            sqlBuilder.Append($" AND Id IN ({idParams})");
            for (int i = 0; i < query.Ids.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@id{i}", query.Ids[i]);
            }
        }

        // Filter by types
        if (query.Types != null && query.Types.Any())
        {
            var typeParams = string.Join(",", query.Types.Select((_, i) => $"@type{i}"));
            sqlBuilder.Append($" AND Type IN ({typeParams})");
            for (int i = 0; i < query.Types.Count; i++)
            {
                cmd.Parameters.AddWithValue($"@type{i}", query.Types[i]);
            }
        }

        // Filter by tags (simple contains check - for production use FTS or JSON operations)
        if (query.Tags != null && query.Tags.Any())
        {
            for (int i = 0; i < query.Tags.Count; i++)
            {
                sqlBuilder.Append($" AND TagsJson LIKE @tag{i}");
                cmd.Parameters.AddWithValue($"@tag{i}", $"%\"{query.Tags[i]}\"%");
            }
        }

        // Filter by metadata (simple contains check)
        if (query.Metadata != null && query.Metadata.Any())
        {
            int metaIndex = 0;
            foreach (var kvp in query.Metadata)
            {
                sqlBuilder.Append($" AND MetadataJson LIKE @metaKey{metaIndex} AND MetadataJson LIKE @metaVal{metaIndex}");
                cmd.Parameters.AddWithValue($"@metaKey{metaIndex}", $"%\"{kvp.Key}\"%");
                cmd.Parameters.AddWithValue($"@metaVal{metaIndex}", $"%\"{kvp.Value}\"%");
                metaIndex++;
            }
        }

        // Order by relevance score and limit
        sqlBuilder.Append(" ORDER BY RelevanceScore DESC, Created DESC LIMIT @limit");
        cmd.Parameters.AddWithValue("@limit", query.TopK);

        cmd.CommandText = sqlBuilder.ToString();

        var facts = new List<Fact>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facts.Add(ReadFact(reader));
        }

        // If semantic search requested and embeddings available, compute similarity
        if (query.QueryEmbedding != null)
        {
            facts = facts
                .Select(f =>
                {
                    if (f.Embedding != null)
                    {
                        f.RelevanceScore = ComputeCosineSimilarity(query.QueryEmbedding, f.Embedding);
                    }
                    return f;
                })
                .Where(f => f.RelevanceScore >= query.MinSimilarity)
                .OrderByDescending(f => f.RelevanceScore)
                .Take(query.TopK)
                .ToList();
        }

        return facts;
    }

    /// <inheritdoc />
    public async Task<List<Fact>> GetByTagsAsync(List<string> tags, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        var query = new FactQuery
        {
            Tags = tags,
            TopK = maxResults
        };
        return await QueryAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<List<Fact>> GetByTypeAsync(string type, int maxResults = 100, CancellationToken cancellationToken = default)
    {
        var query = new FactQuery
        {
            Types = new List<string> { type },
            TopK = maxResults
        };
        return await QueryAsync(query, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Facts";

        var result = await cmd.ExecuteScalarAsync(cancellationToken);
        return result != null ? Convert.ToInt64(result) : 0;
    }

    /// <inheritdoc />
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Facts";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task EnsureInitializedAsync()
    {
        if (!_initialized)
        {
            await InitializeAsync();
        }
    }

    private void AddFactParameters(SqliteCommand cmd, Fact fact)
    {
        cmd.Parameters.AddWithValue("@id", fact.Id);
        cmd.Parameters.AddWithValue("@content", fact.Content);
        cmd.Parameters.AddWithValue("@type", fact.Type);
        cmd.Parameters.AddWithValue("@metadata", fact.Metadata != null ? JsonSerializer.Serialize(fact.Metadata) : "{}");
        cmd.Parameters.AddWithValue("@tags", fact.Tags != null ? JsonSerializer.Serialize(fact.Tags) : "[]");
        cmd.Parameters.AddWithValue("@created", fact.Created.ToString("o"));
        cmd.Parameters.AddWithValue("@updated", fact.Updated?.ToString("o") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@embedding", fact.Embedding != null ? SerializeEmbedding(fact.Embedding) : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@relevance", fact.RelevanceScore);
    }

    private Fact ReadFact(SqliteDataReader reader)
    {
        var fact = new Fact
        {
            Id = reader.GetString(0),
            Content = reader.GetString(1),
            Type = reader.GetString(2),
            Metadata = reader.IsDBNull(3) ? new Dictionary<string, string>() : JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(3)) ?? new Dictionary<string, string>(),
            Tags = reader.IsDBNull(4) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(reader.GetString(4)) ?? new List<string>(),
            Created = DateTime.Parse(reader.GetString(5)),
            Updated = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)),
            Embedding = reader.IsDBNull(7) ? null : DeserializeEmbedding((byte[])reader.GetValue(7)),
            RelevanceScore = reader.GetDouble(8)
        };

        return fact;
    }

    private byte[] SerializeEmbedding(float[] embedding)
    {
        var bytes = new byte[embedding.Length * sizeof(float)];
        Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private float[] DeserializeEmbedding(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private double ComputeCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Embedding dimensions must match");

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }

    /// <summary>
    /// Disposes the database connection
    /// </summary>
    public void Dispose()
    {
        _connection?.Dispose();
    }
}
