using Hazina.AI.LocalLLM.Configuration;
using Hazina.LLMs;
using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hazina.AI.LocalLLM.Providers;

/// <summary>
/// Local LLM client using LLamaSharp for GGUF model inference.
/// Implements ILLMClient for seamless integration with Hazina.
/// </summary>
public sealed class LocalLLMClient : ILLMClient, IDisposable
{
    private readonly LocalLLMConfig _config;
    private readonly ILogger<LocalLLMClient> _logger;
    private readonly LLamaWeights _model;
    private readonly LLamaContext _context;
    private readonly InteractiveExecutor _executor;
    private readonly LLamaEmbedder? _embedder;
    private bool _disposed;

    /// <summary>
    /// Creates a new LocalLLMClient instance.
    /// </summary>
    /// <param name="config">Configuration for the local model.</param>
    /// <param name="logger">Optional logger.</param>
    public LocalLLMClient(LocalLLMConfig config, ILogger<LocalLLMClient>? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? NullLogger<LocalLLMClient>.Instance;

        if (!File.Exists(config.ModelPath))
            throw new FileNotFoundException($"Model file not found: {config.ModelPath}");

        _logger.LogInformation("Loading model from {ModelPath}", config.ModelPath);

        var modelParams = new ModelParams(config.ModelPath)
        {
            ContextSize = config.ContextSize,
            GpuLayerCount = config.GpuLayerCount,
            MainGpu = config.MainGpuId,
            UseMemorymap = config.UseMemoryMap,
            UseMemoryLock = config.UseMemoryLock
        };

        _model = LLamaWeights.LoadFromFile(modelParams);
        _context = _model.CreateContext(modelParams);
        _executor = new InteractiveExecutor(_context);

        // Create embedder if model supports it
        try
        {
            _embedder = new LLamaEmbedder(_model, modelParams);
        }
        catch
        {
            _logger.LogWarning("Model does not support embeddings");
            _embedder = null;
        }

        _logger.LogInformation("Model loaded successfully. Context size: {ContextSize}", config.ContextSize);
    }

    /// <summary>
    /// Creates a LocalLLMClient asynchronously.
    /// </summary>
    public static async Task<LocalLLMClient> CreateAsync(
        LocalLLMConfig config,
        ILogger<LocalLLMClient>? logger = null,
        CancellationToken ct = default)
    {
        return await Task.Run(() => new LocalLLMClient(config, logger), ct);
    }

    /// <inheritdoc />
    public async Task<Embedding> GenerateEmbedding(string data)
    {
        if (_embedder == null)
            throw new NotSupportedException("This model does not support embeddings");

        var embeddings = await _embedder.GetEmbeddings(data);
        // Convert float[] to IEnumerable<double>
        var firstEmbedding = embeddings.FirstOrDefault() ?? Array.Empty<float>();
        return new Embedding(firstEmbedding.Select(f => (double)f));
    }

    /// <inheritdoc />
    public async Task<LLMResponse<string>> GetResponse(
        List<HazinaChatMessage> messages,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        var prompt = BuildPrompt(messages, responseFormat);
        var inferenceParams = CreateInferenceParams();

        var response = new System.Text.StringBuilder();
        var inputTokens = EstimateTokenCount(prompt);
        var outputTokens = 0;

        await foreach (var token in _executor.InferAsync(prompt, inferenceParams, cancel))
        {
            response.Append(token);
            outputTokens++;

            // Check for stop sequences
            if (ShouldStop(response.ToString()))
                break;
        }

        var result = CleanResponse(response.ToString());

        return new LLMResponse<string>(
            result,
            new TokenUsageInfo(
                inputTokens,
                outputTokens,
                0m, 0m,
                Path.GetFileNameWithoutExtension(_config.ModelPath)));
    }

    /// <inheritdoc />
    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(
        List<HazinaChatMessage> messages,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        // Add JSON format instruction
        var jsonMessages = new List<HazinaChatMessage>(messages);

        var formatInstruction = $"\n\nYou MUST respond with valid JSON matching this schema: {ChatResponse<ResponseType>.Signature}\n" +
            $"Example: {System.Text.Json.JsonSerializer.Serialize(ChatResponse<ResponseType>.Example)}";

        if (jsonMessages.Count > 0 && jsonMessages[^1].Role == HazinaMessageRole.User)
        {
            var lastMsg = jsonMessages[^1];
            jsonMessages[^1] = new HazinaChatMessage(lastMsg.Role, lastMsg.Text + formatInstruction);
        }

        var response = await GetResponse(jsonMessages, HazinaChatResponseFormat.Json, toolsContext, images, cancel);

        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<ResponseType>(response.Result);
            return new LLMResponse<ResponseType?>(parsed, response.TokenUsage);
        }
        catch (System.Text.Json.JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON response: {Response}", response.Result);
            return new LLMResponse<ResponseType?>(default, response.TokenUsage);
        }
    }

    /// <inheritdoc />
    public async Task<LLMResponse<string>> GetResponseStream(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        var prompt = BuildPrompt(messages, responseFormat);
        var inferenceParams = CreateInferenceParams();

        var response = new System.Text.StringBuilder();
        var inputTokens = EstimateTokenCount(prompt);
        var outputTokens = 0;

        await foreach (var token in _executor.InferAsync(prompt, inferenceParams, cancel))
        {
            response.Append(token);
            outputTokens++;
            onChunkReceived(token);

            if (ShouldStop(response.ToString()))
                break;
        }

        var result = CleanResponse(response.ToString());

        return new LLMResponse<string>(
            result,
            new TokenUsageInfo(inputTokens, outputTokens, 0m, 0m,
                Path.GetFileNameWithoutExtension(_config.ModelPath)));
    }

    /// <inheritdoc />
    public async Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var response = await GetResponseStream(messages, onChunkReceived, HazinaChatResponseFormat.Json, toolsContext, images, cancel);

        try
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize<ResponseType>(response.Result);
            return new LLMResponse<ResponseType?>(parsed, response.TokenUsage);
        }
        catch
        {
            return new LLMResponse<ResponseType?>(default, response.TokenUsage);
        }
    }

    /// <inheritdoc />
    public Task<LLMResponse<HazinaGeneratedImage>> GetImage(
        string prompt,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        throw new NotSupportedException("Image generation is not supported by local LLM models. Use a dedicated image generation model.");
    }

    /// <inheritdoc />
    public Task SpeakStream(
        string text,
        string voice,
        Action<byte[]> onAudioChunk,
        string mimeType,
        CancellationToken cancel)
    {
        throw new NotSupportedException("Text-to-speech is not supported by local LLM models. Use a dedicated TTS model.");
    }

    private string BuildPrompt(List<HazinaChatMessage> messages, HazinaChatResponseFormat responseFormat)
    {
        var template = _config.ChatTemplate?.ToLowerInvariant() ?? DetectChatTemplate();

        return template switch
        {
            "llama3" => BuildLlama3Prompt(messages),
            "llama2" => BuildLlama2Prompt(messages),
            "chatml" => BuildChatMLPrompt(messages),
            "phi3" => BuildPhi3Prompt(messages),
            "mistral" => BuildMistralPrompt(messages),
            _ => BuildGenericPrompt(messages)
        };
    }

    private string DetectChatTemplate()
    {
        var modelName = Path.GetFileName(_config.ModelPath).ToLowerInvariant();

        if (modelName.Contains("llama-3") || modelName.Contains("llama3"))
            return "llama3";
        if (modelName.Contains("llama-2") || modelName.Contains("llama2"))
            return "llama2";
        if (modelName.Contains("phi"))
            return "phi3";
        if (modelName.Contains("mistral"))
            return "mistral";
        if (modelName.Contains("deepseek") || modelName.Contains("qwen"))
            return "chatml";

        return "chatml"; // Default
    }

    private static string BuildLlama3Prompt(List<HazinaChatMessage> messages)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<|begin_of_text|>");

        foreach (var msg in messages)
        {
            var role = GetRoleName(msg.Role);

            sb.AppendLine($"<|start_header_id|>{role}<|end_header_id|>");
            sb.AppendLine();
            sb.AppendLine(msg.Text);
            sb.AppendLine("<|eot_id|>");
        }

        sb.AppendLine("<|start_header_id|>assistant<|end_header_id|>");
        sb.AppendLine();

        return sb.ToString();
    }

    private static string GetRoleName(HazinaMessageRole role)
    {
        if (role == HazinaMessageRole.System || role.Role == "system")
            return "system";
        if (role == HazinaMessageRole.Assistant || role.Role == "assistant")
            return "assistant";
        return "user";
    }

    private static string BuildLlama2Prompt(List<HazinaChatMessage> messages)
    {
        var sb = new System.Text.StringBuilder();
        string? systemPrompt = null;

        foreach (var msg in messages)
        {
            if (msg.Role == HazinaMessageRole.System)
            {
                systemPrompt = msg.Text;
                continue;
            }

            if (msg.Role == HazinaMessageRole.User)
            {
                sb.Append("<s>[INST] ");
                if (systemPrompt != null)
                {
                    sb.Append($"<<SYS>>\n{systemPrompt}\n<</SYS>>\n\n");
                    systemPrompt = null;
                }
                sb.Append($"{msg.Text} [/INST]");
            }
            else if (msg.Role == HazinaMessageRole.Assistant)
            {
                sb.Append($" {msg.Text}</s>");
            }
        }

        return sb.ToString();
    }

    private static string BuildChatMLPrompt(List<HazinaChatMessage> messages)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var msg in messages)
        {
            var role = GetRoleName(msg.Role);

            sb.AppendLine($"<|im_start|>{role}");
            sb.AppendLine(msg.Text);
            sb.AppendLine("<|im_end|>");
        }

        sb.AppendLine("<|im_start|>assistant");

        return sb.ToString();
    }

    private static string BuildPhi3Prompt(List<HazinaChatMessage> messages)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var msg in messages)
        {
            var role = GetRoleName(msg.Role);

            sb.AppendLine($"<|{role}|>");
            sb.AppendLine(msg.Text);
            sb.AppendLine($"<|end|>");
        }

        sb.AppendLine("<|assistant|>");

        return sb.ToString();
    }

    private static string BuildMistralPrompt(List<HazinaChatMessage> messages)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var msg in messages)
        {
            if (msg.Role == HazinaMessageRole.User)
            {
                sb.Append($"[INST] {msg.Text} [/INST]");
            }
            else if (msg.Role == HazinaMessageRole.Assistant)
            {
                sb.Append($" {msg.Text}</s>");
            }
            else if (msg.Role == HazinaMessageRole.System)
            {
                sb.Append($"[INST] {msg.Text}\n\n");
            }
        }

        return sb.ToString();
    }

    private static string BuildGenericPrompt(List<HazinaChatMessage> messages)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var msg in messages)
        {
            // Capitalize for generic prompt format
            var roleName = GetRoleName(msg.Role);
            var role = char.ToUpper(roleName[0]) + roleName[1..];

            sb.AppendLine($"{role}: {msg.Text}");
            sb.AppendLine();
        }

        sb.Append("Assistant: ");

        return sb.ToString();
    }

    private InferenceParams CreateInferenceParams()
    {
        return new InferenceParams
        {
            MaxTokens = _config.MaxTokens,
            AntiPrompts = _config.StopSequences.ToList(),
            SamplingPipeline = new DefaultSamplingPipeline
            {
                Temperature = _config.Temperature,
                TopP = _config.TopP,
                TopK = _config.TopK,
                RepeatPenalty = _config.RepetitionPenalty
            }
        };
    }

    private bool ShouldStop(string response)
    {
        foreach (var stop in _config.StopSequences)
        {
            if (response.EndsWith(stop))
                return true;
        }

        // Check for chat template end tokens
        var lowerResponse = response.ToLowerInvariant();
        return lowerResponse.EndsWith("<|eot_id|>") ||
               lowerResponse.EndsWith("<|im_end|>") ||
               lowerResponse.EndsWith("<|end|>") ||
               lowerResponse.EndsWith("</s>");
    }

    private static string CleanResponse(string response)
    {
        // Remove trailing end tokens
        var result = response
            .Replace("<|eot_id|>", "")
            .Replace("<|im_end|>", "")
            .Replace("<|end|>", "")
            .Replace("</s>", "")
            .Trim();

        return result;
    }

    private static int EstimateTokenCount(string text)
    {
        // Rough estimate: ~4 characters per token for English
        return text.Length / 4;
    }

    public void Dispose()
    {
        if (_disposed) return;

        _embedder?.Dispose();
        _context.Dispose();
        _model.Dispose();

        _disposed = true;
    }
}
