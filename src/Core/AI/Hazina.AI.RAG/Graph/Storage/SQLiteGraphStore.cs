namespace Hazina.AI.RAG.Graph.Storage;

using System.Data;
using System.Text;
using System.Text.Json;
using Hazina.AI.RAG.Graph.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

/// <summary>
/// SQLite-based graph storage implementation.
/// Provides persistent storage with full-text search and indexing.
/// </summary>
public class SQLiteGraphStore : IGraphStore, IDisposable
{
    private readonly string _connectionString;
    private readonly ILogger<SQLiteGraphStore> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SQLiteGraphStore(string databasePath, ILogger<SQLiteGraphStore> logger)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Database path cannot be empty", nameof(databasePath));
        }

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connectionString = $"Data Source={databasePath}";

        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var createTables = @"
            -- Entities table
            CREATE TABLE IF NOT EXISTS graph_entities (
                id TEXT PRIMARY KEY,
                name TEXT NOT NULL,
                type TEXT NOT NULL,
                description TEXT,
                properties TEXT,
                created TEXT NOT NULL,
                last_updated TEXT NOT NULL,
                embedding BLOB,
                confidence REAL NOT NULL,
                mention_count INTEGER NOT NULL DEFAULT 1
            );

            -- Relationships table
            CREATE TABLE IF NOT EXISTS graph_relationships (
                id TEXT PRIMARY KEY,
                source_entity_id TEXT NOT NULL,
                target_entity_id TEXT NOT NULL,
                relation_type TEXT NOT NULL,
                description TEXT,
                properties TEXT,
                confidence REAL NOT NULL,
                source_document_id TEXT,
                source_text TEXT,
                created TEXT NOT NULL,
                last_updated TEXT NOT NULL,
                weight REAL NOT NULL DEFAULT 1.0,
                is_bidirectional INTEGER NOT NULL DEFAULT 0,
                temporal_start TEXT,
                temporal_end TEXT,
                FOREIGN KEY (source_entity_id) REFERENCES graph_entities(id) ON DELETE CASCADE,
                FOREIGN KEY (target_entity_id) REFERENCES graph_entities(id) ON DELETE CASCADE
            );

            -- Entity source documents (many-to-many)
            CREATE TABLE IF NOT EXISTS entity_source_documents (
                entity_id TEXT NOT NULL,
                document_id TEXT NOT NULL,
                PRIMARY KEY (entity_id, document_id),
                FOREIGN KEY (entity_id) REFERENCES graph_entities(id) ON DELETE CASCADE
            );

            -- Entity aliases (many-to-many)
            CREATE TABLE IF NOT EXISTS entity_aliases (
                entity_id TEXT NOT NULL,
                alias TEXT NOT NULL,
                PRIMARY KEY (entity_id, alias),
                FOREIGN KEY (entity_id) REFERENCES graph_entities(id) ON DELETE CASCADE
            );

            -- Indexes for performance
            CREATE INDEX IF NOT EXISTS idx_entity_name ON graph_entities(name);
            CREATE INDEX IF NOT EXISTS idx_entity_type ON graph_entities(type);
            CREATE INDEX IF NOT EXISTS idx_entity_name_type ON graph_entities(name, type);
            CREATE INDEX IF NOT EXISTS idx_relationship_source ON graph_relationships(source_entity_id);
            CREATE INDEX IF NOT EXISTS idx_relationship_target ON graph_relationships(target_entity_id);
            CREATE INDEX IF NOT EXISTS idx_relationship_type ON graph_relationships(relation_type);
            CREATE INDEX IF NOT EXISTS idx_alias_alias ON entity_aliases(alias);

            -- Full-text search virtual table for entities
            CREATE VIRTUAL TABLE IF NOT EXISTS entity_fts USING fts5(
                entity_id UNINDEXED,
                name,
                type,
                description,
                content='graph_entities',
                content_rowid='rowid'
            );

            -- Triggers to keep FTS in sync
            CREATE TRIGGER IF NOT EXISTS entity_fts_insert AFTER INSERT ON graph_entities BEGIN
                INSERT INTO entity_fts(rowid, entity_id, name, type, description)
                VALUES (new.rowid, new.id, new.name, new.type, new.description);
            END;

            CREATE TRIGGER IF NOT EXISTS entity_fts_delete AFTER DELETE ON graph_entities BEGIN
                DELETE FROM entity_fts WHERE rowid = old.rowid;
            END;

            CREATE TRIGGER IF NOT EXISTS entity_fts_update AFTER UPDATE ON graph_entities BEGIN
                DELETE FROM entity_fts WHERE rowid = old.rowid;
                INSERT INTO entity_fts(rowid, entity_id, name, type, description)
                VALUES (new.rowid, new.id, new.name, new.type, new.description);
            END;
        ";

        using var command = connection.CreateCommand();
        command.CommandText = createTables;
        command.ExecuteNonQuery();

        _logger.LogInformation("SQLite graph database initialized at {Path}", _connectionString);
    }

    public async Task AddEntitiesAsync(List<GraphEntity> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null || entities.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var entity in entities)
                {
                    // Insert or replace entity
                    var insertEntity = @"
                        INSERT OR REPLACE INTO graph_entities
                        (id, name, type, description, properties, created, last_updated, embedding, confidence, mention_count)
                        VALUES (@id, @name, @type, @description, @properties, @created, @lastUpdated, @embedding, @confidence, @mentionCount)";

                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = insertEntity;
                    cmd.Parameters.AddWithValue("@id", entity.Id);
                    cmd.Parameters.AddWithValue("@name", entity.Name);
                    cmd.Parameters.AddWithValue("@type", entity.Type);
                    cmd.Parameters.AddWithValue("@description", entity.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@properties", JsonSerializer.Serialize(entity.Properties));
                    cmd.Parameters.AddWithValue("@created", entity.Created.ToString("o"));
                    cmd.Parameters.AddWithValue("@lastUpdated", entity.LastUpdated.ToString("o"));
                    cmd.Parameters.AddWithValue("@embedding", entity.Embedding != null ? SerializeEmbedding(entity.Embedding) : DBNull.Value);
                    cmd.Parameters.AddWithValue("@confidence", entity.Confidence);
                    cmd.Parameters.AddWithValue("@mentionCount", entity.MentionCount);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);

                    // Insert source documents
                    if (entity.SourceDocuments != null && entity.SourceDocuments.Count > 0)
                    {
                        foreach (var docId in entity.SourceDocuments)
                        {
                            var insertDoc = "INSERT OR IGNORE INTO entity_source_documents (entity_id, document_id) VALUES (@entityId, @docId)";
                            using var docCmd = connection.CreateCommand();
                            docCmd.Transaction = transaction;
                            docCmd.CommandText = insertDoc;
                            docCmd.Parameters.AddWithValue("@entityId", entity.Id);
                            docCmd.Parameters.AddWithValue("@docId", docId);
                            await docCmd.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }

                    // Insert aliases
                    if (entity.Aliases != null && entity.Aliases.Count > 0)
                    {
                        foreach (var alias in entity.Aliases)
                        {
                            var insertAlias = "INSERT OR IGNORE INTO entity_aliases (entity_id, alias) VALUES (@entityId, @alias)";
                            using var aliasCmd = connection.CreateCommand();
                            aliasCmd.Transaction = transaction;
                            aliasCmd.CommandText = insertAlias;
                            aliasCmd.Parameters.AddWithValue("@entityId", entity.Id);
                            aliasCmd.Parameters.AddWithValue("@alias", alias);
                            await aliasCmd.ExecuteNonQueryAsync(cancellationToken);
                        }
                    }
                }

                await transaction.CommitAsync(cancellationToken);
                _logger.LogDebug("Added {Count} entities to graph store", entities.Count);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<GraphEntity?> GetEntityByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var query = "SELECT * FROM graph_entities WHERE id = @id";
        using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var entity = ReadEntity(reader);
            await LoadEntityRelations(connection, entity, cancellationToken);
            return entity;
        }

        return null;
    }

    public async Task<List<GraphEntity>> SearchEntitiesByNameAsync(
        string name,
        string? type = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var query = new StringBuilder("SELECT DISTINCT e.* FROM graph_entities e ");
        query.Append("LEFT JOIN entity_aliases a ON e.id = a.entity_id ");
        query.Append("WHERE (e.name LIKE @name OR a.alias LIKE @name) ");

        if (!string.IsNullOrEmpty(type))
        {
            query.Append("AND e.type = @type ");
        }

        query.Append("ORDER BY e.mention_count DESC, e.confidence DESC LIMIT 50");

        using var cmd = connection.CreateCommand();
        cmd.CommandText = query.ToString();
        cmd.Parameters.AddWithValue("@name", $"%{name}%");
        if (!string.IsNullOrEmpty(type))
        {
            cmd.Parameters.AddWithValue("@type", type);
        }

        var entities = new List<GraphEntity>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var entity = ReadEntity(reader);
            await LoadEntityRelations(connection, entity, cancellationToken);
            entities.Add(entity);
        }

        return entities;
    }

    public async Task<List<GraphEntity>> SearchEntitiesByEmbeddingAsync(
        float[] embedding,
        int topK = 10,
        string? typeFilter = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Note: SQLite doesn't have native vector similarity functions
        // We load all entities with embeddings and calculate similarity in memory
        var query = "SELECT * FROM graph_entities WHERE embedding IS NOT NULL";
        if (!string.IsNullOrEmpty(typeFilter))
        {
            query += " AND type = @type";
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        if (!string.IsNullOrEmpty(typeFilter))
        {
            cmd.Parameters.AddWithValue("@type", typeFilter);
        }

        var candidates = new List<(GraphEntity entity, double similarity)>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var entity = ReadEntity(reader);
            if (entity.Embedding != null)
            {
                var similarity = CalculateCosineSimilarity(embedding, entity.Embedding);
                candidates.Add((entity, similarity));
            }
        }

        var results = candidates
            .OrderByDescending(c => c.similarity)
            .Take(topK)
            .Select(c => c.entity)
            .ToList();

        foreach (var entity in results)
        {
            await LoadEntityRelations(connection, entity, cancellationToken);
        }

        return results;
    }

    public async Task AddRelationshipsAsync(List<GraphRelationship> relationships, CancellationToken cancellationToken = default)
    {
        if (relationships == null || relationships.Count == 0)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();
            try
            {
                var insert = @"
                    INSERT OR REPLACE INTO graph_relationships
                    (id, source_entity_id, target_entity_id, relation_type, description, properties,
                     confidence, source_document_id, source_text, created, last_updated, weight,
                     is_bidirectional, temporal_start, temporal_end)
                    VALUES (@id, @sourceId, @targetId, @relationType, @description, @properties,
                            @confidence, @sourceDocId, @sourceText, @created, @lastUpdated, @weight,
                            @isBidir, @temporalStart, @temporalEnd)";

                foreach (var rel in relationships)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = insert;
                    cmd.Parameters.AddWithValue("@id", rel.Id);
                    cmd.Parameters.AddWithValue("@sourceId", rel.SourceEntityId);
                    cmd.Parameters.AddWithValue("@targetId", rel.TargetEntityId);
                    cmd.Parameters.AddWithValue("@relationType", rel.RelationType);
                    cmd.Parameters.AddWithValue("@description", rel.Description ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@properties", JsonSerializer.Serialize(rel.Properties));
                    cmd.Parameters.AddWithValue("@confidence", rel.Confidence);
                    cmd.Parameters.AddWithValue("@sourceDocId", rel.SourceDocumentId ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@sourceText", rel.SourceText ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@created", rel.Created.ToString("o"));
                    cmd.Parameters.AddWithValue("@lastUpdated", rel.LastUpdated.ToString("o"));
                    cmd.Parameters.AddWithValue("@weight", rel.Weight);
                    cmd.Parameters.AddWithValue("@isBidir", rel.IsBidirectional ? 1 : 0);
                    cmd.Parameters.AddWithValue("@temporalStart", rel.Temporal?.StartDate?.ToString("o") ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@temporalEnd", rel.Temporal?.EndDate?.ToString("o") ?? (object)DBNull.Value);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                _logger.LogDebug("Added {Count} relationships to graph store", relationships.Count);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<List<GraphRelationship>> GetRelationshipsAsync(string entityId, CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var query = "SELECT * FROM graph_relationships WHERE source_entity_id = @id OR target_entity_id = @id";
        using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@id", entityId);

        var relationships = new List<GraphRelationship>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            relationships.Add(ReadRelationship(reader));
        }

        return relationships;
    }

    public async Task<List<GraphEntity>> GetNeighborsAsync(
        string entityId,
        int depth = 1,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var visited = new HashSet<string> { entityId };
        var neighbors = new List<GraphEntity>();

        await GetNeighborsRecursiveAsync(connection, entityId, depth, visited, neighbors, cancellationToken);

        return neighbors;
    }

    private async Task GetNeighborsRecursiveAsync(
        SqliteConnection connection,
        string entityId,
        int depth,
        HashSet<string> visited,
        List<GraphEntity> neighbors,
        CancellationToken cancellationToken)
    {
        if (depth <= 0)
        {
            return;
        }

        var query = @"
            SELECT DISTINCT
                CASE
                    WHEN source_entity_id = @id THEN target_entity_id
                    ELSE source_entity_id
                END as neighbor_id
            FROM graph_relationships
            WHERE source_entity_id = @id OR target_entity_id = @id";

        using var cmd = connection.CreateCommand();
        cmd.CommandText = query;
        cmd.Parameters.AddWithValue("@id", entityId);

        var neighborIds = new List<string>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var neighborId = reader.GetString(0);
            if (!visited.Contains(neighborId))
            {
                neighborIds.Add(neighborId);
                visited.Add(neighborId);
            }
        }

        foreach (var neighborId in neighborIds)
        {
            var neighbor = await GetEntityByIdAsync(neighborId, cancellationToken);
            if (neighbor != null)
            {
                neighbors.Add(neighbor);

                if (depth > 1)
                {
                    await GetNeighborsRecursiveAsync(connection, neighborId, depth - 1, visited, neighbors, cancellationToken);
                }
            }
        }
    }

    public Task<List<GraphPath>> FindPathsAsync(
        string sourceId,
        string targetId,
        PathFindingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Simplified implementation - full BFS/DFS path finding would be complex in SQL
        // For production, consider using dedicated graph database or in-memory processing
        return Task.FromResult(new List<GraphPath>());
    }

    public async Task<List<GraphEntity>> SearchByLinkageAsync(
        string entityId,
        string? relationTypeFilter = null,
        int depth = 2,
        CancellationToken cancellationToken = default)
    {
        return await GetNeighborsAsync(entityId, depth, cancellationToken);
    }

    public async Task UpdateEntityAsync(GraphEntity entity, CancellationToken cancellationToken = default)
    {
        await AddEntitiesAsync(new List<GraphEntity> { entity }, cancellationToken);
    }

    public async Task DeleteEntityAsync(string entityId, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();
            try
            {
                // Delete entity (CASCADE will handle relationships, aliases, source_documents)
                var delete = "DELETE FROM graph_entities WHERE id = @id";
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = delete;
                cmd.Parameters.AddWithValue("@id", entityId);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<GraphStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var stats = new GraphStatistics();

        // Entity count
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM graph_entities";
            stats.EntityCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
        }

        // Relationship count
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM graph_relationships";
            stats.RelationshipCount = Convert.ToInt32(await cmd.ExecuteScalarAsync(cancellationToken));
        }

        // Entity type distribution
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT type, COUNT(*) FROM graph_entities GROUP BY type";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                stats.EntityTypeDistribution[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        // Relationship type distribution
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT relation_type, COUNT(*) FROM graph_relationships GROUP BY relation_type";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                stats.RelationshipTypeDistribution[reader.GetString(0)] = reader.GetInt32(1);
            }
        }

        return stats;
    }

    private GraphEntity ReadEntity(SqliteDataReader reader)
    {
        var entity = new GraphEntity
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            Type = reader.GetString(reader.GetOrdinal("type")),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
            Properties = JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(reader.GetOrdinal("properties"))) ?? new(),
            Created = DateTime.Parse(reader.GetString(reader.GetOrdinal("created"))),
            LastUpdated = DateTime.Parse(reader.GetString(reader.GetOrdinal("last_updated"))),
            Confidence = reader.GetDouble(reader.GetOrdinal("confidence")),
            MentionCount = reader.GetInt32(reader.GetOrdinal("mention_count"))
        };

        if (!reader.IsDBNull(reader.GetOrdinal("embedding")))
        {
            entity.Embedding = DeserializeEmbedding((byte[])reader["embedding"]);
        }

        return entity;
    }

    private async Task LoadEntityRelations(SqliteConnection connection, GraphEntity entity, CancellationToken cancellationToken)
    {
        // Load source documents
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT document_id FROM entity_source_documents WHERE entity_id = @id";
            cmd.Parameters.AddWithValue("@id", entity.Id);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entity.SourceDocuments.Add(reader.GetString(0));
            }
        }

        // Load aliases
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT alias FROM entity_aliases WHERE entity_id = @id";
            cmd.Parameters.AddWithValue("@id", entity.Id);
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entity.Aliases ??= new List<string>();
                entity.Aliases.Add(reader.GetString(0));
            }
        }
    }

    private GraphRelationship ReadRelationship(SqliteDataReader reader)
    {
        var rel = new GraphRelationship
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            SourceEntityId = reader.GetString(reader.GetOrdinal("source_entity_id")),
            TargetEntityId = reader.GetString(reader.GetOrdinal("target_entity_id")),
            RelationType = reader.GetString(reader.GetOrdinal("relation_type")),
            Description = reader.IsDBNull(reader.GetOrdinal("description")) ? null : reader.GetString(reader.GetOrdinal("description")),
            Properties = JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(reader.GetOrdinal("properties"))) ?? new(),
            Confidence = reader.GetDouble(reader.GetOrdinal("confidence")),
            SourceDocumentId = reader.IsDBNull(reader.GetOrdinal("source_document_id")) ? null : reader.GetString(reader.GetOrdinal("source_document_id")),
            SourceText = reader.IsDBNull(reader.GetOrdinal("source_text")) ? null : reader.GetString(reader.GetOrdinal("source_text")),
            Created = DateTime.Parse(reader.GetString(reader.GetOrdinal("created"))),
            LastUpdated = DateTime.Parse(reader.GetString(reader.GetOrdinal("last_updated"))),
            Weight = reader.GetDouble(reader.GetOrdinal("weight")),
            IsBidirectional = reader.GetInt32(reader.GetOrdinal("is_bidirectional")) == 1
        };

        if (!reader.IsDBNull(reader.GetOrdinal("temporal_start")) || !reader.IsDBNull(reader.GetOrdinal("temporal_end")))
        {
            rel.Temporal = new TemporalScope
            {
                StartDate = reader.IsDBNull(reader.GetOrdinal("temporal_start")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("temporal_start"))),
                EndDate = reader.IsDBNull(reader.GetOrdinal("temporal_end")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("temporal_end")))
            };
        }

        return rel;
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

    private double CalculateCosineSimilarity(float[] vec1, float[] vec2)
    {
        if (vec1.Length != vec2.Length)
        {
            return 0.0;
        }

        double dotProduct = 0.0;
        double magnitude1 = 0.0;
        double magnitude2 = 0.0;

        for (var i = 0; i < vec1.Length; i++)
        {
            dotProduct += vec1[i] * vec2[i];
            magnitude1 += vec1[i] * vec1[i];
            magnitude2 += vec2[i] * vec2[i];
        }

        if (magnitude1 == 0.0 || magnitude2 == 0.0)
        {
            return 0.0;
        }

        return dotProduct / (Math.Sqrt(magnitude1) * Math.Sqrt(magnitude2));
    }

    public void Dispose()
    {
        _lock?.Dispose();
    }
}
