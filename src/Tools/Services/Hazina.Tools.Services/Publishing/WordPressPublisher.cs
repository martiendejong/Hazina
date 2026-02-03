using HazinaStore.Services;
using HazinaStore.Models;
using System.Linq;

namespace Hazina.Tools.Services.Publishing;

/// <summary>
/// WordPress publishing implementation using WordPress REST API
/// Supports both WordPress.com and self-hosted WordPress
/// </summary>
public class WordPressPublisher : IPublishingService
{
    private readonly WordpressBlogService _wordpressService;

    public WordPressPublisher(WordpressBlogService wordpressService)
    {
        _wordpressService = wordpressService;
    }

    public string PlatformName => "WordPress";

    public async Task<PublishingResult> PublishAsync(BlogItem blogItem, string connectionId)
    {
        try
        {
            // Get category name from ID
            var categories = await _wordpressService.GetCategoriesAsync();
            var category = categories.FirstOrDefault(c => c.Id == blogItem.WordpressCategoryId);
            var categoryName = category?.name ?? "Uncategorized";

            // Create post using actual WordpressBlogService API
            (bool success, int postId, string url) = await _wordpressService.CreateBlogPostAsync(
                blogItem.Title,
                categoryName,
                blogItem.Body
            );

            if (!success)
            {
                return new PublishingResult
                {
                    Success = false,
                    ErrorMessage = "WordPress API returned failure"
                };
            }

            return new PublishingResult
            {
                Success = true,
                PlatformPostId = postId.ToString(),
                PublicUrl = url ?? $"/?p={postId}",
                PublishedAt = DateTime.UtcNow,
                Metadata = new Dictionary<string, string>
                {
                    ["categoryId"] = blogItem.WordpressCategoryId.ToString(),
                    ["categoryName"] = categoryName
                }
            };
        }
        catch (Exception ex)
        {
            return new PublishingResult
            {
                Success = false,
                ErrorMessage = $"WordPress publish failed: {ex.Message}"
            };
        }
    }

    public async Task<PublishingResult> UpdateAsync(BlogItem blogItem, string platformPostId, string connectionId)
    {
        // WordPress REST API supports updating posts via PUT request
        // For now, return not implemented - can be added later
        await Task.CompletedTask;
        return new PublishingResult
        {
            Success = false,
            ErrorMessage = "WordPress update not yet implemented"
        };
    }

    public async Task<bool> DeleteAsync(string platformPostId, string connectionId)
    {
        // WordPress REST API supports deleting posts
        // For now, return not implemented - can be added later
        await Task.CompletedTask;
        return false;
    }

    public async Task<bool> ValidateConnectionAsync(string connectionId)
    {
        try
        {
            var categories = await _wordpressService.GetCategoriesAsync();
            return categories != null && categories.Any();
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<string>> GetCategoriesAsync(string connectionId)
    {
        var categories = await _wordpressService.GetCategoriesAsync();
        return categories?.Select(c => c.name).ToList() ?? new List<string>();
    }

}
