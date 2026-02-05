using Hazina.LLMs;

namespace Hazina.App.HazinaCoder.Core.Vision;

/// <summary>
/// Vision provider integration (OpenAI + Claude)
/// Iteration 38: Vision completion
/// </summary>
public class VisionProviderIntegration
{
    private readonly ILLMClient _client;
    private readonly VisionCache _cache;

    public VisionProviderIntegration(ILLMClient client, VisionCache cache)
    {
        _client = client;
        _cache = cache;
    }

    public async Task<string> AnalyzeImageAsync(string imagePath, string prompt)
    {
        // Check cache
        var cacheKey = $"{imagePath}:{prompt}";
        var cached = _cache.Get(cacheKey);
        if (cached != null)
            return cached;

        // Load image
        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var base64 = Convert.ToBase64String(imageBytes);

        // Analyze with LLM
        var messages = new List<HazinaChatMessage>
        {
            new()
            {
                Role = "user",
                Content = prompt,
                Images = new List<ImageData>
                {
                    new() { Base64Data = base64 }
                }
            }
        };

        var interaction = _client.StartChatInteraction(messages);
        var result = await interaction.GetResponseAsync();

        // Cache result
        _cache.Set(cacheKey, result);

        return result;
    }

    public async Task<List<string>> BatchAnalyzeAsync(List<string> imagePaths, string prompt)
    {
        var tasks = imagePaths.Select(path => AnalyzeImageAsync(path, prompt));
        return (await Task.WhenAll(tasks)).ToList();
    }
}

/// <summary>
/// Image comparison tool
/// Iteration 39: Advanced vision features
/// </summary>
public class ImageComparer
{
    private readonly VisionProviderIntegration _vision;

    public ImageComparer(VisionProviderIntegration vision)
    {
        _vision = vision;
    }

    public async Task<string> CompareImagesAsync(string image1Path, string image2Path)
    {
        var prompt = @"Compare these two images and describe:
1. What's different between them
2. What's the same
3. Which version is better (if applicable)

Be specific and detailed.";

        // Analyze both images
        var analysis1 = await _vision.AnalyzeImageAsync(image1Path, "Describe this image in detail");
        var analysis2 = await _vision.AnalyzeImageAsync(image2Path, "Describe this image in detail");

        // Compare
        var comparePrompt = $"Image 1: {analysis1}\n\nImage 2: {analysis2}\n\n{prompt}";

        // Return comparison (would need multi-image support in actual implementation)
        return $"Comparison based on individual analyses:\n{comparePrompt}";
    }
}
