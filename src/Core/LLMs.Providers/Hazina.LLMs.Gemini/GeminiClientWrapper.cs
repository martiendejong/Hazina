using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DevGPT.LLMs.Gemini;

public class GeminiClientWrapper : ILLMClient
{
    private readonly GeminiConfig _config;
    private readonly HttpClient _http;

    public GeminiClientWrapper(GeminiConfig config)
    {
        _config = config;
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.Endpoint)
        };
    }

    // Embeddings not implemented for Gemini in this wrapper
    public Task<Embedding> GenerateEmbedding(string data)
        => throw new NotSupportedException("Gemini embeddings are not supported by this client.");

    private sealed record GeminiPart(string text);
    private sealed record GeminiContent(string role, GeminiPart[] parts);
    private sealed record GeminiRequest(GeminiContent[] contents, object? system_instruction = null);

    private sealed record GeminiUsage(int promptTokenCount, int candidatesTokenCount, int totalTokenCount);

    private sealed record GeminiCandidateContentPart(string? text);
    private sealed record GeminiCandidateContent(GeminiCandidateContentPart[] parts);
    private sealed record GeminiCandidate(GeminiCandidateContent content);
    private sealed record GeminiResponse(GeminiCandidate[] candidates, GeminiUsage? usageMetadata);

    private static (object? system, List<GeminiContent> msgs) MapMessages(List<DevGPTChatMessage> messages)
    {
        var systemParts = messages
            .Where(m => m.Role == DevGPTMessageRole.System || m.Role.Role == DevGPTMessageRole.System.Role)
            .Select(m => m.Text)
            .ToList();
        object? system = null;
        if (systemParts.Count > 0)
        {
            system = new { parts = systemParts.Select(t => new { text = t }).ToArray() };
        }

        var mapped = new List<GeminiContent>();
        foreach (var m in messages)
        {
            if (m.Role == DevGPTMessageRole.System || m.Role.Role == DevGPTMessageRole.System.Role)
                continue;
            var role = (m.Role == DevGPTMessageRole.Assistant || m.Role.Role == DevGPTMessageRole.Assistant.Role) ? "model" : "REGULAR";
            mapped.Add(new GeminiContent(role, [ new GeminiPart(m.Text) ]));
        }
        return (system, mapped);
    }

    private async Task<(string text, TokenUsageInfo usage)> CallGemini(List<DevGPTChatMessage> messages, CancellationToken cancel, IToolsContext? tools = null)
    {
        var (system, mapped) = MapMessages(messages);
        var id = Guid.NewGuid().ToString();
        tools?.SendMessage?.Invoke(id, "LLM Request (Gemini)", string.Join("\n", messages.Select(m => $"{m.Role?.Role}: {m.Text}")));

        var req = new GeminiRequest(mapped.ToArray(), system);
        var json = JsonSerializer.Serialize(req);
        var uri = $"/models/{_config.Model}:generateContent?key={_config.ApiKey}";
        using var resp = await _http.PostAsync(uri, new StringContent(json, Encoding.UTF8, "application/json"), cancel);
        var respText = await resp.Content.ReadAsStringAsync(cancel);
        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"Gemini error: {resp.StatusCode}: {respText}");
        }

        GeminiResponse? parsed = null;
        try { parsed = JsonSerializer.Deserialize<GeminiResponse>(respText); } catch { }

        var text = parsed?.candidates?.FirstOrDefault()?.content?.parts?.FirstOrDefault()?.text ?? respText;
        var usage = ExtractTokenUsage(parsed);
        tools?.SendMessage?.Invoke(id, "LLM Response (Gemini)", text);
        return (text, usage);
    }

    private TokenUsageInfo ExtractTokenUsage(GeminiResponse? response)
    {
        if (response?.usageMetadata == null)
            return new TokenUsageInfo(0, 0, 0, 0, _config.Model);

        var input = response.usageMetadata.promptTokenCount;
        var output = response.usageMetadata.candidatesTokenCount;

        decimal inputCost = CalculateCost(_config.Model, input, true);
        decimal outputCost = CalculateCost(_config.Model, output, false);
        return new TokenUsageInfo(input, output, inputCost, outputCost, _config.Model);
    }

    private decimal CalculateCost(string model, int tokens, bool isInput)
    {
        var m = model.ToLower();
        decimal pricePerMillion = m.Contains("1.5-pro") ? (isInput ? 3m : 15m)
                                 : m.Contains("1.5-flash") ? (isInput ? 0.35m : 0.53m)
                                 : (isInput ? 1m : 2m);
        return (tokens / 1_000_000m) * pricePerMillion;
    }

    public async Task<LLMResponse<string>> GetResponse(List<DevGPTChatMessage> messages, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        var (text, usage) = await CallGemini(messages, cancel, toolsContext);
        return new LLMResponse<string>(text, usage);
    }

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(List<DevGPTChatMessage> messages, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        // Add JSON format instruction similar to other clients
        var withFormat = new List<DevGPTChatMessage>(messages);
        var instruction = $"YOUR OUTPUT WILL ALWAYS BE ONLY A JSON RESPONSE IN THIS FORMAT AND NOTHING ELSE: {ChatResponse<ResponseType>.Signature} EXAMPLE: {JsonSerializer.Serialize(ChatResponse<ResponseType>.Example)}";
        var insert = Math.Max(0, withFormat.Count - 1);
        withFormat.Insert(insert, new DevGPTChatMessage { Role = DevGPTMessageRole.System, Text = instruction });

        var (text, usage) = await CallGemini(withFormat, cancel, toolsContext);
        try
        {
            var result = JsonSerializer.Deserialize<ResponseType>(text);
            return new LLMResponse<ResponseType?>(result, usage);
        }
        catch
        {
            // Try to trim to first JSON
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

    public async Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel)
    {
        // Use Google Cloud Text-to-Speech REST API
        var ttsApiKey = _config.TtsApiKey ?? _config.ApiKey;
        if (string.IsNullOrWhiteSpace(ttsApiKey))
            throw new InvalidOperationException("Gemini TTS requires an API key in Gemini:TtsApiKey or Gemini:ApiKey.");

        string encoding = MapMimeToGoogleEncoding(mimeType, _config.TtsAudioEncoding);
        var payload = new
        {
            input = new { text },
            voice = new { languageCode = _config.TtsLanguageCode, name = string.IsNullOrWhiteSpace(voice) ? _config.TtsVoiceName : voice },
            audioConfig = new { audioEncoding = encoding }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, $"https://texttospeech.googleapis.com/v1/text:synthesize?key={ttsApiKey}")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        using var resp = await _http.SendAsync(req, cancel);
        var respText = await resp.Content.ReadAsStringAsync(cancel);
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Google TTS error: {resp.StatusCode}: {respText}");

        using var doc = JsonDocument.Parse(respText);
        if (!doc.RootElement.TryGetProperty("audioContent", out var audioProp))
            throw new Exception("Google TTS response missing audioContent.");
        var b64 = audioProp.GetString();
        var bytes = Convert.FromBase64String(b64);

        int offset = 0;
        const int chunk = 8192;
        while (offset < bytes.Length)
        {
            var size = Math.Min(chunk, bytes.Length - offset);
            var slice = new byte[size];
            Buffer.BlockCopy(bytes, offset, slice, 0, size);
            onAudioChunk(slice);
            offset += size;
        }
    }

    private static string MapMimeToGoogleEncoding(string mime, string fallback)
    {
        var m = (mime ?? "").ToLowerInvariant();
        if (m.Contains("wav") || m.Contains("linear16")) return "LINEAR16";
        if (m.Contains("ogg") || m.Contains("opus")) return "OGG_OPUS";
        if (m.Contains("mp3") || m.Contains("mpeg")) return "MP3";
        return string.IsNullOrWhiteSpace(fallback) ? "MP3" : fallback;
    }

    public async Task<LLMResponse<DevGPTGeneratedImage>> GetImage(string prompt, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        // Google Generative Language Image Generation (v1beta)
        // Default endpoint/model:
        //   POST {Endpoint}/models/{ImageModel}:generate?key=API_KEY
        //   Body: { prompt: { text: "..." }, numberOfImages: 1 }
        var id = Guid.NewGuid().ToString();
        toolsContext?.SendMessage?.Invoke(id, "IMAGE Request (Gemini/Google)", prompt);

        var url = $"{_config.Endpoint}/models/{_config.ImageModel}:generate?key={_config.ApiKey}";
        var payload = new
        {
            prompt = new { text = prompt },
            numberOfImages = 1
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        using var resp = await _http.SendAsync(req, cancel);
        var respText = await resp.Content.ReadAsStringAsync(cancel);
        if (!resp.IsSuccessStatusCode)
            throw new Exception($"Gemini image error: {resp.StatusCode}: {respText}");

        try
        {
            using var doc = JsonDocument.Parse(respText);
            // Response schema varies; common shapes:
            // - { images: [{ data: "base64..." }] }
            // - { generatedImages: [{ image: { data: "base64..." } }] }
            string? b64 = null;
            if (doc.RootElement.TryGetProperty("images", out var imagesArr) && imagesArr.ValueKind == JsonValueKind.Array && imagesArr.GetArrayLength() > 0)
            {
                var imgObj = imagesArr[0];
                if (imgObj.TryGetProperty("data", out var dataProp))
                    b64 = dataProp.GetString();
            }
            else if (doc.RootElement.TryGetProperty("generatedImages", out var genArr) && genArr.ValueKind == JsonValueKind.Array && genArr.GetArrayLength() > 0)
            {
                var first = genArr[0];
                if (first.TryGetProperty("image", out var imageObj) && imageObj.TryGetProperty("data", out var dataProp2))
                    b64 = dataProp2.GetString();
            }

            if (string.IsNullOrEmpty(b64))
            {
                // Try a generic path
                var found = doc.RootElement.GetRawText();
                toolsContext?.SendMessage?.Invoke(id, "IMAGE Response (Gemini/Google)", found);
                throw new Exception("Could not find image data in response");
            }

            var bytes = Convert.FromBase64String(b64);
            var img = new DevGPTGeneratedImage(null, BinaryData.FromBytes(bytes));
            var tokenUsage = new TokenUsageInfo(0, 0, 0, 0, _config.ImageModel);
            toolsContext?.SendMessage?.Invoke(id, "IMAGE Response (Gemini/Google)", $"Generated {bytes.Length} bytes");
            return new LLMResponse<DevGPTGeneratedImage>(img, tokenUsage);
        }
        catch
        {
            // Fallback: try treat response as raw base64
            try
            {
                var bytes = Convert.FromBase64String(respText);
                var img = new DevGPTGeneratedImage(null, BinaryData.FromBytes(bytes));
                var tokenUsage = new TokenUsageInfo(0, 0, 0, 0, _config.ImageModel);
                return new LLMResponse<DevGPTGeneratedImage>(img, tokenUsage);
            }
            catch { throw; }
        }
    }

    

    private static IEnumerable<string> Chunk(string s, int size)
    {
        for (int i = 0; i < s.Length; i += size)
            yield return s.Substring(i, Math.Min(size, s.Length - i));
    }
}

