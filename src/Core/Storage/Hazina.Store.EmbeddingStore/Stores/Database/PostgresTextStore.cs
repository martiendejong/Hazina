using Npgsql;

public class PostgresTextStore : ITextStore
{
    public string RootFolder { get; set; }
    private readonly string _connectionString;

    public PostgresTextStore(string connectionString)
    {
        _connectionString = connectionString;
        RootFolder = connectionString;

        using var conn = new NpgsqlConnection(_connectionString);
        conn.Open();
        using var cmd = new NpgsqlCommand(@"CREATE TABLE IF NOT EXISTS documents (
                key TEXT PRIMARY KEY,
                content TEXT NOT NULL
            );", conn);
        cmd.ExecuteNonQuery();
    }

    public string GetPath(string key)
    {
        return $"postgres://documents/{key}";
    }

    public async Task<string?> Get(string key)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT content FROM documents WHERE key = @key", conn);
        cmd.Parameters.AddWithValue("@key", key);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    public async Task<bool> Remove(string key)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM documents WHERE key = @key", conn);
        cmd.Parameters.AddWithValue("@key", key);
        var affected = await cmd.ExecuteNonQueryAsync();
        return affected > 0;
    }

    public async Task<bool> Store(string key, string value)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"INSERT INTO documents (key, content)
                                                  VALUES (@key, @content)
                                                  ON CONFLICT (key) DO UPDATE
                                                  SET content = EXCLUDED.content", conn);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@content", value);
        await cmd.ExecuteNonQueryAsync();
        return true;
    }
}

