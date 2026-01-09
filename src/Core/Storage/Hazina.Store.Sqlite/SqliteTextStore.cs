using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Hazina.Store.Sqlite;

/// <summary>
/// SQLite-based implementation of ITextStore.
/// Stores text content in the chunks table.
/// Note: RootFolder and GetPath() return database path since text is stored in DB, not files.
/// </summary>
public class SqliteTextStore : ITextStore
{
    private readonly string _connectionString;
    private readonly ILogger? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public string RootFolder { get; set; }

    public SqliteTextStore(string connectionString, ILogger? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger;

        // Extract database path from connection string
        var builder = new SqliteConnectionStringBuilder(connectionString);
        RootFolder = Path.GetDirectoryName(builder.DataSource) ?? Environment.CurrentDirectory;

        // Initialize schema
        var schema = new SqliteSchema(connectionString, logger);
        schema.InitializeAsync().GetAwaiter().GetResult();
    }

    public async Task<bool> Store(string key, string value)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE chunks
                SET content = @content
                WHERE chunk_key = @key;
            ";
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@content", value);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();

            if (rowsAffected == 0)
            {
                _logger?.LogWarning("Text store failed: chunk key '{Key}' not found. Ensure chunk is created first via IChunkStore.", key);
                return false;
            }

            _logger?.LogDebug("Stored text for chunk key '{Key}' ({Length} chars)", key, value.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to store text for chunk key '{Key}'", key);
            return false;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> Get(string key)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT content
            FROM chunks
            WHERE chunk_key = @key
            LIMIT 1;
        ";
        cmd.Parameters.AddWithValue("@key", key);

        var result = await cmd.ExecuteScalarAsync();
        return result?.ToString();
    }

    public string GetPath(string key)
    {
        // For SQLite, there is no file path - text is in database
        // Return a virtual path for compatibility
        var builder = new SqliteConnectionStringBuilder(_connectionString);
        return $"{builder.DataSource}#chunks/{key}";
    }

    public async Task<bool> Remove(string key)
    {
        await _lock.WaitAsync();
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                UPDATE chunks
                SET content = NULL
                WHERE chunk_key = @key;
            ";
            cmd.Parameters.AddWithValue("@key", key);

            var rowsAffected = await cmd.ExecuteNonQueryAsync();
            _logger?.LogDebug("Removed text for chunk key '{Key}' (rows affected: {Count})", key, rowsAffected);

            return rowsAffected > 0;
        }
        finally
        {
            _lock.Release();
        }
    }
}
