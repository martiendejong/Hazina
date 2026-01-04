using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hazina.LLMs;

/// <summary>
/// Ollama implementation of ILLMClient for local LLM inference
/// </summary>
public class OllamaClientWrapper : ILLMClient
{
    public OllamaConfig Config { get; set; }
    private readonly HttpClient _http;
    private readonly PartialJsonParser _parser;

    public OllamaClientWrapper(OllamaConfig config)
    {
        Config = config;
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.Endpoint.TrimEnd('/')),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _parser = new PartialJsonParser();
    }

    #region Chat Completion

    public async Task<LLMResponse<string>> GetResponse(
        List<HazinaChatMessage> messages,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        Log(messages.LastOrDefault()?.Text);

        var request = CreateChatRequest(messages, responseFormat, stream: false);
        var json = JsonSerializer.Serialize(request, GetJsonOptions());

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/chat", content, cancel);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(cancel);
        var ollamaResponse = JsonSerializer.Deserialize<OllamaChatResponse>(responseBody, GetJsonOptions());

        var result = ollamaResponse?.Message?.Content ?? string.Empty;
        var tokenUsage = ExtractTokenUsage(ollamaResponse);

        return new LLMResponse<string>(result, tokenUsage);
    }

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(
        List<HazinaChatMessage> messages,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var messagesWithFormat = AddFormattingInstruction<ResponseType>(messages);
        var response = await GetResponse(messagesWithFormat, HazinaChatResponseFormat.Json, toolsContext, images, cancel);
        var parsed = _parser.Parse<ResponseType>(response.Result);
        return new LLMResponse<ResponseType?>(parsed, response.TokenUsage);
    }

    #endregion

    #region Streaming

    public async Task<LLMResponse<string>> GetResponseStream(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        Log(messages.LastOrDefault()?.Text);

        var request = CreateChatRequest(messages, responseFormat, stream: true);
        var json = JsonSerializer.Serialize(request, GetJsonOptions());

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancel);
        response.EnsureSuccessStatusCode();

        var fullResponse = new StringBuilder();
        var tokenUsage = new TokenUsageInfo { ModelName = Config.Model };

        using var stream = await response.Content.ReadAsStreamAsync(cancel);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream && !cancel.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancel);
            if (string.IsNullOrWhiteSpace(line)) continue;

            try
            {
                var chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, GetJsonOptions());
                if (chunk?.Message?.Content != null)
                {
                    fullResponse.Append(chunk.Message.Content);
                    onChunkReceived(chunk.Message.Content);
                }

                if (chunk?.Done == true)
                {
                    tokenUsage = ExtractTokenUsage(chunk);
                }
            }
            catch (JsonException)
            {
                // Skip malformed lines
            }
        }

        return new LLMResponse<string>(fullResponse.ToString(), tokenUsage);
    }

    public async Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var messagesWithFormat = AddFormattingInstruction<ResponseType>(messages);
        var response = await GetResponseStream(messagesWithFormat, onChunkReceived, HazinaChatResponseFormat.Json, toolsContext, images, cancel);
        var parsed = _parser.Parse<ResponseType>(response.Result);
        return new LLMResponse<ResponseType?>(parsed, response.TokenUsage);
    }

    #endregion

    #region Embeddings

    public async Task<Embedding> GenerateEmbedding(string data)
    {
        var request = new OllamaEmbeddingRequest
        {
            Model = Config.EmbeddingModel,
            Prompt = data
        };

        var json = JsonSerializer.Serialize(request, GetJsonOptions());
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/api/embeddings", content);
        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync();
        var embedResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseBody, GetJsonOptions());

        if (embedResponse?.Embedding == null || embedResponse.Embedding.Length == 0)
        {
            throw new InvalidOperationException("Ollama returned empty embedding");
        }

        var doubleEmbedding = embedResponse.Embedding.Select(f => (double)f);
        return new Embedding(doubleEmbedding);
    }

    #endregion

    #region Image Generation (Not Supported)

    public Task<LLMResponse<HazinaGeneratedImage>> GetImage(
        string prompt,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        throw new NotSupportedException("Ollama does not support image generation. Use OpenAI or another provider for image generation.");
    }

    #endregion

    #region Text-to-Speech (Not Supported)

    public Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel)
    {
        throw new NotSupportedException("Ollama does not support text-to-speech. Use OpenAI or another provider for TTS.");
    }

    #endregion

    #region Helpers

    private OllamaChatRequest CreateChatRequest(
        List<HazinaChatMessage> messages,
        HazinaChatResponseFormat responseFormat,
        bool stream)
    {
        var ollamaMessages = messages.Select(m => new OllamaMessage
        {
            Role = MapRole(m.Role),
            Content = m.Text ?? string.Empty
        }).ToList();

        var request = new OllamaChatRequest
        {
            Model = Config.Model,
            Messages = ollamaMessages,
            Stream = stream,
            Options = new OllamaOptions
            {
                Temperature = (float)Config.Temperature,
                NumPredict = Config.MaxTokens,
                TopP = (float)Config.TopP
            }
        };

        // Request JSON format if specified
        if (responseFormat == HazinaChatResponseFormat.Json)
        {
            request.Format = "json";
        }

        return request;
    }

    private static string MapRole(HazinaMessageRole? role)
    {
        if (role == null) return "user";
        if (role == HazinaMessageRole.System) return "system";
        if (role == HazinaMessageRole.Assistant) return "assistant";
        return "user";
    }

    private List<HazinaChatMessage> AddFormattingInstruction<ResponseType>(List<HazinaChatMessage> messages)
        where ResponseType : ChatResponse<ResponseType>, new()
    {
        var formatInstruction = $"YOUR OUTPUT WILL ALWAYS BE ONLY A JSON RESPONSE IN THIS FORMAT AND NOTHING ELSE: {ChatResponse<ResponseType>.Signature} EXAMPLE: {JsonSerializer.Serialize(ChatResponse<ResponseType>.Example)}";
        var result = new List<HazinaChatMessage>(messages);
        result.Insert(result.Count - 1, new HazinaChatMessage(HazinaMessageRole.System, formatInstruction));
        return result;
    }

    private TokenUsageInfo ExtractTokenUsage(OllamaChatResponse? response)
    {
        if (response == null)
        {
            return new TokenUsageInfo { ModelName = Config.Model };
        }

        var inputTokens = response.PromptEvalCount ?? 0;
        var outputTokens = response.EvalCount ?? 0;

        // Ollama is free (local), so costs are zero
        return new TokenUsageInfo(
            inputTokens: inputTokens,
            outputTokens: outputTokens,
            inputCost: 0m,
            outputCost: 0m,
            modelName: Config.Model
        );
    }

    private static JsonSerializerOptions GetJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Log(string? data)
    {
        if (string.IsNullOrEmpty(Config.LogPath)) return;

        try
        {
            var message = $"{DateTime.Now:yy-MM-dd HH:mm:ss}\n{data ?? ""}";
            var dir = Path.GetDirectoryName(Config.LogPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.AppendAllText(Config.LogPath, message + Environment.NewLine);
        }
        catch
        {
            // Ignore logging errors
        }
    }

    #endregion
}

#region Ollama API Models

internal class OllamaChatRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OllamaMessage> Messages { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }

    [JsonPropertyName("format")]
    public string? Format { get; set; }

    [JsonPropertyName("options")]
    public OllamaOptions? Options { get; set; }
}

internal class OllamaMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "user";

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}

internal class OllamaOptions
{
    [JsonPropertyName("temperature")]
    public float Temperature { get; set; } = 0.7f;

    [JsonPropertyName("num_predict")]
    public int NumPredict { get; set; } = 4096;

    [JsonPropertyName("top_p")]
    public float TopP { get; set; } = 1.0f;
}

internal class OllamaChatResponse
{
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("message")]
    public OllamaMessage? Message { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("prompt_eval_count")]
    public int? PromptEvalCount { get; set; }

    [JsonPropertyName("eval_count")]
    public int? EvalCount { get; set; }

    [JsonPropertyName("total_duration")]
    public long? TotalDuration { get; set; }
}

internal class OllamaEmbeddingRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("prompt")]
    public string Prompt { get; set; } = string.Empty;
}

internal class OllamaEmbeddingResponse
{
    [JsonPropertyName("embedding")]
    public float[]? Embedding { get; set; }
}

#endregion
