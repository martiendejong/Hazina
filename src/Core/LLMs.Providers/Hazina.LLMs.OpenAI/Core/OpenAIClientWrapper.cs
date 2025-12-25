using System.ClientModel;
using System.Text.Json;
using System.Threading.Channels;

using OpenAI;
using OpenAI.Chat;
using OpenAI.Embeddings;
using OpenAI.Images;
using static System.Net.Mime.MediaTypeNames;
using System.Net.Http;
using System.Net.Http.Headers;

public partial class OpenAIClientWrapper : ILLMClient
{
    public string GetFormatInstruction<ResponseType>() where ResponseType : ChatResponse<ResponseType>, new()
    {
        return $"YOUR OUTPUT WILL ALWAYS BE ONLY A JSON RESPONSE IN THIS FORMAT AND NOTHING ELSE: {ChatResponse<ResponseType>.Signature} EXAMPLE: {JsonSerializer.Serialize(ChatResponse<ResponseType>.Example)}";
    }

    public List<HazinaChatMessage> AddFormattingInstruction<ResponseType>(List<HazinaChatMessage> messages) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var formatInstruction = GetFormatInstruction<ResponseType>();
        messages.Insert(messages.Count - 1, new HazinaChatMessage { Role = HazinaMessageRole.System, Text = formatInstruction });
        return messages;
    }

    public PartialJsonParser Parser { get; set; } = new PartialJsonParser();

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(List<HazinaChatMessage> messages, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel = default) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var response = await GetResponse(AddFormattingInstruction<ResponseType>(messages), HazinaChatResponseFormat.Json, toolsContext, images, cancel);
        return new LLMResponse<ResponseType?>(Parser.Parse<ResponseType>(response.Result), response.TokenUsage);
    }

    public async Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(List<HazinaChatMessage> messages, Action<string> onChunkReceived, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var response = await GetResponseStream(AddFormattingInstruction<ResponseType>(messages), onChunkReceived, HazinaChatResponseFormat.Json, toolsContext, images, cancel);
        return new LLMResponse<ResponseType?>(Parser.Parse<ResponseType>(response.Result), response.TokenUsage);
    }
}

public partial class OpenAIClientWrapper : ILLMClient
{
    public OpenAIConfig Config { get; set; }
    private readonly EmbeddingClient EmbeddingClient;
    private readonly OpenAIClient API;
    private OpenAIStreamHandler StreamHandler { get; set; }
    private readonly HttpClient _http;

    public OpenAIClientWrapper(OpenAIConfig config)
    {
        Config = config;
        EmbeddingClient = new EmbeddingClient(config.EmbeddingModel, config.ApiKey);
        API = new OpenAIClient(config.ApiKey);
        StreamHandler = new OpenAIStreamHandler();
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
    }

    public async Task<Embedding> GenerateEmbedding(string text)
    {
        return await Retry.Run(async () =>
        {
            var response = await EmbeddingClient.GenerateEmbeddingAsync(text);
            var embeddings = response.Value.ToFloats().ToArray().Select(f => (double)f);
            return new Embedding(embeddings);
        });
    }

    public async Task<LLMResponse<string>> GetResponseStream(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived, HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        Log(messages.Last()?.Text);
        var id = Guid.NewGuid().ToString();
        toolsContext?.SendMessage?.Invoke(id, "LLM Request (OpenAI)", string.Join("\n", messages.Select(m => $"{m.Role?.Role}: {m.Text}")));
        var collected = new List<string>();
        var tokenUsage = new TokenUsageInfo();
        string result = await StreamHandler.HandleStream(chunk =>
        {
            collected.Add(chunk);
            onChunkReceived(chunk);
        }, StreamChatResult(messages.OpenAI(), responseFormat.OpenAI(), toolsContext, images, cancel), tokenUsage);
        toolsContext?.SendMessage?.Invoke(id, "LLM Response (OpenAI)", result);
        return new LLMResponse<string>(result, tokenUsage);
    }

    public async Task<LLMResponse<string>> GetResponse(
        List<HazinaChatMessage> messages, HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        Log(messages.Last()?.Text);
        var id = Guid.NewGuid().ToString();
        toolsContext?.SendMessage?.Invoke(id, "LLM Request (OpenAI)", string.Join("\n", messages.Select(m => $"{m.Role?.Role}: {m.Text}")));
        var completion = await GetChatResult(messages.OpenAI(), responseFormat.OpenAI(), toolsContext, images, cancel);
        var text = GetText(completion);
        var tokenUsage = ExtractTokenUsage(completion);
        toolsContext?.SendMessage?.Invoke(id, "LLM Response (OpenAI)", text);
        return new LLMResponse<string>(text, tokenUsage);
    }

    public async Task<LLMResponse<HazinaGeneratedImage>> GetImage(
        string prompt, HazinaChatResponseFormat responseFormat, IToolsContext? toolsContext, List<ImageData>? images, CancellationToken cancel)
    {
        Log(prompt);
        var image = (await GetImageResult(prompt, responseFormat.OpenAI(), toolsContext, images, cancel)).Hazina();
        var tokenUsage = new TokenUsageInfo(0, 0, 0, 0.04m, Config.ImageModel);
        return new LLMResponse<HazinaGeneratedImage>(image, tokenUsage);
    }

    public async Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel)
    {
        // Default to mp3 if not provided
        var contentType = string.IsNullOrWhiteSpace(mimeType) ? "audio/mpeg" : mimeType;

        var url = "https://api.openai.com/v1/audio/speech";
        var requestObj = new
        {
            model = Config.TtsModel ?? "gpt-4o-mini-tts",
            input = text,
            voice = string.IsNullOrWhiteSpace(voice) ? "alloy" : voice,
            format = contentType.Contains("mpeg") || contentType.Contains("mp3") ? "mp3" : (contentType.Contains("wav") ? "wav" : "mp3")
        };

        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(requestObj), System.Text.Encoding.UTF8, "application/json")
        };
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(contentType));

        using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancel);
        resp.EnsureSuccessStatusCode();
        using var stream = await resp.Content.ReadAsStreamAsync(cancel);
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancel)) > 0)
        {
            var chunk = new byte[read];
            Buffer.BlockCopy(buffer, 0, chunk, 0, read);
            onAudioChunk(chunk);
        }
    }

    #region internal

    protected async Task<ChatCompletion> GetChatResult(List<ChatMessage> messages, ChatResponseFormat responseFormat, IToolsContext? context, List<ImageData>? images, CancellationToken cancel)
    {
        var client = API.GetChatClient(Config.Model);
        var imageClient = API.GetImageClient(Config.Model);
        var interaction = new SimpleOpenAIClientChatInteraction(context, API, this, Config.ApiKey, Config.Model, Config.LogPath, client, imageClient, messages, images, responseFormat, true, true);
        return await interaction.Run(cancel);
    }

    protected async Task<GeneratedImage> GetImageResult(string prompt, ChatResponseFormat responseFormat, IToolsContext? context, List<ImageData>? images, CancellationToken cancel)
    {
        var client = API.GetChatClient(Config.Model);
        var imageClient = API.GetImageClient(Config.ImageModel);
        var interaction = new SimpleOpenAIClientChatInteraction(context, API, this, Config.ApiKey, Config.Model, Config.LogPath, client, imageClient, [prompt], images, responseFormat, true, true);
        return await interaction.RunImage(prompt, cancel);
    }

    private IAsyncEnumerable<StreamingChatCompletionUpdate> StreamChatResult(List<ChatMessage> messages, ChatResponseFormat responseFormat, IToolsContext? context, List<ImageData>? images, CancellationToken cancel)
    {
        var client = API.GetChatClient(Config.Model);
        var imageClient = API.GetImageClient(Config.Model);
        var interaction = new SimpleOpenAIClientChatInteraction(context, API, this, Config.ApiKey, Config.Model, Config.LogPath, client, imageClient, messages, images, responseFormat, true, true);
        return interaction.Stream(cancel);
    }

    protected string GetText(ChatCompletion result)
    {
        return result.Content.ToList().First().Text;
    }

    protected TokenUsageInfo ExtractTokenUsage(ChatCompletion result)
    {
        var usage = result.Usage;
        var inputTokens = usage.InputTokenCount;
        var outputTokens = usage.OutputTokenCount;

        decimal inputCost = CalculateCost(Config.Model, inputTokens, true);
        decimal outputCost = CalculateCost(Config.Model, outputTokens, false);

        return new TokenUsageInfo(inputTokens, outputTokens, inputCost, outputCost, Config.Model);
    }

    protected decimal CalculateCost(string model, int tokens, bool isInput)
    {
        decimal pricePerMillion = model.ToLower() switch
        {
            var m when m.Contains("gpt-4o") => isInput ? 2.5m : 10m,
            var m when m.Contains("gpt-4-turbo") => isInput ? 10m : 30m,
            var m when m.Contains("gpt-4") => isInput ? 30m : 60m,
            var m when m.Contains("gpt-3.5-turbo") => isInput ? 0.5m : 1.5m,
            var m when m.Contains("o1-preview") => isInput ? 15m : 60m,
            var m when m.Contains("o1-mini") => isInput ? 3m : 12m,
            var m when m.Contains("o3-mini") => isInput ? 1.1m : 4.4m,
            _ => isInput ? 2.5m : 10m
        };

        return (tokens / 1_000_000m) * pricePerMillion;
    }

    #endregion

    public void Log(string? data)
    {
        const int maxRetries = 10;
        const int delayMs = 100;
        string message = $"{DateTime.Now:yy-MM-dd HH:mm:ss}\n{data ?? ""}";

        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                using (FileStream stream = new FileStream(Config.LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    writer.WriteLine(message);
                    return;
                }
            }
            catch (IOException)
            {
                // File is likely locked by another writer
                Thread.Sleep(delayMs);
            }
        }

        throw new IOException("Could not write to log file after multiple attempts due to it being locked.");
    }
}
