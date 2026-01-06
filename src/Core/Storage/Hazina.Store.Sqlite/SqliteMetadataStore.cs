using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Hazina.Store.Sqlite;

/// <summary>
/// SQLite-based implementation of IQueryableMetadataStore.
/// Stores metadata in normalized tables (items, metadata, tags) with SQL query support.
/// Supports FTS5 full-text search for efficient keyword matching.
/// </summary>
public class SqliteMetadataStore : IQueryableMetadataStore
{
    private readonly string _connectionString;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteMetadataStore(string connectionString, ILogger? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger;

        // Initialize schema
        var schema = new SqliteSchema(connectionString, logger);
        schema.InitializeAsync().GetAwaiter().GetResult();
    }

    #region IDocumentMetadataStore Implementation

    public async Task<bool> Store(string id, DocumentMetadata metadata)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Store in items table
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO items (
                            id, source_file, mime_type, is_binary, content, created_at, updated_at
                        ) VALUES (
                            @id, @source_file, @mime_type, @is_binary, @content, @created_at, datetime('now')
                        );
                    ";

                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@source_file", metadata.OriginalPath ?? string.Empty);
                    cmd.Parameters.AddWithValue("@mime_type", metadata.MimeType);
                    cmd.Parameters.AddWithValue("@is_binary", metadata.IsBinary ? 1 : 0);
                    cmd.Parameters.AddWithValue("@content", metadata.Summary ?? string.Empty);
                    cmd.Parameters.AddWithValue("@created_at", metadata.Created.ToString("o"));

                    await cmd.ExecuteNonQueryAsync();
                }

                // Delete existing metadata and tags
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM metadata WHERE item_id = @id;";
                    cmd.Parameters.AddWithValue("@id", id);
                    await cmd.ExecuteNonQueryAsync();
                }

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM tags WHERE item_id = @id;";
                    cmd.Parameters.AddWithValue("@id", id);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Store custom metadata
                foreach (var kv in metadata.CustomMetadata)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO metadata (item_id, key, value)
                        VALUES (@item_id, @key, @value);
                    ";
                    cmd.Parameters.AddWithValue("@item_id", id);
                    cmd.Parameters.AddWithValue("@key", kv.Key);
                    cmd.Parameters.AddWithValue("@value", kv.Value);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Store tags
                foreach (var tag in metadata.Tags)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO tags (item_id, tag)
                        VALUES (@item_id, @tag);
                    ";
                    cmd.Parameters.AddWithValue("@item_id", id);
                    cmd.Parameters.AddWithValue("@tag", tag);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Update FTS5 index
                var searchableContent = metadata.SearchableText ?? metadata.Summary ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(searchableContent))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT OR REPLACE INTO items_fts (item_id, content)
                        VALUES (@item_id, @content);
                    ";
                    cmd.Parameters.AddWithValue("@item_id", id);
                    cmd.Parameters.AddWithValue("@content", searchableContent);
                    await cmd.ExecuteNonQueryAsync();
                }

                transaction.Commit();
                _logger?.LogDebug("Stored metadata for document '{Id}'", id);
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger?.LogError(ex, "Failed to store metadata for document '{Id}'", id);
                return false;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<DocumentMetadata?> Get(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Get item
        DocumentMetadata? metadata = null;
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, source_file, mime_type, is_binary, content, created_at
                FROM items
                WHERE id = @id
                LIMIT 1;
            ";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            metadata = new DocumentMetadata
            {
                Id = reader.GetString(0),
                OriginalPath = reader.GetString(1),
                MimeType = reader.GetString(2),
                IsBinary = reader.GetInt32(3) == 1,
                Summary = reader.IsDBNull(4) ? null : reader.GetString(4),
                Created = DateTime.Parse(reader.GetString(5))
            };
        }

        // Get custom metadata
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT key, value FROM metadata WHERE item_id = @id;";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                metadata.CustomMetadata[reader.GetString(0)] = reader.GetString(1);
            }
        }

        // Get tags
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT tag FROM tags WHERE item_id = @id;";
            cmd.Parameters.AddWithValue("@id", id);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                metadata.Tags.Add(reader.GetString(0));
            }
        }

        return metadata;
    }

    public async Task<bool> Remove(string id)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Remove from FTS5
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM items_fts WHERE item_id = @id;";
                    cmd.Parameters.AddWithValue("@id", id);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Remove from items (cascades to metadata and tags via foreign keys)
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM items WHERE id = @id;";
                    cmd.Parameters.AddWithValue("@id", id);
                    var rowsAffected = await cmd.ExecuteNonQueryAsync();

                    transaction.Commit();
                    _logger?.LogDebug("Removed document '{Id}' (rows affected: {Count})", id, rowsAffected);
                    return rowsAffected > 0;
                }
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger?.LogError(ex, "Failed to remove document '{Id}'", id);
                return false;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> Exists(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM items WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        var count = (long?)await cmd.ExecuteScalarAsync() ?? 0;
        return count > 0;
    }

    #endregion

    #region IQueryableMetadataStore Implementation

    public async Task<List<DocumentMetadata>> QueryAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DocumentMetadata>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var (sql, parameters) = BuildQuerySql(filter);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var param in parameters)
        {
            cmd.Parameters.AddWithValue(param.Key, param.Value);
        }

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(0);
            var metadata = await Get(id); // Load full metadata including custom fields and tags
            if (metadata != null)
            {
                results.Add(metadata);
            }
        }

        _logger?.LogDebug("Query returned {Count} documents", results.Count);
        return results;
    }

    public async Task<List<string>> GetMatchingIdsAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default)
    {
        var ids = new List<string>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var (sql, parameters) = BuildQuerySql(filter);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var param in parameters)
        {
            cmd.Parameters.AddWithValue(param.Key, param.Value);
        }

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    public async Task<int> CountAsync(
        MetadataFilter filter,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var (sql, parameters) = BuildQuerySql(filter, countOnly: true);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        foreach (var param in parameters)
        {
            cmd.Parameters.AddWithValue(param.Key, param.Value);
        }

        var count = (long?)await cmd.ExecuteScalarAsync(cancellationToken) ?? 0;
        return (int)count;
    }

    public async Task<List<DocumentMetadata>> SearchTextAsync(
        string searchText,
        MetadataFilter? filter = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DocumentMetadata>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        // Use FTS5 for full-text search
        var sql = new StringBuilder(@"
            SELECT DISTINCT i.id
            FROM items_fts fts
            JOIN items i ON i.id = fts.item_id
            WHERE items_fts MATCH @search
        ");

        var parameters = new Dictionary<string, object>
        {
            ["@search"] = searchText
        };

        // Add filter conditions if provided
        if (filter != null && !filter.IsEmpty)
        {
            var (filterSql, filterParams) = BuildFilterConditions(filter);
            sql.Append(" AND ").Append(filterSql);
            foreach (var param in filterParams)
            {
                parameters[param.Key] = param.Value;
            }
        }

        sql.Append($" LIMIT {limit};");

        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql.ToString();
        foreach (var param in parameters)
        {
            cmd.Parameters.AddWithValue(param.Key, param.Value);
        }

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetString(0);
            var metadata = await Get(id);
            if (metadata != null)
            {
                results.Add(metadata);
            }
        }

        _logger?.LogDebug("Full-text search for '{Query}' returned {Count} documents", searchText, results.Count);
        return results;
    }

    #endregion

    #region Helper Methods

    private (string sql, Dictionary<string, object> parameters) BuildQuerySql(MetadataFilter filter, bool countOnly = false)
    {
        var select = countOnly ? "SELECT COUNT(DISTINCT i.id)" : "SELECT DISTINCT i.id";
        var sql = new StringBuilder($"{select} FROM items i");

        var parameters = new Dictionary<string, object>();
        var whereClauses = new List<string>();
        var joins = new HashSet<string>();

        // Add filter conditions
        if (!filter.IsEmpty)
        {
            var (filterSql, filterParams) = BuildFilterConditions(filter, joins);
            if (!string.IsNullOrEmpty(filterSql))
            {
                whereClauses.Add(filterSql);
                foreach (var param in filterParams)
                {
                    parameters[param.Key] = param.Value;
                }
            }
        }

        // Add joins
        foreach (var join in joins)
        {
            sql.Append(" ").Append(join);
        }

        // Add WHERE clause
        if (whereClauses.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", whereClauses));
        }

        // Add LIMIT and OFFSET
        if (!countOnly)
        {
            sql.Append($" LIMIT {filter.Limit} OFFSET {filter.Offset};");
        }
        else
        {
            sql.Append(";");
        }

        return (sql.ToString(), parameters);
    }

    private (string sql, Dictionary<string, object> parameters) BuildFilterConditions(
        MetadataFilter filter,
        HashSet<string>? joins = null)
    {
        var conditions = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(filter.MimeType))
        {
            conditions.Add("i.mime_type = @mime_type");
            parameters["@mime_type"] = filter.MimeType;
        }

        if (!string.IsNullOrEmpty(filter.MimeTypePrefix))
        {
            conditions.Add("i.mime_type LIKE @mime_type_prefix");
            parameters["@mime_type_prefix"] = filter.MimeTypePrefix + "%";
        }

        if (!string.IsNullOrEmpty(filter.PathPattern))
        {
            var pattern = filter.PathPattern.Replace("*", "%");
            conditions.Add("i.source_file LIKE @path_pattern");
            parameters["@path_pattern"] = pattern;
        }

        if (filter.CreatedAfter.HasValue)
        {
            conditions.Add("datetime(i.created_at) >= datetime(@created_after)");
            parameters["@created_after"] = filter.CreatedAfter.Value.ToString("o");
        }

        if (filter.CreatedBefore.HasValue)
        {
            conditions.Add("datetime(i.created_at) <= datetime(@created_before)");
            parameters["@created_before"] = filter.CreatedBefore.Value.ToString("o");
        }

        if (filter.IsBinary.HasValue)
        {
            conditions.Add("i.is_binary = @is_binary");
            parameters["@is_binary"] = filter.IsBinary.Value ? 1 : 0;
        }

        // Custom metadata (requires join)
        if (filter.CustomMetadata != null && filter.CustomMetadata.Count > 0)
        {
            int idx = 0;
            foreach (var kv in filter.CustomMetadata)
            {
                var alias = $"m{idx}";
                joins?.Add($"JOIN metadata {alias} ON {alias}.item_id = i.id");
                conditions.Add($"{alias}.key = @meta_key{idx} AND {alias}.value = @meta_val{idx}");
                parameters[$"@meta_key{idx}"] = kv.Key;
                parameters[$"@meta_val{idx}"] = kv.Value;
                idx++;
            }
        }

        // Tags - ALL must match
        if (filter.Tags != null && filter.Tags.Count > 0)
        {
            for (int i = 0; i < filter.Tags.Count; i++)
            {
                var alias = $"t{i}";
                joins?.Add($"JOIN tags {alias} ON {alias}.item_id = i.id");
                conditions.Add($"{alias}.tag = @tag{i}");
                parameters[$"@tag{i}"] = filter.Tags[i];
            }
        }

        // Tags - ANY must match
        if (filter.AnyTags != null && filter.AnyTags.Count > 0)
        {
            joins?.Add("JOIN tags ta ON ta.item_id = i.id");
            var tagPlaceholders = string.Join(",", filter.AnyTags.Select((_, i) => $"@anytag{i}"));
            conditions.Add($"ta.tag IN ({tagPlaceholders})");
            for (int i = 0; i < filter.AnyTags.Count; i++)
            {
                parameters[$"@anytag{i}"] = filter.AnyTags[i];
            }
        }

        return (string.Join(" AND ", conditions), parameters);
    }

    #endregion
}
