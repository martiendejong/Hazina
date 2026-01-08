using Hazina.Tools.Services.GoogleDrive.Models;

namespace Hazina.Tools.Services.GoogleDrive.Abstractions;

/// <summary>
/// Storage interface for Google Drive connections and files.
/// Manages OAuth tokens, file metadata, and sync status.
/// </summary>
public interface IGoogleDriveStore
{
    /// <summary>
    /// Saves a Google Drive connection.
    /// </summary>
    Task SaveConnectionAsync(
        string projectId,
        GoogleDriveConnection connection,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all Google Drive connections for a project.
    /// </summary>
    Task<List<GoogleDriveConnection>> GetConnectionsAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific Google Drive connection.
    /// </summary>
    Task<GoogleDriveConnection?> GetConnectionAsync(
        string projectId,
        string connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a Google Drive connection and optionally its files.
    /// </summary>
    Task<bool> RemoveConnectionAsync(
        string projectId,
        string connectionId,
        bool deleteFiles = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates tokens for a connection.
    /// </summary>
    Task UpdateTokensAsync(
        string projectId,
        string connectionId,
        string accessToken,
        string? refreshToken,
        DateTime expiresAt,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates sync settings for a connection.
    /// </summary>
    Task UpdateSyncSettingsAsync(
        string projectId,
        string connectionId,
        bool syncEnabled,
        bool embeddingEnabled,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a Google Drive file.
    /// </summary>
    Task SaveFileAsync(
        string projectId,
        GoogleDriveFile file,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all files for a connection.
    /// </summary>
    Task<List<GoogleDriveFile>> GetFilesAsync(
        string projectId,
        string connectionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific file by Drive file ID.
    /// </summary>
    Task<GoogleDriveFile?> GetFileByDriveIdAsync(
        string projectId,
        string connectionId,
        string driveFileId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes files that are no longer in Drive.
    /// </summary>
    Task DeleteFilesNotInListAsync(
        string projectId,
        string connectionId,
        List<string> driveFileIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates file sync status.
    /// </summary>
    Task UpdateFileSyncStatusAsync(
        string projectId,
        string fileId,
        SyncStatus syncStatus,
        DateTime? lastSyncedAt = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates file embedding status.
    /// </summary>
    Task UpdateFileEmbeddingStatusAsync(
        string projectId,
        string fileId,
        EmbeddingStatus embeddingStatus,
        DateTime? lastEmbeddedAt = null,
        CancellationToken cancellationToken = default);
}
