using HazinaStore;
using HazinaStore.Models;

namespace Hazina.Tools.Services.Blog;

/// <summary>
/// File-based blog storage implementation using JSON files
/// Storage location: /projects/{projectId}/blogs/{id}.json
/// </summary>
public class FileBlogStorageService : IBlogStorageService
{
    private readonly BlogService _blogService;

    public FileBlogStorageService(BlogService blogService)
    {
        _blogService = blogService;
    }

    public Task<BlogItem> AddBlogItemAsync(string projectId, BlogItem item)
    {
        _blogService.AddBlogItem(projectId, item);
        return Task.FromResult(item);
    }

    public Task<BlogItem> UpdateBlogItemAsync(string projectId, BlogItem item)
    {
        _blogService.UpdateBlogItem(projectId, item.Id, item);
        return Task.FromResult(item);
    }

    public Task DeleteBlogItemAsync(string projectId, string blogId)
    {
        _blogService.DeleteBlogItem(projectId, blogId);
        return Task.CompletedTask;
    }

    public Task<BlogItem?> GetBlogItemAsync(string projectId, string blogId)
    {
        var item = _blogService.GetBlogItem(projectId, blogId);
        return Task.FromResult<BlogItem?>(item);
    }

    public Task<List<BlogItem>> GetBlogItemsAsync(string projectId)
    {
        var items = _blogService.GetBlogItems(projectId);
        return Task.FromResult(items);
    }

    public async Task<List<BlogItem>> GetDueForPublishingAsync(DateTime cutoffTime)
    {
        // File-based storage doesn't have cross-project queries
        // This would need to scan all projects - not efficient
        // For now, return empty list - scheduled publishing works better with database
        await Task.CompletedTask;
        return new List<BlogItem>();
    }

    public async Task MarkAsPublishedAsync(string projectId, string blogId, DateTime publishedAt)
    {
        var item = await GetBlogItemAsync(projectId, blogId);
        if (item != null)
        {
            item.PublishedDateTime = publishedAt;
            await UpdateBlogItemAsync(projectId, item);
        }
    }

    public string GetProviderType() => "File";
}
