using System;
using System.Collections.Generic;

namespace Hazina.Tools.Services.GoogleDrive.Models;

/// <summary>
/// DTO for Google Drive file information returned to client.
/// </summary>
public class GoogleDriveFileDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime ModifiedTime { get; set; }
    public string? WebViewLink { get; set; }
    public bool IsSynced { get; set; }
    public bool IsEmbedded { get; set; }
}

/// <summary>
/// Result of sync operation.
/// </summary>
public class DrivesSyncResult
{
    public int FilesFound { get; set; }
    public int FilesAdded { get; set; }
    public int FilesUpdated { get; set; }
    public int FilesDeleted { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}
