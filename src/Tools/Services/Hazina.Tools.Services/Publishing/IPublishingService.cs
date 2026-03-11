using HazinaStore.Models;

namespace Hazina.Tools.Services.Publishing;

/// <summary>
/// Generic publishing service abstraction supporting multiple platforms
/// Implementations: WordPress, Medium, Dev.to, Hashnode, Ghost, Blogger, etc.
/// </summary>
public interface IPublishingService
{
    /// <summary>
    /// Platform name for identification (e.g., "WordPress", "Medium", "Dev.to")
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Publish a blog post to the platform
    /// </summary>
    /// <param name="blogItem">Blog content to publish</param>
    /// <param name="connectionId">Platform-specific connection/credential ID</param>
    /// <returns>Platform-specific post ID</returns>
    Task<PublishingResult> PublishAsync(BlogItem blogItem, string connectionId);

    /// <summary>
    /// Update an existing published post
    /// </summary>
    Task<PublishingResult> UpdateAsync(BlogItem blogItem, string platformPostId, string connectionId);

    /// <summary>
    /// Delete/unpublish a post from the platform
    /// </summary>
    Task<bool> DeleteAsync(string platformPostId, string connectionId);

    /// <summary>
    /// Validate that connection credentials are working
    /// </summary>
    Task<bool> ValidateConnectionAsync(string connectionId);

    /// <summary>
    /// Get available categories/tags from platform (if supported)
    /// </summary>
    Task<List<string>> GetCategoriesAsync(string connectionId);
}

/// <summary>
/// Result of publishing operation with platform-specific details
/// </summary>
public class PublishingResult
{
    public bool Success { get; set; }
    public string? PlatformPostId { get; set; }
    public string? PublicUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Platform-specific metadata (e.g., slug, canonical URL, etc.)
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
