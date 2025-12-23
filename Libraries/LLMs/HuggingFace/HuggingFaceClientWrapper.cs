using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DevGPT;
using DevGPT.LLMs.HuggingFace;




public class HuggingFaceClientWrapper : ILLMClient
{
    private readonly HuggingFaceConfig _config;
    private static readonly HttpClient _httpClient = new HttpClient();

    // Default model names
    private const string DefaultChatModel = "meta-llama/Llama-2-70b-chat-hf"; // or "mistralai/Mixtral-8x7B-Instruct-v0.1"
    private const string DefaultEmbeddingModel = "sentence-transformers/all-mpnet-base-v2"; // or "BAAI/bge-large-en-v1.5"
    private const string DefaultImageModel = "stabilityai/stable-diffusion-xl-base-1.0";

    public HuggingFaceClientWrapper(HuggingFaceConfig config)
    {
        _config = config;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.ApiKey);
    }

    public async Task<Embedding> GenerateEmbedding(string data)
    {
        var endpoint = $"{_config.Endpoint}/pipeline/feature-extraction/{DefaultEmbeddingModel}";
        var reqObj = new { inputs = data };
        var reqContent = new StringContent(JsonSerializer.Serialize(reqObj), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, reqContent);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HuggingFace Embedding error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");

        var content = await response.Content.ReadAsStringAsync();
        try {
            var vector = JsonSerializer.Deserialize<double[][]>(content)?[0];
            return new Embedding(vector);
        } catch {
            throw new Exception($"Could not parse embedding output: {content}");
        }
    }

    public async Task<LLMResponse<DevGPTGeneratedImage>> GetImage(string prompt, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        var endpoint = $"{_config.Endpoint}/pipeline/text-to-image/{DefaultImageModel}";
        var payload = new { inputs = prompt };
        var reqContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, reqContent, cancel);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HuggingFace Image error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");

        var content = await response.Content.ReadAsStringAsync();
        // Image can come as base64 string or direct image, depend on endpoint config
        // Try to deserialize as { generated_image: <base64str> }
        using var doc = JsonDocument.Parse(content);
        if(doc.RootElement.TryGetProperty("generated_image", out var imgProp) || doc.RootElement.TryGetProperty("image", out imgProp)) {
            var base64 = imgProp.GetString();
            var bytes = Convert.FromBase64String(base64);
            // Return only bytes, no URL for HuggingFace
            var image = new DevGPTGeneratedImage(null, BinaryData.FromBytes(bytes));
            var tokenUsage = new TokenUsageInfo(0, 0, 0, 0.05m, DefaultImageModel);
            return new LLMResponse<DevGPTGeneratedImage>(image, tokenUsage);
        }
        throw new Exception($"Could not parse image response: {content}");
    }

    public async Task<LLMResponse<string>> GetResponse(List<DevGPTChatMessage> messages, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        var endpoint = $"{_config.Endpoint}/pipeline/text-generation/{DefaultChatModel}";
        var id = Guid.NewGuid().ToString();
        toolsContext?.SendMessage?.Invoke(id, "LLM Request (HuggingFace)", string.Join("\n", messages.ConvertAll(m => $"{m.Role?.Role}: {m.Text}")));
        var prompt = string.Join("\n", messages.ConvertAll(m => $"[{m.Role?.Role}] {m.Text}"));
        var payload = new { inputs = prompt };
        var reqContent = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, reqContent, cancel);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HuggingFace Chat error: {response.StatusCode}, {await response.Content.ReadAsStringAsync()}");

        var content = await response.Content.ReadAsStringAsync();
        var tokenUsage = new TokenUsageInfo(0, 0, 0, 0, DefaultChatModel);

        // Most models return [{"generated_text": "..."}] or similar
        try {
            using var doc = JsonDocument.Parse(content);
            if(doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var elem = doc.RootElement[0];
                if (elem.TryGetProperty("generated_text", out var text))
                {
                    var t = text.GetString();
                    toolsContext?.SendMessage?.Invoke(id, "LLM Response (HuggingFace)", t);
                    return new LLMResponse<string>(t, tokenUsage);
                }
                if(elem.TryGetProperty("output", out text))
                {
                    var t = text.GetString();
                    toolsContext?.SendMessage?.Invoke(id, "LLM Response (HuggingFace)", t);
                    return new LLMResponse<string>(t, tokenUsage);
                }
                var raw = elem.ToString();
                toolsContext?.SendMessage?.Invoke(id, "LLM Response (HuggingFace)", raw);
                return new LLMResponse<string>(raw, tokenUsage);
            }
            toolsContext?.SendMessage?.Invoke(id, "LLM Response (HuggingFace)", content);
            return new LLMResponse<string>(content, tokenUsage);
        } catch {
            toolsContext?.SendMessage?.Invoke(id, "LLM Response (HuggingFace)", content);
            return new LLMResponse<string>(content, tokenUsage); // Fallback as plain text
        }
    }

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(List<DevGPTChatMessage> messages, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        // Compose JSON format request as needed (like OpenAI implementation)
        var response = await GetResponse(messages, DevGPTChatResponseFormat.Json, toolsContext, images, cancel);
        var result = System.Text.Json.JsonSerializer.Deserialize<ResponseType>(response.Result);
        return new LLMResponse<ResponseType?>(result, response.TokenUsage);
    }

    public async Task<LLMResponse<string>> GetResponseStream(List<DevGPTChatMessage> messages, Action<string> onChunkReceived, DevGPTChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        // HuggingFace Inference API streaming support is only on paid tiers, so here chunk the output.
        var response = await GetResponse(messages, responseFormat, toolsContext, images, cancel);
        foreach (var chunk in ChunkString(response.Result, 40))
            onChunkReceived(chunk);
        return response;
    }

    public async Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(List<DevGPTChatMessage> messages, Action<string> onChunkReceived, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var response = await GetResponseStream(messages, onChunkReceived, DevGPTChatResponseFormat.Json, toolsContext, images, cancel);
        var result = System.Text.Json.JsonSerializer.Deserialize<ResponseType>(response.Result);
        return new LLMResponse<ResponseType?>(result, response.TokenUsage);
    }

    public Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel)
        => throw new NotSupportedException("Voice streaming is not supported for HuggingFace in this client.");

    // Util
    private static IEnumerable<string> ChunkString(string str, int chunkSize)
    {
        for(int i=0; i<str.Length; i+=chunkSize)
            yield return str.Substring(i, Math.Min(chunkSize, str.Length-i));
    }
}
