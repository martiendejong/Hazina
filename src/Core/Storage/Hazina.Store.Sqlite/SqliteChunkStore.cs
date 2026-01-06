using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Hazina.Store.Sqlite;

/// <summary>
/// SQLite-based implementation of IChunkStore.
/// Stores document-to-chunk relationships in the chunks table.
/// </summary>
public class SqliteChunkStore : IChunkStore
{
    private readonly string _connectionString;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SqliteChunkStore(string connectionString, ILogger? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger;

        // Initialize schema
        var schema = new SqliteSchema(connectionString, logger);
        schema.InitializeAsync().GetAwaiter().GetResult();
    }

    public async Task<bool> Store(string name, IEnumerable<string> chunkKeys)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Delete existing chunks for this document
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM chunks WHERE parent_document_id = @name;";
                    cmd.Parameters.AddWithValue("@name", name);
                    await cmd.ExecuteNonQueryAsync();
                }

                // Insert new chunks
                int index = 0;
                foreach (var chunkKey in chunkKeys)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO chunks (chunk_key, parent_document_id, chunk_index)
                        VALUES (@chunk_key, @parent_id, @index);
                    ";
                    cmd.Parameters.AddWithValue("@chunk_key", chunkKey);
                    cmd.Parameters.AddWithValue("@parent_id", name);
                    cmd.Parameters.AddWithValue("@index", index);
                    await cmd.ExecuteNonQueryAsync();
                    index++;
                }

                transaction.Commit();
                _logger?.LogDebug("Stored {Count} chunks for document '{Name}'", index, name);
                return true;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger?.LogError(ex, "Failed to store chunks for document '{Name}'", name);
                return false;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<string>> Get(string name)
    {
        var chunkKeys = new List<string>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT chunk_key
            FROM chunks
            WHERE parent_document_id = @name
            ORDER BY chunk_index ASC;
        ";
        cmd.Parameters.AddWithValue("@name", name);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            chunkKeys.Add(reader.GetString(0));
        }

        return chunkKeys;
    }

    public async Task<bool> Remove(string name, IEnumerable<string> chunkKeys)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            int totalRemoved = 0;

            foreach (var chunkKey in chunkKeys)
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    DELETE FROM chunks
                    WHERE parent_document_id = @name AND chunk_key = @chunk_key;
                ";
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@chunk_key", chunkKey);

                var rowsAffected = await cmd.ExecuteNonQueryAsync();
                totalRemoved += rowsAffected;
            }

            _logger?.LogDebug("Removed {Count} chunks from document '{Name}'", totalRemoved, name);
            return totalRemoved > 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IEnumerable<string>> ListNames()
    {
        var names = new List<string>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT parent_document_id FROM chunks ORDER BY parent_document_id;";

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    public async Task<string?> GetParentDocument(string chunkKey)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT parent_document_id
            FROM chunks
            WHERE chunk_key = @chunk_key
            LIMIT 1;
        ";
        cmd.Parameters.AddWithValue("@chunk_key", chunkKey);

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }
}
