using System.Text;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Vision.Providers;

/// <summary>
/// OpenAI GPT-4 Vision provider
/// </summary>
public class OpenAIVisionProvider : IVisionProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public string ProviderName => "OpenAI GPT-4 Vision";
    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public OpenAIVisionProvider(
        string? apiKey = null,
        string model = "gpt-4o",
        HttpClient? httpClient = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        _model = model;
        _httpClient = httpClient ?? new HttpClient();

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
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
                Error = "OpenAI API key not configured. Set OPENAI_API_KEY environment variable."
            };
        }

        try
        {
            var base64Image = Convert.ToBase64String(imageBytes);
            var dataUrl = $"data:{mimeType};base64,{base64Image}";

            var detailLevel = options.DetailLevel switch
            {
                VisionDetailLevel.Low => "low",
                VisionDetailLevel.High => "high",
                _ => "auto"
            };

            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = query
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = dataUrl,
                                    detail = detailLevel
                                }
                            }
                        }
                    }
                },
                max_tokens = options.MaxTokens,
                temperature = options.Temperature
            };

            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                "https://api.openai.com/v1/chat/completions",
                content);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            var textContent = responseData
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;

            return new VisionAnalysisResult
            {
                Success = true,
                Analysis = textContent,
                Confidence = 0.90,
                Metadata = new Dictionary<string, object>
                {
                    ["provider"] = ProviderName,
                    ["model"] = _model,
                    ["prompt_tokens"] = responseData.GetProperty("usage").GetProperty("prompt_tokens").GetInt32(),
                    ["completion_tokens"] = responseData.GetProperty("usage").GetProperty("completion_tokens").GetInt32()
                }
            };
        }
        catch (Exception ex)
        {
            return new VisionAnalysisResult
            {
                Success = false,
                Error = $"OpenAI API error: {ex.Message}"
            };
        }
    }
}
