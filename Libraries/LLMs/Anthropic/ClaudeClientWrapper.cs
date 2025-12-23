using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DevGPT.LLMs.Anthropic;

public class ClaudeClientWrapper : ILLMClient
{
    private readonly AnthropicConfig _config;
    private readonly HttpClient _http;

    public ClaudeClientWrapper(AnthropicConfig config)
    {
        _config = config;
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.Endpoint)
        };
        _http.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", _config.ApiVersion);
    }

    // Anthropic does not currently expose embeddings compatible with this interface in this codebase
    public Task<Embedding> GenerateEmbedding(string data)
        => throw new NotSupportedException("Anthropic Claude embeddings are not supported by this client.");

    // Anthropic Messages API basic request body
    private sealed record AnthropicMessageRequest(string model, int max_tokens, object[] messages, string? system = null);

    private sealed record AnthropicContentBlock(string type, string text);

    private sealed record AnthropicMessage(string role, AnthropicContentBlock[] content);

    private sealed record AnthropicUsage(int input_tokens, int output_tokens);

    private sealed record AnthropicMessageResponse(AnthropicContentBlock[] content, AnthropicUsage usage);

    private static string BuildJsonFormatInstruction<ResponseType>() where ResponseType : ChatResponse<ResponseType>, new()
    {
        // Align with OpenAI wrapper formatting instruction for parity
        return $"YOUR OUTPUT WILL ALWAYS BE ONLY A JSON RESPONSE IN THIS FORMAT AND NOTHING ELSE: {ChatResponse<ResponseType>.Signature} EXAMPLE: {JsonSerializer.Serialize(ChatResponse<ResponseType>.Example)}";
    }

    private static (string? system, List<AnthropicMessage> msgs) MapMessages(List<DevGPTChatMessage> messages)
    {
        // Gather system as a single string (join if multiple)
        var systemParts = messages
            .Where(m => m.Role == DevGPTMessageRole.System || m.Role.Role == DevGPTMessageRole.System.Role)
            .Select(m => m.Text)
            .ToList();
        string? system = systemParts.Count > 0 ? string.Join("\n\n", systemParts) : null;

        var mapped = new List<AnthropicMessage>();
        foreach (var m in messages)
        {
            if (m.Role == DevGPTMessageRole.System || m.Role.Role == DevGPTMessageRole.System.Role)
                continue; // handled via system field

            var role = (m.Role == DevGPTMessageRole.Assistant || m.Role.Role == DevGPTMessageRole.Assistant.Role) ? "assistant" : "REGULAR";
            mapped.Add(new AnthropicMessage(
                role,
                [ new AnthropicContentBlock("text", m.Text) ]
            ));
        }
        return (system, mapped);
    }

    private async Task<(string text, TokenUsageInfo tokenUsage)> CallClaude(List<DevGPTChatMessage> messages, CancellationToken cancel, IToolsContext? toolsContext = null)
    {
        var (system, mapped) = MapMessages(messages);
        var id = Guid.NewGuid().ToString();
        toolsContext?.SendMessage?.Invoke(id, "LLM Request (Claude)", string.Join("\n", messages.Select(m => $"{m.Role?.Role}: {m.Text}")));
        var req = new AnthropicMessageRequest(
            model: _config.Model,
            max_tokens: 1024,
            messages: mapped.Cast<object>().ToArray(),
            system: system
        );

        var json = JsonSerializer.Serialize(req, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("/v1/messages", content, cancel);
        var respText = await resp.Content.ReadAsStringAsync(cancel);
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"Claude error: {resp.StatusCode}: {respText}");
        }
        try
        {
            var parsed = JsonSerializer.Deserialize<AnthropicMessageResponse>(respText);
            var firstText = parsed?.content?.FirstOrDefault(c => c.type == "text")?.text ?? string.Empty;
            var tokenUsage = ExtractTokenUsage(parsed);
            toolsContext?.SendMessage?.Invoke(id, "LLM Response (Claude)", firstText);
            return (firstText, tokenUsage);
        }
        catch
        {
            // Fallback to raw
            toolsContext?.SendMessage?.Invoke(id, "LLM Response (Claude)", respText);
            return (respText, new TokenUsageInfo(0, 0, 0, 0, _config.Model));
        }
    }

    private TokenUsageInfo ExtractTokenUsage(AnthropicMessageResponse? response)
    {
        if (response?.usage == null)
            return new TokenUsageInfo(0, 0, 0, 0, _config.Model);

        var inputTokens = response.usage.input_tokens;
        var outputTokens = response.usage.output_tokens;

        decimal inputCost = CalculateClaudeCost(_config.Model, inputTokens, true);
        decimal outputCost = CalculateClaudeCost(_config.Model, outputTokens, false);

        return new TokenUsageInfo(inputTokens, outputTokens, inputCost, outputCost, _config.Model);
    }

    private decimal CalculateClaudeCost(string model, int tokens, bool isInput)
    {
        decimal pricePerMillion = model.ToLower() switch
        {
            var m when m.Contains("claude-3-5-sonnet") => isInput ? 3m : 15m,
            var m when m.Contains("claude-3-opus") => isInput ? 15m : 75m,
            var m when m.Contains("claude-3-sonnet") => isInput ? 3m : 15m,
            var m when m.Contains("claude-3-haiku") => isInput ? 0.25m : 1.25m,
            _ => isInput ? 3m : 15m
        };

        return (tokens / 1_000_000m) * pricePerMillion;
    }

    public async Task<LLMResponse<string>> GetResponse(List<DevGPTChatMessage> messages, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        // images/tooling ignored in this basic implementation
        var (text, tokenUsage) = await CallClaude(messages, cancel, toolsContext);
        return new LLMResponse<string>(text, tokenUsage);
    }

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(List<DevGPTChatMessage> messages, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        // Inject formatting instruction as a System message before the final user prompt for better adherence
        var withFormat = new List<DevGPTChatMessage>(messages);
        var instruction = BuildJsonFormatInstruction<ResponseType>();
        // Insert near the end but before last message if possible
        var insertIndex = Math.Max(0, withFormat.Count - 1);
        withFormat.Insert(insertIndex, new DevGPTChatMessage { Role = DevGPTMessageRole.System, Text = instruction });

        var (text, tokenUsage) = await CallClaude(withFormat, cancel, toolsContext);
        try
        {
            var result = JsonSerializer.Deserialize<ResponseType>(text);
            return new LLMResponse<ResponseType?>(result, tokenUsage);
        }
        catch
        {
            // Try to salvage by trimming to first JSON object if present
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var json = text.Substring(start, end - start + 1);
                try
                {
                    var result = JsonSerializer.Deserialize<ResponseType>(json);
                    return new LLMResponse<ResponseType?>(result, tokenUsage);
                }
                catch { }
            }
            throw;
        }
    }

    public async Task<LLMResponse<string>> GetResponseStream(List<DevGPTChatMessage> messages, Action<string> onChunkReceived, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        // Simple chunking as a placeholder; Anthropic SSE can be added later
        var response = await GetResponse(messages, responseFormat, toolsContext, images, cancel);
        foreach (var chunk in Chunk(response.Result, 60))
            onChunkReceived(chunk);
        return response;
    }

    public async Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(List<DevGPTChatMessage> messages, Action<string> onChunkReceived, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var response = await GetResponseStream(messages, onChunkReceived, DevGPTChatResponseFormat.Json, toolsContext, images, cancel);
        var result = JsonSerializer.Deserialize<ResponseType>(response.Result);
        return new LLMResponse<ResponseType?>(result, response.TokenUsage);
    }

    public Task<LLMResponse<DevGPTGeneratedImage>> GetImage(string prompt, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        => throw new NotSupportedException("Claude does not generate images in this client.");

    public Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel)
        => throw new NotSupportedException("Voice streaming is not supported for Anthropic in this client.");

    private static IEnumerable<string> Chunk(string s, int size)
    {
        for (int i = 0; i < s.Length; i += size)
            yield return s.Substring(i, Math.Min(size, s.Length - i));
    }
}
