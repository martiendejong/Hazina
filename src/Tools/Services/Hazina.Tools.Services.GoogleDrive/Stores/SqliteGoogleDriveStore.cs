using System.Text.Json;

using Hazina.Tools.Services.GoogleDrive.Abstractions;
using Hazina.Tools.Services.GoogleDrive.Models;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace Hazina.Tools.Services.GoogleDrive.Stores;

/// <summary>
/// SQLite-based storage for Google Drive connections and files.
/// </summary>
public class SqliteGoogleDriveStore : IGoogleDriveStore
{
    private readonly ILogger<SqliteGoogleDriveStore> _logger;
    private readonly Func<string, string> _databasePathResolver;

    public SqliteGoogleDriveStore(
        ILogger<SqliteGoogleDriveStore> logger,
        Func<string, string> databasePathResolver)
    {
        _logger = logger;
        _databasePathResolver = databasePathResolver;
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
            CREATE TABLE IF NOT EXISTS google_drive_connections (
                id TEXT PRIMARY KEY,
                project_id TEXT NOT NULL,
                google_account_id TEXT NOT NULL,
                google_email TEXT NOT NULL,
                access_token TEXT NOT NULL,
                refresh_token TEXT NOT NULL,
                token_expires_at TEXT NOT NULL,
                connected_at TEXT NOT NULL,
                last_sync_at TEXT,
                status TEXT NOT NULL,
                sync_enabled INTEGER NOT NULL DEFAULT 1,
                embedding_enabled INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_drive_connections_project ON google_drive_connections(project_id);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_drive_connections_unique ON google_drive_connections(project_id, google_account_id);

            CREATE TABLE IF NOT EXISTS google_drive_files (
                id TEXT PRIMARY KEY,
                connection_id TEXT NOT NULL,
                project_id TEXT NOT NULL,
                drive_file_id TEXT NOT NULL,
                file_name TEXT NOT NULL,
                mime_type TEXT NOT NULL,
                drive_path TEXT,
                file_size INTEGER NOT NULL,
                modified_time TEXT NOT NULL,
                web_view_link TEXT,
                local_path TEXT,
                sync_status TEXT NOT NULL,
                embedding_status TEXT NOT NULL,
                last_synced_at TEXT,
                last_embedded_at TEXT,
                metadata TEXT,
                FOREIGN KEY (connection_id) REFERENCES google_drive_connections(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS idx_drive_files_connection ON google_drive_files(connection_id);
            CREATE INDEX IF NOT EXISTS idx_drive_files_project ON google_drive_files(project_id);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_drive_files_unique ON google_drive_files(connection_id, drive_file_id);
        ";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    #region Connections

    public async Task SaveConnectionAsync(
        string projectId,
        GoogleDriveConnection connection,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO google_drive_connections
            (id, project_id, google_account_id, google_email, access_token, refresh_token,
             token_expires_at, connected_at, last_sync_at, status, sync_enabled, embedding_enabled)
            VALUES (@id, @project_id, @google_account_id, @google_email, @access_token, @refresh_token,
                    @token_expires_at, @connected_at, @last_sync_at, @status, @sync_enabled, @embedding_enabled)";

        cmd.Parameters.AddWithValue("@id", connection.Id);
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@google_account_id", connection.GoogleAccountId);
        cmd.Parameters.AddWithValue("@google_email", connection.GoogleEmail);
        cmd.Parameters.AddWithValue("@access_token", connection.AccessToken);
        cmd.Parameters.AddWithValue("@refresh_token", connection.RefreshToken);
        cmd.Parameters.AddWithValue("@token_expires_at", connection.TokenExpiresAt.ToString("O"));
        cmd.Parameters.AddWithValue("@connected_at", connection.ConnectedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@last_sync_at", connection.LastSyncAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@status", connection.Status.ToString());
        cmd.Parameters.AddWithValue("@sync_enabled", connection.SyncEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@embedding_enabled", connection.EmbeddingEnabled ? 1 : 0);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Saved Google Drive connection {ConnectionId} for project {ProjectId}", connection.Id, projectId);
    }

    public async Task<List<GoogleDriveConnection>> GetConnectionsAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT * FROM google_drive_connections WHERE project_id = @project_id ORDER BY connected_at DESC";
        cmd.Parameters.AddWithValue("@project_id", projectId);

        var connections = new List<GoogleDriveConnection>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            connections.Add(ReadConnection(reader));
        }

        return connections;
    }

    public async Task<GoogleDriveConnection?> GetConnectionAsync(
        string projectId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT * FROM google_drive_connections WHERE project_id = @project_id AND id = @id";
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@id", connectionId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadConnection(reader);
        }

        return null;
    }

    public async Task<bool> RemoveConnectionAsync(
        string projectId,
        string connectionId,
        bool deleteFiles = true,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        if (deleteFiles)
        {
            using var deleteFilesCmd = db.CreateCommand();
            deleteFilesCmd.CommandText = "DELETE FROM google_drive_files WHERE project_id = @project_id AND connection_id = @connection_id";
            deleteFilesCmd.Parameters.AddWithValue("@project_id", projectId);
            deleteFilesCmd.Parameters.AddWithValue("@connection_id", connectionId);
            await deleteFilesCmd.ExecuteNonQueryAsync(cancellationToken);
        }

        using var cmd = db.CreateCommand();
        cmd.CommandText = "DELETE FROM google_drive_connections WHERE project_id = @project_id AND id = @id";
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@id", connectionId);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogInformation("Removed Google Drive connection {ConnectionId} from project {ProjectId}, deleted files: {DeleteFiles}",
            connectionId, projectId, deleteFiles);

        return affected > 0;
    }

    public async Task UpdateTokensAsync(
        string projectId,
        string connectionId,
        string accessToken,
        string? refreshToken,
        DateTime expiresAt,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
            UPDATE google_drive_connections
            SET access_token = @access_token,
                refresh_token = @refresh_token,
                token_expires_at = @token_expires_at,
                status = @status
            WHERE project_id = @project_id AND id = @id";

        cmd.Parameters.AddWithValue("@access_token", accessToken);
        cmd.Parameters.AddWithValue("@refresh_token", refreshToken ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@token_expires_at", expiresAt.ToString("O"));
        cmd.Parameters.AddWithValue("@status", ConnectionStatus.Active.ToString());
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@id", connectionId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Updated tokens for Google Drive connection {ConnectionId} in project {ProjectId}", connectionId, projectId);
    }

    public async Task UpdateSyncSettingsAsync(
        string projectId,
        string connectionId,
        bool syncEnabled,
        bool embeddingEnabled,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
            UPDATE google_drive_connections
            SET sync_enabled = @sync_enabled,
                embedding_enabled = @embedding_enabled
            WHERE project_id = @project_id AND id = @id";

        cmd.Parameters.AddWithValue("@sync_enabled", syncEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@embedding_enabled", embeddingEnabled ? 1 : 0);
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@id", connectionId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Updated sync settings for Google Drive connection {ConnectionId} in project {ProjectId}", connectionId, projectId);
    }

    private static GoogleDriveConnection ReadConnection(SqliteDataReader reader)
    {
        return new GoogleDriveConnection
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            ProjectId = reader.GetString(reader.GetOrdinal("project_id")),
            GoogleAccountId = reader.GetString(reader.GetOrdinal("google_account_id")),
            GoogleEmail = reader.GetString(reader.GetOrdinal("google_email")),
            AccessToken = reader.GetString(reader.GetOrdinal("access_token")),
            RefreshToken = reader.GetString(reader.GetOrdinal("refresh_token")),
            TokenExpiresAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("token_expires_at"))),
            ConnectedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("connected_at"))),
            LastSyncAt = reader.IsDBNull(reader.GetOrdinal("last_sync_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_sync_at"))),
            Status = Enum.Parse<ConnectionStatus>(reader.GetString(reader.GetOrdinal("status"))),
            SyncEnabled = reader.GetInt32(reader.GetOrdinal("sync_enabled")) == 1,
            EmbeddingEnabled = reader.GetInt32(reader.GetOrdinal("embedding_enabled")) == 1
        };
    }

    #endregion

    #region Files

    public async Task SaveFileAsync(
        string projectId,
        GoogleDriveFile file,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO google_drive_files
            (id, connection_id, project_id, drive_file_id, file_name, mime_type, drive_path,
             file_size, modified_time, web_view_link, local_path, sync_status, embedding_status,
             last_synced_at, last_embedded_at, metadata)
            VALUES (@id, @connection_id, @project_id, @drive_file_id, @file_name, @mime_type, @drive_path,
                    @file_size, @modified_time, @web_view_link, @local_path, @sync_status, @embedding_status,
                    @last_synced_at, @last_embedded_at, @metadata)";

        cmd.Parameters.AddWithValue("@id", file.Id);
        cmd.Parameters.AddWithValue("@connection_id", file.ConnectionId);
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@drive_file_id", file.DriveFileId);
        cmd.Parameters.AddWithValue("@file_name", file.FileName);
        cmd.Parameters.AddWithValue("@mime_type", file.MimeType);
        cmd.Parameters.AddWithValue("@drive_path", file.DrivePath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@file_size", file.FileSize);
        cmd.Parameters.AddWithValue("@modified_time", file.ModifiedTime.ToString("O"));
        cmd.Parameters.AddWithValue("@web_view_link", file.WebViewLink ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@local_path", file.LocalPath ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@sync_status", file.SyncStatus.ToString());
        cmd.Parameters.AddWithValue("@embedding_status", file.EmbeddingStatus.ToString());
        cmd.Parameters.AddWithValue("@last_synced_at", file.LastSyncedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@last_embedded_at", file.LastEmbeddedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@metadata", file.Metadata ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Saved Google Drive file {FileId} ({FileName}) for project {ProjectId}", file.Id, file.FileName, projectId);
    }

    public async Task<List<GoogleDriveFile>> GetFilesAsync(
        string projectId,
        string connectionId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT * FROM google_drive_files WHERE project_id = @project_id AND connection_id = @connection_id ORDER BY modified_time DESC";
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@connection_id", connectionId);

        var files = new List<GoogleDriveFile>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            files.Add(ReadFile(reader));
        }

        return files;
    }

    public async Task<GoogleDriveFile?> GetFileByDriveIdAsync(
        string projectId,
        string connectionId,
        string driveFileId,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT * FROM google_drive_files WHERE project_id = @project_id AND connection_id = @connection_id AND drive_file_id = @drive_file_id";
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@connection_id", connectionId);
        cmd.Parameters.AddWithValue("@drive_file_id", driveFileId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            return ReadFile(reader);
        }

        return null;
    }

    public async Task DeleteFilesNotInListAsync(
        string projectId,
        string connectionId,
        List<string> driveFileIds,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        var placeholders = string.Join(",", driveFileIds.Select((_, i) => $"@id{i}"));
        var sql = $"DELETE FROM google_drive_files WHERE project_id = @project_id AND connection_id = @connection_id AND drive_file_id NOT IN ({placeholders})";

        using var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@connection_id", connectionId);

        for (int i = 0; i < driveFileIds.Count; i++)
        {
            cmd.Parameters.AddWithValue($"@id{i}", driveFileIds[i]);
        }

        var deleted = await cmd.ExecuteNonQueryAsync(cancellationToken);
        if (deleted > 0)
        {
            _logger.LogInformation("Deleted {Count} files no longer in Drive for connection {ConnectionId}", deleted, connectionId);
        }
    }

    public async Task UpdateFileSyncStatusAsync(
        string projectId,
        string fileId,
        SyncStatus syncStatus,
        DateTime? lastSyncedAt = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
            UPDATE google_drive_files
            SET sync_status = @sync_status,
                last_synced_at = @last_synced_at
            WHERE project_id = @project_id AND id = @id";

        cmd.Parameters.AddWithValue("@sync_status", syncStatus.ToString());
        cmd.Parameters.AddWithValue("@last_synced_at", lastSyncedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@id", fileId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateFileEmbeddingStatusAsync(
        string projectId,
        string fileId,
        EmbeddingStatus embeddingStatus,
        DateTime? lastEmbeddedAt = null,
        CancellationToken cancellationToken = default)
    {
        await using var db = await GetConnectionAsync(projectId, cancellationToken);

        using var cmd = db.CreateCommand();
        cmd.CommandText = @"
            UPDATE google_drive_files
            SET embedding_status = @embedding_status,
                last_embedded_at = @last_embedded_at
            WHERE project_id = @project_id AND id = @id";

        cmd.Parameters.AddWithValue("@embedding_status", embeddingStatus.ToString());
        cmd.Parameters.AddWithValue("@last_embedded_at", lastEmbeddedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@project_id", projectId);
        cmd.Parameters.AddWithValue("@id", fileId);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static GoogleDriveFile ReadFile(SqliteDataReader reader)
    {
        return new GoogleDriveFile
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            ConnectionId = reader.GetString(reader.GetOrdinal("connection_id")),
            ProjectId = reader.GetString(reader.GetOrdinal("project_id")),
            DriveFileId = reader.GetString(reader.GetOrdinal("drive_file_id")),
            FileName = reader.GetString(reader.GetOrdinal("file_name")),
            MimeType = reader.GetString(reader.GetOrdinal("mime_type")),
            DrivePath = reader.IsDBNull(reader.GetOrdinal("drive_path")) ? null : reader.GetString(reader.GetOrdinal("drive_path")),
            FileSize = reader.GetInt64(reader.GetOrdinal("file_size")),
            ModifiedTime = DateTime.Parse(reader.GetString(reader.GetOrdinal("modified_time"))),
            WebViewLink = reader.IsDBNull(reader.GetOrdinal("web_view_link")) ? null : reader.GetString(reader.GetOrdinal("web_view_link")),
            LocalPath = reader.IsDBNull(reader.GetOrdinal("local_path")) ? null : reader.GetString(reader.GetOrdinal("local_path")),
            SyncStatus = Enum.Parse<SyncStatus>(reader.GetString(reader.GetOrdinal("sync_status"))),
            EmbeddingStatus = Enum.Parse<EmbeddingStatus>(reader.GetString(reader.GetOrdinal("embedding_status"))),
            LastSyncedAt = reader.IsDBNull(reader.GetOrdinal("last_synced_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_synced_at"))),
            LastEmbeddedAt = reader.IsDBNull(reader.GetOrdinal("last_embedded_at")) ? null : DateTime.Parse(reader.GetString(reader.GetOrdinal("last_embedded_at"))),
            Metadata = reader.IsDBNull(reader.GetOrdinal("metadata")) ? null : reader.GetString(reader.GetOrdinal("metadata"))
        };
    }

    #endregion
}
