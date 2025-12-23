using System;
using System.Text.Json;
using System.Threading.Tasks;
using Npgsql;

public class PostgresDocumentMetadataStore : IDocumentMetadataStore
{
    private readonly string _connectionString;

    public PostgresDocumentMetadataStore(string connectionString)
    {
        _connectionString = connectionString;
        InitializeAsync().Wait();
    }

    private async Task InitializeAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var createTableSql = @"
            CREATE TABLE IF NOT EXISTS document_metadata (
                id TEXT PRIMARY KEY,
                metadata_json TEXT NOT NULL,
                created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
            )";

        await using var cmd = new NpgsqlCommand(createTableSql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<bool> Store(string id, DocumentMetadata metadata)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var json = JsonSerializer.Serialize(metadata);
            var sql = @"
                INSERT INTO document_metadata (id, metadata_json)
                VALUES (@id, @json)
                ON CONFLICT (id) DO UPDATE SET metadata_json = @json";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("json", json);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DocumentMetadata?> Get(string id)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "SELECT metadata_json FROM document_metadata WHERE id = @id";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            var result = await cmd.ExecuteScalarAsync();
            if (result == null) return null;

            var json = result.ToString();
            return JsonSerializer.Deserialize<DocumentMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> Remove(string id)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "DELETE FROM document_metadata WHERE id = @id";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            await cmd.ExecuteNonQueryAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> Exists(string id)
    {
        try
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var sql = "SELECT COUNT(*) FROM document_metadata WHERE id = @id";

            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("id", id);

            var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
            return count > 0;
        }
        catch
        {
            return false;
        }
    }
}
