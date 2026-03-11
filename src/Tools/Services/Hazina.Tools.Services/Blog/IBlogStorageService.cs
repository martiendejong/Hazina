using HazinaStore.Models;

namespace Hazina.Tools.Services.Blog;

/// <summary>
/// Generic blog storage abstraction supporting multiple storage providers (File, Database, etc.)
/// Configured via appsettings.json: "BlogStorage:Provider"
/// </summary>
public interface IBlogStorageService
{
    /// <summary>
    /// Add a new blog item to storage
    /// </summary>
    Task<BlogItem> AddBlogItemAsync(string projectId, BlogItem item);

    /// <summary>
    /// Update an existing blog item
    /// </summary>
    Task<BlogItem> UpdateBlogItemAsync(string projectId, BlogItem item);

    /// <summary>
    /// Delete a blog item (may be soft or hard delete depending on provider)
    /// </summary>
    Task DeleteBlogItemAsync(string projectId, string blogId);

    /// <summary>
    /// Get a single blog item by ID
    /// </summary>
    Task<BlogItem?> GetBlogItemAsync(string projectId, string blogId);

    /// <summary>
    /// Get all blog items for a project (sorted by scheduled/published date)
    /// </summary>
    Task<List<BlogItem>> GetBlogItemsAsync(string projectId);

    /// <summary>
    /// Get blog items that are scheduled for publishing and due now
    /// Used by background publishing job
    /// </summary>
    Task<List<BlogItem>> GetDueForPublishingAsync(DateTime cutoffTime);

    /// <summary>
    /// Mark blog item as published with timestamp
    /// </summary>
    Task MarkAsPublishedAsync(string projectId, string blogId, DateTime publishedAt);

    /// <summary>
    /// Get storage provider type for diagnostics/logging
    /// </summary>
    string GetProviderType();
}
