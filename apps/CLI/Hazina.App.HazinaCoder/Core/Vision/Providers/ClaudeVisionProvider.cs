using System.Text;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Vision.Providers;

/// <summary>
/// Claude vision provider using Anthropic's Vision API
/// </summary>
public class ClaudeVisionProvider : IVisionProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public string ProviderName => "Claude (Anthropic)";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public ClaudeVisionProvider(
        string? apiKey = null,
        string model = "claude-3-5-sonnet-20241022",
        HttpClient? httpClient = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
        _model = model;
        _httpClient = httpClient ?? new HttpClient();

        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<VisionAnalysisResult> AnalyzeImageAsync(
        byte[] imageBytes,
        string mimeType,
        string query,
        VisionAnalysisOptions options)
    {
        if (!IsAvailable)
        {
            return new VisionAnalysisResult
            {
                Success = false,
                Error = "Claude API key not configured. Set ANTHROPIC_API_KEY environment variable."
            };
        }

        try
        {
            var base64Image = Convert.ToBase64String(imageBytes);

            var request = new
            {
                model = _model,
                max_tokens = options.MaxTokens,
                temperature = options.Temperature,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "image",
                                source = new
                                {
                                    type = "base64",
                                    media_type = mimeType,
                                    data = base64Image
                                }
                            },
                            new
                            {
                                type = "text",
                                text = query
                            }
                        }
                    }
                }
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.anthropic.com/v1/messages",
                content);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            var textContent = responseData
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            return new VisionAnalysisResult
            {
                Success = true,
                Analysis = textContent,
                Confidence = 0.95, // Claude doesn't provide confidence scores
                Metadata = new Dictionary<string, object>
                {
                    ["provider"] = ProviderName,
                    ["model"] = _model,
                    ["input_tokens"] = responseData.GetProperty("usage").GetProperty("input_tokens").GetInt32(),
                    ["output_tokens"] = responseData.GetProperty("usage").GetProperty("output_tokens").GetInt32()
                }
            };
        }
        catch (Exception ex)
        {
            return new VisionAnalysisResult
            {
                Success = false,
                Error = $"Claude API error: {ex.Message}"
            };
        }
    }
}
