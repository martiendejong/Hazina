namespace Hazina.Tools.Services.Social.Models;

/// <summary>
/// Represents media attached to content (images, videos, documents)
/// </summary>
public class ContentMedia
{
    public string Id { get; set; } = "";
    public string ContentId { get; set; } = "";
    public MediaType Type { get; set; }
    public string Url { get; set; } = "";
    public string? LocalUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public int? DurationSeconds { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? MimeType { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsFeatured { get; set; }
    public string? Caption { get; set; }
    public string? AltText { get; set; }
    public DateTime ImportedAt { get; set; }
}

public enum MediaType
{
    Image,
    Video,
    Document,
    Audio
}
