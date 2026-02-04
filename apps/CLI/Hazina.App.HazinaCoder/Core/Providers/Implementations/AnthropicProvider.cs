using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace Hazina.App.HazinaCoder.Core.Providers.Implementations;

/// <summary>
/// Anthropic Claude provider implementation
/// </summary>
public class AnthropicProvider : ILLMProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private const string BaseUrl = "https://api.anthropic.com/v1";

    public string ProviderName => "Anthropic";

    public bool IsAvailable => !string.IsNullOrEmpty(_apiKey);

    public ProviderCapabilities Capabilities => new()
    {
        SupportsVision = true,
        SupportsToolUse = true,
        SupportsStreaming = true,
        SupportsEmbeddings = false,
        SupportsFunctionCalling = true,
        MaxContextLength = 200_000,
        MaxOutputTokens = 8_192,
        AvailableModels = new()
        {
            "claude-3-5-sonnet-20241022",
            "claude-3-5-haiku-20241022",
            "claude-3-opus-20240229",
            "claude-3-sonnet-20240229",
            "claude-3-haiku-20240307"
        }
    };

    public AnthropicProvider(string? apiKey = null, HttpClient? httpClient = null)
    {
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
        _httpClient = httpClient ?? new HttpClient();

        _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
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
                $"{BaseUrl}/messages",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var responseData = JsonSerializer.Deserialize<JsonElement>(responseJson);

            var textContent = responseData
                .GetProperty("content")[0]
                .GetProperty("text")
                .GetString() ?? string.Empty;

            var usage = responseData.GetProperty("usage");
            var inputTokens = usage.GetProperty("input_tokens").GetInt32();
            var outputTokens = usage.GetProperty("output_tokens").GetInt32();

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
                Error = $"Anthropic API error: {ex.Message}",
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

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/messages")
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
            var type = data.GetProperty("type").GetString();

            if (type == "content_block_delta")
            {
                var delta = data.GetProperty("delta");
                if (delta.TryGetProperty("text", out var text))
                {
                    yield return new LLMStreamChunk
                    {
                        Content = text.GetString() ?? string.Empty,
                        IsComplete = false
                    };
                }
            }
            else if (type == "message_delta")
            {
                // Extract usage from final message
                if (data.TryGetProperty("usage", out var usage))
                {
                    var outputTokens = usage.GetProperty("output_tokens").GetInt32();
                    yield return new LLMStreamChunk
                    {
                        Content = string.Empty,
                        IsComplete = true,
                        Usage = new LLMUsage { OutputTokens = outputTokens }
                    };
                }
            }
        }
    }

    public Task<EmbeddingResponse> GenerateEmbeddingsAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EmbeddingResponse
        {
            Success = false,
            Error = "Anthropic does not support embeddings. Use OpenAI or local models."
        });
    }

    public async Task<ProviderHealth> GetHealthAsync()
    {
        try
        {
            var testRequest = new LLMRequest
            {
                Model = "claude-3-5-haiku-20241022",
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
            "claude-3-5-sonnet-20241022" => new ProviderPricing
            {
                InputTokenCost = 3.0,
                OutputTokenCost = 15.0
            },
            "claude-3-5-haiku-20241022" => new ProviderPricing
            {
                InputTokenCost = 0.80,
                OutputTokenCost = 4.0
            },
            "claude-3-opus-20240229" => new ProviderPricing
            {
                InputTokenCost = 15.0,
                OutputTokenCost = 75.0
            },
            _ => new ProviderPricing
            {
                InputTokenCost = 3.0,
                OutputTokenCost = 15.0
            }
        };
    }

    private Dictionary<string, object> BuildApiRequest(LLMRequest request)
    {
        var apiRequest = new Dictionary<string, object>
        {
            ["model"] = request.Model,
            ["max_tokens"] = request.MaxTokens,
            ["temperature"] = request.Temperature
        };

        // Convert messages
        var messages = new List<object>();
        foreach (var message in request.Messages)
        {
            if (message.Contents?.Any() == true)
            {
                // Multimodal message
                var content = message.Contents.Select(c => (object)(c.Type switch
                {
                    "text" => new { type = "text", text = c.Text },
                    "image" => new
                    {
                        type = "image",
                        source = new
                        {
                            type = c.Image!.Type,
                            media_type = c.Image.MimeType,
                            data = c.Image.Data
                        }
                    },
                    _ => throw new ArgumentException($"Unknown content type: {c.Type}")
                })).ToList();

                messages.Add(new { role = message.Role, content });
            }
            else
            {
                // Text-only message
                messages.Add(new { role = message.Role, content = message.Content });
            }
        }

        apiRequest["messages"] = messages;

        // System prompt
        if (!string.IsNullOrEmpty(request.SystemPrompt))
        {
            apiRequest["system"] = request.SystemPrompt;
        }

        // Tools
        if (request.Tools?.Any() == true)
        {
            apiRequest["tools"] = request.Tools.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                input_schema = t.Parameters
            }).ToList();
        }

        return apiRequest;
    }
}
