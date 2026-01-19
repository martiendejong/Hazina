namespace Hazina.Tools.Services.Social.Models;

/// <summary>
/// Represents a comment on content (works for all platforms)
/// </summary>
public class ContentComment
{
    public string Id { get; set; } = "";
    public string ContentId { get; set; } = "";
    public string? ParentCommentId { get; set; } // For threaded comments
    public string SourceType { get; set; } = ""; // wordpress, linkedin, etc.
    public string SourceCommentId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string? AuthorId { get; set; }
    public string? AuthorAvatarUrl { get; set; }
    public string? AuthorProfileUrl { get; set; }
    public string CommentText { get; set; } = "";
    public string? CommentHtml { get; set; }
    public int LikeCount { get; set; }
    public int ReplyCount { get; set; }
    public DateTime PostedAt { get; set; }
    public DateTime ImportedAt { get; set; }
    public bool IsDeleted { get; set; }

    // AI Analysis
    public string? Sentiment { get; set; }
    public bool IsQuestion { get; set; }
    public bool RequiresResponse { get; set; }
}
