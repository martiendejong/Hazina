using Hazina.API.Generic.Configuration;
using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace Hazina.API.Generic.Dynamic;

/// <summary>
/// Storage for dynamic entities using SQLite.
/// No EF Core needed - direct SQL for maximum flexibility.
/// </summary>
public class DynamicEntityStore : IDisposable
{
    private readonly string _connectionString;
    private readonly Dictionary<string, EntityDefinition> _entityDefinitions;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _initialized;

    public DynamicEntityStore(string databasePath, IEnumerable<EntityDefinition> definitions)
    {
        _connectionString = $"Data Source={databasePath}";
        _entityDefinitions = definitions.ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Initialize database tables for all entities
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var entity in _entityDefinitions.Values)
        {
            await CreateTableAsync(connection, entity);
        }

        _initialized = true;
    }

    /// <summary>
    /// Get all entities of a type
    /// </summary>
    public async Task<List<DynamicEntity>> GetAllAsync(string entityType, int page = 1, int pageSize = 20)
    {
        ValidateEntityType(entityType);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var offset = (page - 1) * pageSize;
        var sql = $"SELECT data FROM {entityType} WHERE json_extract(data, '$.isDeleted') != 1 OR json_extract(data, '$.isDeleted') IS NULL ORDER BY json_extract(data, '$.createdAt') DESC LIMIT @limit OFFSET @offset";

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@limit", pageSize);
        cmd.Parameters.AddWithValue("@offset", offset);

        var results = new List<DynamicEntity>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            results.Add(DynamicEntity.FromJson(json, entityType));
        }

        return results;
    }

    /// <summary>
    /// Get entity by ID
    /// </summary>
    public async Task<DynamicEntity?> GetByIdAsync(string entityType, Guid id)
    {
        ValidateEntityType(entityType);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = $"SELECT data FROM {entityType} WHERE id = @id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", id.ToString());

        var result = await cmd.ExecuteScalarAsync();
        if (result is string json)
        {
            return DynamicEntity.FromJson(json, entityType);
        }

        return null;
    }

    /// <summary>
    /// Create a new entity
    /// </summary>
    public async Task<DynamicEntity> CreateAsync(string entityType, DynamicEntity entity)
    {
        ValidateEntityType(entityType);

        entity.EntityType = entityType;
        entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        entity.CreatedAt = DateTime.UtcNow;
        entity.IsDeleted = false;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = $"INSERT INTO {entityType} (id, data) VALUES (@id, @data)";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", entity.Id.ToString());
        cmd.Parameters.AddWithValue("@data", entity.ToJson(_jsonOptions));

        await cmd.ExecuteNonQueryAsync();
        return entity;
    }

    /// <summary>
    /// Update an entity
    /// </summary>
    public async Task<DynamicEntity?> UpdateAsync(string entityType, Guid id, DynamicEntity entity)
    {
        ValidateEntityType(entityType);

        var existing = await GetByIdAsync(entityType, id);
        if (existing == null) return null;

        // Preserve system fields
        entity.Id = id;
        entity.EntityType = entityType;
        entity.CreatedAt = existing.CreatedAt;
        entity.UpdatedAt = DateTime.UtcNow;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = $"UPDATE {entityType} SET data = @data WHERE id = @id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.Parameters.AddWithValue("@data", entity.ToJson(_jsonOptions));

        await cmd.ExecuteNonQueryAsync();
        return entity;
    }

    /// <summary>
    /// Delete an entity (soft delete)
    /// </summary>
    public async Task<bool> DeleteAsync(string entityType, Guid id, bool hard = false)
    {
        ValidateEntityType(entityType);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        if (hard)
        {
            var sql = $"DELETE FROM {entityType} WHERE id = @id";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@id", id.ToString());
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        else
        {
            var entity = await GetByIdAsync(entityType, id);
            if (entity == null) return false;

            entity.IsDeleted = true;
            entity["deletedAt"] = DateTime.UtcNow;
            await UpdateAsync(entityType, id, entity);
            return true;
        }
    }

    /// <summary>
    /// Count entities
    /// </summary>
    public async Task<int> CountAsync(string entityType)
    {
        ValidateEntityType(entityType);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = $"SELECT COUNT(*) FROM {entityType} WHERE json_extract(data, '$.isDeleted') != 1 OR json_extract(data, '$.isDeleted') IS NULL";
        using var cmd = new SqliteCommand(sql, connection);

        var result = await cmd.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Search entities by text (simple LIKE search)
    /// </summary>
    public async Task<List<DynamicEntity>> SearchAsync(string entityType, string query, int limit = 20)
    {
        ValidateEntityType(entityType);

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = $"SELECT data FROM {entityType} WHERE data LIKE @query AND (json_extract(data, '$.isDeleted') != 1 OR json_extract(data, '$.isDeleted') IS NULL) LIMIT @limit";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@query", $"%{query}%");
        cmd.Parameters.AddWithValue("@limit", limit);

        var results = new List<DynamicEntity>();
        using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var json = reader.GetString(0);
            results.Add(DynamicEntity.FromJson(json, entityType));
        }

        return results;
    }

    /// <summary>
    /// Get all registered entity types
    /// </summary>
    public IEnumerable<string> GetEntityTypes() => _entityDefinitions.Keys;

    /// <summary>
    /// Get entity definition
    /// </summary>
    public EntityDefinition? GetDefinition(string entityType)
    {
        return _entityDefinitions.TryGetValue(entityType, out var def) ? def : null;
    }

    private void ValidateEntityType(string entityType)
    {
        if (!_entityDefinitions.ContainsKey(entityType))
        {
            throw new ArgumentException($"Unknown entity type: {entityType}. Available types: {string.Join(", ", _entityDefinitions.Keys)}");
        }
    }

    private static async Task CreateTableAsync(SqliteConnection connection, EntityDefinition entity)
    {
        // Simple schema: id + json data blob
        // This gives maximum flexibility - any field changes in YAML work immediately
        var sql = $@"
            CREATE TABLE IF NOT EXISTS {entity.Name} (
                id TEXT PRIMARY KEY,
                data TEXT NOT NULL
            )";

        using var cmd = new SqliteCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();

        // Create index for common queries
        var indexSql = $"CREATE INDEX IF NOT EXISTS idx_{entity.Name}_created ON {entity.Name}(json_extract(data, '$.createdAt'))";
        using var indexCmd = new SqliteCommand(indexSql, connection);
        await indexCmd.ExecuteNonQueryAsync();
    }

    public void Dispose()
    {
        // SQLite connections are lightweight, nothing to dispose
    }
}
