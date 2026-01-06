using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Hazina.Store.Sqlite;

/// <summary>
/// Manages SQLite database schema creation and migrations.
/// Implements the canonical storage model: Items (with checksums) → Metadata/Tags/Chunks → Embeddings (optional).
/// </summary>
public class SqliteSchema
{
    private readonly string _connectionString;
    private readonly ILogger? _logger;

    /// <summary>
    /// Current schema version. Increment when making breaking changes.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    public SqliteSchema(string connectionString, ILogger? logger = null)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    /// <summary>
    /// Initializes the database schema if it doesn't exist.
    /// Idempotent - safe to call multiple times.
    /// </summary>
    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Enable foreign keys (not enabled by default in SQLite)
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA foreign_keys = ON;";
            await cmd.ExecuteNonQueryAsync();
        }

        // Create tables
        await CreateSchemaInfoTableAsync(connection);
        await CreateItemsTableAsync(connection);
        await CreateMetadataTableAsync(connection);
        await CreateTagsTableAsync(connection);
        await CreateChunksTableAsync(connection);
        await CreateEmbeddingsTableAsync(connection);
        await CreateFTS5TableAsync(connection);

        // Create indexes
        await CreateIndexesAsync(connection);

        // Update schema version
        await UpdateSchemaVersionAsync(connection);

        _logger?.LogInformation("SQLite database schema initialized (version {Version})", CurrentSchemaVersion);
    }

    private async Task CreateSchemaInfoTableAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS schema_info (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateItemsTableAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS items (
                id TEXT PRIMARY KEY,
                source_file TEXT,
                file_checksum TEXT,
                ingest_version INTEGER NOT NULL DEFAULT 1,
                mime_type TEXT,
                is_binary INTEGER NOT NULL DEFAULT 0,
                content TEXT,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateMetadataTableAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS metadata (
                item_id TEXT NOT NULL,
                key TEXT NOT NULL,
                value TEXT NOT NULL,
                PRIMARY KEY (item_id, key),
                FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateTagsTableAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS tags (
                item_id TEXT NOT NULL,
                tag TEXT NOT NULL,
                PRIMARY KEY (item_id, tag),
                FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateChunksTableAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS chunks (
                chunk_key TEXT PRIMARY KEY,
                parent_document_id TEXT NOT NULL,
                chunk_index INTEGER NOT NULL,
                content TEXT,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (parent_document_id) REFERENCES items(id) ON DELETE CASCADE
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateEmbeddingsTableAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS embeddings (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                item_id TEXT NOT NULL,
                model TEXT NOT NULL,
                dimension INTEGER NOT NULL,
                vector BLOB NOT NULL,
                checksum TEXT NOT NULL,
                created_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (item_id) REFERENCES items(id) ON DELETE CASCADE,
                UNIQUE (item_id, model)
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateFTS5TableAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE VIRTUAL TABLE IF NOT EXISTS items_fts USING fts5(
                item_id UNINDEXED,
                content,
                tokenize = 'porter'
            );
        ";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task CreateIndexesAsync(SqliteConnection connection)
    {
        var indexes = new[]
        {
            "CREATE INDEX IF NOT EXISTS idx_items_source_file ON items(source_file);",
            "CREATE INDEX IF NOT EXISTS idx_items_mime_type ON items(mime_type);",
            "CREATE INDEX IF NOT EXISTS idx_items_created_at ON items(created_at);",
            "CREATE INDEX IF NOT EXISTS idx_metadata_key ON metadata(key);",
            "CREATE INDEX IF NOT EXISTS idx_tags_tag ON tags(tag);",
            "CREATE INDEX IF NOT EXISTS idx_chunks_parent ON chunks(parent_document_id);",
            "CREATE INDEX IF NOT EXISTS idx_embeddings_item_id ON embeddings(item_id);",
            "CREATE INDEX IF NOT EXISTS idx_embeddings_model ON embeddings(model);"
        };

        foreach (var indexSql in indexes)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = indexSql;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private async Task UpdateSchemaVersionAsync(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO schema_info (key, value, updated_at)
            VALUES ('schema_version', @version, datetime('now'));
        ";
        cmd.Parameters.AddWithValue("@version", CurrentSchemaVersion.ToString());
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Gets the current schema version from the database.
    /// Returns 0 if not initialized.
    /// </summary>
    public async Task<int> GetSchemaVersionAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM schema_info WHERE key = 'schema_version' LIMIT 1;";

        var result = await cmd.ExecuteScalarAsync();
        if (result == null) return 0;

        return int.TryParse(result.ToString(), out var version) ? version : 0;
    }
}
