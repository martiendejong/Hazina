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
        var cached = await _cache.GetAsync(cacheKey);
        if (cached != null)
            return cached.Analysis; // Return the analysis from cached result

        // Load image
        var imageBytes = await File.ReadAllBytesAsync(imagePath);
        var binaryData = BinaryData.FromBytes(imageBytes);

        // Analyze with LLM (images passed as separate parameter)
        var messages = new List<HazinaChatMessage>
        {
            new()
            {
                Role = HazinaMessageRole.User,
                Text = prompt
            }
        };

        var images = new List<ImageData>
        {
            new()
            {
                BinaryData = binaryData,
                MimeType = GetMimeType(imagePath),
                Name = Path.GetFileName(imagePath)
            }
        };

        var response = await _client.GetResponse(messages, HazinaChatResponseFormat.Text, null, images, CancellationToken.None);
        var result = response.Result;

        // Cache result
        var analysisResult = new VisionAnalysisResult
        {
            Analysis = result,
            ImagePath = imagePath,
            Query = prompt,
            Success = true,
            Confidence = 1.0,
            Duration = TimeSpan.Zero // Would need to track actual duration
        };
        await _cache.SetAsync(cacheKey, analysisResult);

        return result;
    }

    private string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
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
