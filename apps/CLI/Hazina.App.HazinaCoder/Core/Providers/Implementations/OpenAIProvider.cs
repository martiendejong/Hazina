using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Providers.Implementations;

/// <summary>
/// OpenAI GPT provider implementation
/// </summary>
public class OpenAIProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.openai.com/v1";

    public string ProviderName => "OpenAI";

    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public ProviderCapabilities Capabilities => new()
    {
        SupportsVision = true,
        SupportsToolUse = true,
        SupportsStreaming = true,
        SupportsEmbeddings = true,
        SupportsFunctionCalling = true,
        MaxContextLength = 128_000,
        MaxOutputTokens = 16_384,
        AvailableModels = new()
        {
            "gpt-4o",
            "gpt-4o-mini",
            "gpt-4-turbo",
            "gpt-4",
            "gpt-3.5-turbo"
        }
    };

    public OpenAIProvider(string? apiKey = null, HttpClient? httpClient = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;
        _httpClient = httpClient ?? new HttpClient();

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<LLMResponse> GenerateAsync(LLMRequest request, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            var apiRequest = BuildApiRequest(request);
            var json = JsonSerializer.Serialize(apiRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/chat/completions",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            var choice = responseData.GetProperty("choices")[0];
            var message = choice.GetProperty("message");
            var textContent = message.GetProperty("content").GetString() ?? string.Empty;

            var usage = responseData.GetProperty("usage");
            var inputTokens = usage.GetProperty("prompt_tokens").GetInt32();
            var outputTokens = usage.GetProperty("completion_tokens").GetInt32();

            var pricing = GetPricing(request.Model);
            var cost = pricing.CalculateCost(inputTokens, outputTokens);

            return new LLMResponse
            {
                Success = true,
                Content = textContent,
                Model = request.Model,
                Provider = ProviderName,
                Duration = DateTime.UtcNow - startTime,
                Usage = new LLMUsage
                {
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    Cost = cost
                }
            };
        }
        catch (Exception ex)
        {
            return new LLMResponse
            {
                Success = false,
                Error = $"OpenAI API error: {ex.Message}",
                Provider = ProviderName,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    public async IAsyncEnumerable<LLMStreamChunk> GenerateStreamAsync(
        LLMRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var apiRequest = BuildApiRequest(request);
        apiRequest["stream"] = true;

        var json = JsonSerializer.Serialize(apiRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/chat/completions")
        {
            Content = content
        };

        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                continue;

            var dataJson = line["data: ".Length..];

            if (dataJson == "[DONE]")
            {
                yield return new LLMStreamChunk
                {
                    Content = string.Empty,
                    IsComplete = true
                };
                yield break;
            }

            var data = JsonSerializer.Deserialize<JsonElement>(dataJson);
            var choices = data.GetProperty("choices");

            if (choices.GetArrayLength() > 0)
            {
                var delta = choices[0].GetProperty("delta");

                if (delta.TryGetProperty("content", out var content))
                {
                    yield return new LLMStreamChunk
                    {
                        Content = content.GetString() ?? string.Empty,
                        IsComplete = false
                    };
                }
            }
        }
    }

    public async Task<EmbeddingResponse> GenerateEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var apiRequest = new
            {
                model = request.Model.StartsWith("text-embedding") ? request.Model : "text-embedding-3-small",
                input = request.Texts
            };

            var json = JsonSerializer.Serialize(apiRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{BaseUrl}/embeddings",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            var embeddings = new List<float[]>();
            foreach (var item in responseData.GetProperty("data").EnumerateArray())
            {
                var embedding = item.GetProperty("embedding")
                    .EnumerateArray()
                    .Select(e => (float)e.GetDouble())
                    .ToArray();

                embeddings.Add(embedding);
            }

            var usage = responseData.GetProperty("usage");
            var tokensUsed = usage.GetProperty("total_tokens").GetInt32();

            // Embedding pricing: $0.13 per 1M tokens for text-embedding-3-small
            var cost = tokensUsed / 1_000_000.0 * 0.13;

            return new EmbeddingResponse
            {
                Success = true,
                Embeddings = embeddings,
                TokensUsed = tokensUsed,
                Cost = cost
            };
        }
        catch (Exception ex)
        {
            return new EmbeddingResponse
            {
                Success = false,
                Error = $"OpenAI embedding error: {ex.Message}"
            };
        }
    }

    public async Task<ProviderHealth> GetHealthAsync()
    {
        try
        {
            var testRequest = new LLMRequest
            {
                Model = "gpt-4o-mini",
                Messages = new List<LLMMessage>
                {
                    new() { Role = "user", Content = "Say 'healthy'" }
                },
                MaxTokens = 10
            };

            var startTime = DateTime.UtcNow;
            var response = await GenerateAsync(testRequest);
            var latency = DateTime.UtcNow - startTime;

            return new ProviderHealth
            {
                IsHealthy = response.Success,
                Status = response.Success ? "healthy" : "degraded",
                Latency = latency,
                LastCheck = DateTime.UtcNow,
                Message = response.Error
            };
        }
        catch (Exception ex)
        {
            return new ProviderHealth
            {
                IsHealthy = false,
                Status = "down",
                LastCheck = DateTime.UtcNow,
                Message = ex.Message
            };
        }
    }

    public ProviderPricing GetPricing(string model)
    {
        // Pricing as of 2025 (per 1M tokens)
        return model switch
        {
            "gpt-4o" => new ProviderPricing
            {
                InputTokenCost = 2.50,
                OutputTokenCost = 10.0
            },
            "gpt-4o-mini" => new ProviderPricing
            {
                InputTokenCost = 0.15,
                OutputTokenCost = 0.60
            },
            "gpt-4-turbo" => new ProviderPricing
            {
                InputTokenCost = 10.0,
                OutputTokenCost = 30.0
            },
            "gpt-4" => new ProviderPricing
            {
                InputTokenCost = 30.0,
                OutputTokenCost = 60.0
            },
            "gpt-3.5-turbo" => new ProviderPricing
            {
                InputTokenCost = 0.50,
                OutputTokenCost = 1.50
            },
            _ => new ProviderPricing
            {
                InputTokenCost = 2.50,
                OutputTokenCost = 10.0
            }
        };
    }

    private Dictionary<string, object> BuildApiRequest(LLMRequest request)
    {
        var apiRequest = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["temperature"] = request.Temperature,
            ["top_p"] = request.TopP
        };

        // Convert messages
        var messages = new List<object>();

        // Add system message if present
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            messages.Add(new
            {
                role = "system",
                content = request.SystemPrompt
            });
        }

        foreach (var message in request.Messages)
        {
            if (message.Contents?.Any() == true)
            {
                // Multimodal message
                var content = message.Contents.Select(c => c.Type switch
                {
                    "text" => new { type = "text", text = c.Text },
                    "image" => new
                    {
                        type = "image_url",
                        image_url = new
                        {
                            url = c.Image!.Type == "base64"
                                ? $"data:{c.Image.MimeType};base64,{c.Image.Data}"
                                : c.Image.Data
                        }
                    },
                    _ => throw new ArgumentException($"Unknown content type: {c.Type}")
                }).ToList();

                messages.Add(new { role = message.Role, content });
            }
            else
            {
                // Text-only message
                messages.Add(new { role = message.Role, content = message.Content });
            }
        }

        apiRequest["messages"] = messages;

        // Tools (function calling)
        if (request.Tools?.Any() == true)
        {
            apiRequest["tools"] = request.Tools.Select(t => new
            {
                type = "function",
                function = new
                {
                    name = t.Name,
                    description = t.Description,
                    parameters = t.Parameters
                }
            }).ToList();
        }

        return apiRequest;
    }
}
