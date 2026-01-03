using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Hazina.Tools.Services.Social.Abstractions;

namespace Hazina.Tools.Services.Social.Stores;

/// <summary>
/// SQLite-based storage for connected social accounts.
/// </summary>
public class SqliteSocialAccountStore : ISocialAccountStore
{
    private readonly ILogger<SqliteSocialAccountStore> _logger;
    private readonly Func<string, string> _databasePathResolver;
    private readonly ISocialContentStore _contentStore;

    public SqliteSocialAccountStore(
        ILogger<SqliteSocialAccountStore> logger,
        Func<string, string> databasePathResolver,
        ISocialContentStore contentStore)
    {
        _logger = logger;
        _databasePathResolver = databasePathResolver;
        _contentStore = contentStore;
    }

    private async Task<SqliteConnection> GetConnectionAsync(string projectId, CancellationToken cancellationToken)
    {
        var dbPath = _databasePathResolver(projectId);
        var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(cancellationToken);
        await EnsureSchemaAsync(connection, cancellationToken);
        return connection;
    }

    private async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sql = @"
            CREATE TABLE IF NOT EXISTS connected_accounts (
                id TEXT PRIMARY KEY,
                project_id TEXT NOT NULL,
                provider_id TEXT NOT NULL,
                provider_user_id TEXT NOT NULL,
                display_name TEXT NOT NULL,
                profile_url TEXT,
                avatar_url TEXT,
                access_token TEXT NOT NULL,
                refresh_token TEXT,
                token_expires_at TEXT,
                connected_at TEXT NOT NULL,
                last_import_at TEXT,
                imported_item_count INTEGER DEFAULT 0,
                status TEXT NOT NULL,
                metadata TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_accounts_project ON connected_accounts(project_id);
            CREATE INDEX IF NOT EXISTS idx_accounts_provider ON connected_accounts(provider_id);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_accounts_unique ON connected_accounts(project_id, provider_id, provider_user_id);
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveAccountAsync(
        string projectId,
        ConnectedAccount account,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO connected_accounts
            (id, project_id, provider_id, provider_user_id, display_name, profile_url, avatar_url,
             access_token, refresh_token, token_expires_at, connected_at, last_import_at,
             imported_item_count, status, metadata)
            VALUES (@id, @project_id, @provider_id, @provider_user_id, @display_name, @profile_url, @avatar_url,
                    @access_token, @refresh_token, @token_expires_at, @connected_at, @last_import_at,
                    @imported_item_count, @status, @metadata)";

        cmd.Parameters.AddWithValue("@id", account.Id);
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@provider_id", account.ProviderId);
        cmd.Parameters.AddWithValue("@provider_user_id", account.ProviderUserId);
        cmd.Parameters.AddWithValue("@display_name", account.DisplayName);
        cmd.Parameters.AddWithValue("@profile_url", (object?)account.ProfileUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@avatar_url", (object?)account.AvatarUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@access_token", account.AccessToken);
        cmd.Parameters.AddWithValue("@refresh_token", (object?)account.RefreshToken ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@token_expires_at", account.TokenExpiresAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@connected_at", account.ConnectedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@last_import_at", account.LastImportAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@imported_item_count", account.ImportedItemCount);
        cmd.Parameters.AddWithValue("@status", account.Status.ToString());
        cmd.Parameters.AddWithValue("@metadata", JsonSerializer.Serialize(account.Metadata));

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Saved account {AccountId} for project {ProjectId}", account.Id, projectId);
    }

    public async Task<List<ConnectedAccount>> GetAccountsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM connected_accounts WHERE project_id = @project_id ORDER BY connected_at DESC";
        cmd.Parameters.AddWithValue("@project_id", projectId);

        var accounts = new List<ConnectedAccount>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            accounts.Add(ReadAccount(reader));
        }

        return accounts;
    }

    public async Task<ConnectedAccount?> GetAccountAsync(
        string projectId,
        string accountId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM connected_accounts WHERE project_id = @project_id AND id = @id";
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@id", accountId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadAccount(reader);
        }

        return null;
    }

    public async Task<bool> RemoveAccountAsync(
        string projectId,
        string accountId,
        bool deleteData = true,
        CancellationToken cancellationToken = default)
    {
        if (deleteData)
        {
            await _contentStore.DeleteAccountContentAsync(projectId, accountId, cancellationToken);
        }

        await using var connection = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM connected_accounts WHERE project_id = @project_id AND id = @id";
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@id", accountId);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Removed account {AccountId} from project {ProjectId}, deleted data: {DeleteData}",
            accountId, projectId, deleteData);

        return affected > 0;
    }

    public async Task UpdateTokensAsync(
        string projectId,
        string accountId,
        string accessToken,
        string? refreshToken,
        DateTime? expiresAt,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE connected_accounts
            SET access_token = @access_token,
                refresh_token = @refresh_token,
                token_expires_at = @token_expires_at,
                status = @status
            WHERE project_id = @project_id AND id = @id";

        cmd.Parameters.AddWithValue("@access_token", accessToken);
        cmd.Parameters.AddWithValue("@refresh_token", (object?)refreshToken ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@token_expires_at", expiresAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@status", AccountStatus.Active.ToString());
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@id", accountId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Updated tokens for account {AccountId} in project {ProjectId}", accountId, projectId);
    }

    private static ConnectedAccount ReadAccount(SqliteDataReader reader)
    {
        return new ConnectedAccount
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            ProjectId = reader.GetString(reader.GetOrdinal("project_id")),
            ProviderId = reader.GetString(reader.GetOrdinal("provider_id")),
            ProviderUserId = reader.GetString(reader.GetOrdinal("provider_user_id")),
            DisplayName = reader.GetString(reader.GetOrdinal("display_name")),
            ProfileUrl = reader.IsDBNull(reader.GetOrdinal("profile_url")) ? null : reader.GetString(reader.GetOrdinal("profile_url")),
            AvatarUrl = reader.IsDBNull(reader.GetOrdinal("avatar_url")) ? null : reader.GetString(reader.GetOrdinal("avatar_url")),
            AccessToken = reader.GetString(reader.GetOrdinal("access_token")),
            RefreshToken = reader.IsDBNull(reader.GetOrdinal("refresh_token")) ? null : reader.GetString(reader.GetOrdinal("refresh_token")),
            TokenExpiresAt = reader.IsDBNull(reader.GetOrdinal("token_expires_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("token_expires_at"))),
            ConnectedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("connected_at"))),
            LastImportAt = reader.IsDBNull(reader.GetOrdinal("last_import_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_import_at"))),
            ImportedItemCount = reader.GetInt32(reader.GetOrdinal("imported_item_count")),
            Status = Enum.Parse<AccountStatus>(reader.GetString(reader.GetOrdinal("status"))),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(reader.GetOrdinal("metadata"))) ?? new()
        };
    }
}
