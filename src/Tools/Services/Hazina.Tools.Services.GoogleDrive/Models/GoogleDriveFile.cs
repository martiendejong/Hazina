using System;
using System.ComponentModel.DataAnnotations;

namespace Hazina.Tools.Services.GoogleDrive.Models;

/// <summary>
/// Represents a file from Google Drive that has been synced.
/// Tracks sync and embedding status.
/// </summary>
public class GoogleDriveFile
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string ConnectionId { get; set; } = string.Empty;

    [Required]
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// The unique ID of the file in Google Drive.
    /// </summary>
    [Required]
    public string DriveFileId { get; set; } = string.Empty;

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string MimeType { get; set; } = string.Empty;

    /// <summary>
    /// Path in Google Drive (e.g., "My Drive/Documents/file.pdf").
    /// Null for root-level files.
    /// </summary>
    public string? DrivePath { get; set; }

    public long FileSize { get; set; }

    public DateTime ModifiedTime { get; set; }

    /// <summary>
    /// Google Drive web view link for opening in browser.
    /// </summary>
    public string? WebViewLink { get; set; }

    /// <summary>
    /// Local path if file was downloaded for embedding.
    /// Format: "Uploads/google-drive/{filename}"
    /// Null if file not downloaded.
    /// </summary>
    public string? LocalPath { get; set; }

    public SyncStatus SyncStatus { get; set; } = SyncStatus.NotSynced;

    public EmbeddingStatus EmbeddingStatus { get; set; } = EmbeddingStatus.NotEmbedded;

    public DateTime? LastSyncedAt { get; set; }

    public DateTime? LastEmbeddedAt { get; set; }

    /// <summary>
    /// JSON metadata for additional Drive file properties.
    /// </summary>
    public string? Metadata { get; set; }
}

public enum SyncStatus
{
    NotSynced,
    Syncing,
    Synced,
    Error
}

public enum EmbeddingStatus
{
    NotEmbedded,
    Queued,
    Embedding,
    Embedded,
    Error
}
