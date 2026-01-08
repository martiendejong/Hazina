using System;
using System.ComponentModel.DataAnnotations;

namespace Hazina.Tools.Services.GoogleDrive.Models;

/// <summary>
/// Represents a Google Drive connection for a project.
/// Stores OAuth tokens and sync settings.
/// </summary>
public class GoogleDriveConnection
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string ProjectId { get; set; } = string.Empty;

    [Required]
    public string GoogleAccountId { get; set; } = string.Empty;

    [Required]
    public string GoogleEmail { get; set; } = string.Empty;

    /// <summary>
    /// Access token for Google Drive API calls.
    /// TODO: Encrypt in production.
    /// </summary>
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh token for obtaining new access tokens.
    /// TODO: Encrypt in production.
    /// </summary>
    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    public DateTime TokenExpiresAt { get; set; }

    public DateTime ConnectedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastSyncAt { get; set; }

    public ConnectionStatus Status { get; set; } = ConnectionStatus.Active;

    /// <summary>
    /// If true, files will be synced automatically in background job.
    /// </summary>
    public bool SyncEnabled { get; set; } = true;

    /// <summary>
    /// If true, synced files will be embedded for semantic search.
    /// If false, files only accessible via API (no relevance search).
    /// </summary>
    public bool EmbeddingEnabled { get; set; } = false;
}

public enum ConnectionStatus
{
    Active,
    TokenExpired,
    Error,
    Disconnected
}
