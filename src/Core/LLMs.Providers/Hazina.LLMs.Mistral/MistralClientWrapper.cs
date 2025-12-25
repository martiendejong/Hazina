using System.Text;
using System.Text.Json;
using DevGPT.LLMs.Mistral;

public class MistralClientWrapper : ILLMClient
{
    private readonly MistralConfig _config;
    private readonly HttpClient _http;

    public MistralClientWrapper(MistralConfig config)
    {
        _config = config;
        _http = new HttpClient { BaseAddress = new Uri(config.Endpoint) };
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);
    }

    public Task<Embedding> GenerateEmbedding(string data)
        => throw new NotSupportedException("Mistral embeddings are not supported by this client.");

    private sealed record MistralMessage(string role, string content);
    private sealed record MistralRequest(string model, MistralMessage[] messages, bool stream = false);

    private sealed record MistralChatMessage(string role, string content);
    private sealed record MistralChoice(MistralChatMessage message);
    private sealed record MistralUsage(int prompt_tokens, int completion_tokens, int total_tokens);
    private sealed record MistralResponse(MistralChoice[] choices, MistralUsage? usage);

    private static List<MistralMessage> MapMessages(List<DevGPTChatMessage> messages)
    {
        var mapped = new List<MistralMessage>();
        foreach (var m in messages)
        {
            var role = (m.Role == DevGPTMessageRole.System || m.Role.Role == DevGPTMessageRole.System.Role) ? "system"
                    : (m.Role == DevGPTMessageRole.Assistant || m.Role.Role == DevGPTMessageRole.Assistant.Role) ? "assistant"
                    : "REGULAR";
            mapped.Add(new MistralMessage(role, m.Text));
        }
        return mapped;
    }

    private async Task<(string text, TokenUsageInfo usage)> CallMistral(List<DevGPTChatMessage> messages, CancellationToken cancel, IToolsContext? tools = null)
    {
        var mapped = MapMessages(messages);
        var id = Guid.NewGuid().ToString();
        tools?.SendMessage?.Invoke(id, "LLM Request (Mistral)", string.Join("\n", messages.Select(m => $"{m.Role?.Role}: {m.Text}")));

        var req = new MistralRequest(_config.Model, mapped.ToArray(), stream: false);
        var json = JsonSerializer.Serialize(req);
        using var resp = await _http.PostAsync("/chat/completions", new StringContent(json, Encoding.UTF8, "application/json"), cancel);
        var respText = await resp.Content.ReadAsStringAsync(cancel);
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Mistral error: {resp.StatusCode}: {respText}");

        MistralResponse? parsed = null;
        try { parsed = JsonSerializer.Deserialize<MistralResponse>(respText); } catch { }
        var text = parsed?.choices?.FirstOrDefault()?.message?.content ?? respText;
        var usage = ExtractTokenUsage(parsed);
        tools?.SendMessage?.Invoke(id, "LLM Response (Mistral)", text);
        return (text, usage);
    }

    private TokenUsageInfo ExtractTokenUsage(MistralResponse? response)
    {
        if (response?.usage == null) return new TokenUsageInfo(0, 0, 0, 0, _config.Model);
        var input = response.usage.prompt_tokens;
        var output = response.usage.completion_tokens;
        // Cost model omitted; set zero to avoid inaccurate pricing
        return new TokenUsageInfo(input, output, 0, 0, _config.Model);
    }

    public async Task<LLMResponse<string>> GetResponse(List<DevGPTChatMessage> messages, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        var (text, usage) = await CallMistral(messages, cancel, toolsContext);
        return new LLMResponse<string>(text, usage);
    }

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(List<DevGPTChatMessage> messages, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var withFormat = new List<DevGPTChatMessage>(messages);
        var instruction = $"YOUR OUTPUT WILL ALWAYS BE ONLY A JSON RESPONSE IN THIS FORMAT AND NOTHING ELSE: {ChatResponse<ResponseType>.Signature} EXAMPLE: {JsonSerializer.Serialize(ChatResponse<ResponseType>.Example)}";
        var insert = Math.Max(0, withFormat.Count - 1);
        withFormat.Insert(insert, new DevGPTChatMessage { Role = DevGPTMessageRole.System, Text = instruction });

        var (text, usage) = await CallMistral(withFormat, cancel, toolsContext);
        try
        {
            var result = JsonSerializer.Deserialize<ResponseType>(text);
            return new LLMResponse<ResponseType?>(result, usage);
        }
        catch
        {
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var json = text.Substring(start, end - start + 1);
                try
                {
                    var result = JsonSerializer.Deserialize<ResponseType>(json);
                    return new LLMResponse<ResponseType?>(result, usage);
                }
                catch { }
            }
            throw;
        }
    }

    public async Task<LLMResponse<string>> GetResponseStream(List<DevGPTChatMessage> messages, Action<string> onChunkReceived, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        var response = await GetResponse(messages, responseFormat, toolsContext, images, cancel);
        foreach (var chunk in Chunk(response.Result, 60)) onChunkReceived(chunk);
        return response;
    }

    public async Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(List<DevGPTChatMessage> messages, Action<string> onChunkReceived, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var response = await GetResponseStream(messages, onChunkReceived, DevGPTChatResponseFormat.Json, toolsContext, images, cancel);
        var result = JsonSerializer.Deserialize<ResponseType>(response.Result);
        return new LLMResponse<ResponseType?>(result, response.TokenUsage);
    }

    public Task<LLMResponse<DevGPTGeneratedImage>> GetImage(string prompt, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
        => throw new NotSupportedException("Mistral image generation is not supported by this client.");

    public Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel)
        => throw new NotSupportedException("Voice streaming is not supported for Mistral in this client.");

    private static IEnumerable<string> Chunk(string s, int size)
    {
        for (int i = 0; i < s.Length; i += size)
            yield return s.Substring(i, Math.Min(size, s.Length - i));
    }
}
