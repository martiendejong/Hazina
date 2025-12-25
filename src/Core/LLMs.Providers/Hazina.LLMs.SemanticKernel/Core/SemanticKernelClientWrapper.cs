#pragma warning disable SKEXP0001 // Suppress experimental API warnings for embeddings and image generation

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.TextToImage;
using System.Text;

namespace DevGPT.LLMs;

/// <summary>
/// Semantic Kernel implementation of ILLMClient for DevGPT
/// Provides multi-provider LLM support while maintaining DevGPT interfaces
/// </summary>
public class SemanticKernelClientWrapper : ILLMClient
{
    public SemanticKernelConfig Config { get; set; }
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ITextEmbeddingGenerationService? _embeddingService;
    private readonly ITextToImageService? _imageService;
    private readonly SemanticKernelStreamHandler _streamHandler;

    public SemanticKernelClientWrapper(SemanticKernelConfig config)
    {
        Config = config;
        _streamHandler = new SemanticKernelStreamHandler();

        // Build kernel based on provider
        var builder = Kernel.CreateBuilder();

        switch (config.Provider)
        {
            case LLMProvider.OpenAI:
                builder.AddOpenAIChatCompletion(
                    modelId: config.Model,
                    apiKey: config.ApiKey);

                if (!string.IsNullOrEmpty(config.EmbeddingModel))
                {
                    builder.AddOpenAITextEmbeddingGeneration(
                        modelId: config.EmbeddingModel,
                        apiKey: config.ApiKey);
                }

                if (!string.IsNullOrEmpty(config.ImageModel))
                {
                    builder.AddOpenAITextToImage(
                        apiKey: config.ApiKey,
                        modelId: config.ImageModel);
                }
                break;

            case LLMProvider.AzureOpenAI:
                if (string.IsNullOrEmpty(config.Endpoint) || string.IsNullOrEmpty(config.DeploymentName))
                    throw new ArgumentException("Azure OpenAI requires Endpoint and DeploymentName in config");

                builder.AddAzureOpenAIChatCompletion(
                    deploymentName: config.DeploymentName,
                    endpoint: config.Endpoint,
                    apiKey: config.ApiKey);

                if (!string.IsNullOrEmpty(config.EmbeddingModel))
                {
                    builder.AddAzureOpenAITextEmbeddingGeneration(
                        deploymentName: config.EmbeddingModel,
                        endpoint: config.Endpoint,
                        apiKey: config.ApiKey);
                }
                break;

            case LLMProvider.Ollama:
                // Ollama support (requires Microsoft.SemanticKernel.Connectors.Ollama package)
                throw new NotImplementedException("Ollama provider support coming soon - requires additional package");

            case LLMProvider.Anthropic:
                // Anthropic support (requires custom connector or community package)
                throw new NotImplementedException("Anthropic provider support coming soon");

            case LLMProvider.Custom:
                throw new NotImplementedException("Custom provider requires manual kernel configuration");

            default:
                throw new ArgumentException($"Unknown provider: {config.Provider}");
        }

        _kernel = builder.Build();

        // Get required services
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
        _embeddingService = _kernel.Services.GetService(typeof(ITextEmbeddingGenerationService)) as ITextEmbeddingGenerationService;
        _imageService = _kernel.Services.GetService(typeof(ITextToImageService)) as ITextToImageService;
    }

    #region Chat Completion

    public async Task<LLMResponse<string>> GetResponse(
        List<DevGPTChatMessage> messages,
        DevGPTChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        var chatHistory = messages.ToSemanticKernelChatHistory();
        var executionSettings = CreateExecutionSettings(responseFormat, toolsContext);

        var tokenUsage = new TokenUsageInfo { ModelName = Config.Model };

        // Register tools if present
        RegisterToolsInKernel(toolsContext, messages, cancel);

        try
        {
            var result = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancel);

            var response = result.Content ?? string.Empty;

            // Extract token usage from metadata
            var usage = ExtractTokenUsageFromMetadata(result.Metadata);
            tokenUsage.InputTokens = usage.InputTokens;
            tokenUsage.OutputTokens = usage.OutputTokens;

            // Calculate costs (preserve existing logic from OpenAI wrapper)
            CalculateCosts(tokenUsage);

            return new LLMResponse<string>(response, tokenUsage);
        }
        catch (Exception ex)
        {
            LogError("GetResponse", ex);
            throw;
        }
    }

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(
        List<DevGPTChatMessage> messages,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
        where ResponseType : ChatResponse<ResponseType>, new()
    {
        // Try native SK structured output first (if supported)
        if (SupportsNativeStructuredOutput())
        {
            try
            {
                return await GetResponseWithNativeStructuredOutput<ResponseType>(messages, toolsContext, images, cancel);
            }
            catch (NotSupportedException)
            {
                // Fall back to schema injection approach if allowed
                if (!Config.FallbackToSchemaInjection)
                    throw;
            }
            catch (Exception)
            {
                // Fall back to schema injection approach if allowed
                if (!Config.FallbackToSchemaInjection)
                    throw;
            }
        }

        // Fall back to schema injection approach (always works)
        return await GetResponseWithSchemaInjection<ResponseType>(messages, toolsContext, images, cancel);
    }

    /// <summary>
    /// Get typed response using native SK structured output (OpenAI, Azure only)
    /// </summary>
    private async Task<LLMResponse<ResponseType?>> GetResponseWithNativeStructuredOutput<ResponseType>(
        List<DevGPTChatMessage> messages,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
        where ResponseType : ChatResponse<ResponseType>, new()
    {
        if (Config.Provider != LLMProvider.OpenAI && Config.Provider != LLMProvider.AzureOpenAI)
            throw new NotSupportedException("Native structured output only supported for OpenAI and Azure OpenAI");

        var chatHistory = messages.ToSemanticKernelChatHistory();
        var tokenUsage = new TokenUsageInfo { ModelName = Config.Model };

        // Register tools if present
        RegisterToolsInKernel(toolsContext, messages, cancel);

        // Create execution settings with response format
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = Config.Temperature,
            MaxTokens = Config.MaxTokens,
            TopP = Config.TopP,
            FrequencyPenalty = Config.FrequencyPenalty,
            PresencePenalty = Config.PresencePenalty,
            ToolCallBehavior = toolsContext?.Tools?.Any() == true
                ? ToolCallBehavior.AutoInvokeKernelFunctions
                : null,
            ResponseFormat = typeof(ResponseType) // Native structured output
        };

        try
        {
            // SK will automatically serialize/deserialize based on ResponseFormat type
            var result = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                settings,
                _kernel,
                cancel);

            var response = result.Content ?? string.Empty;

            // Extract token usage
            var usage = ExtractTokenUsageFromMetadata(result.Metadata);
            tokenUsage.InputTokens = usage.InputTokens;
            tokenUsage.OutputTokens = usage.OutputTokens;
            CalculateCosts(tokenUsage);

            // Parse the JSON response
            var parser = new PartialJsonParser();
            var parsed = parser.Parse<ResponseType>(response);

            return new LLMResponse<ResponseType?>(parsed, tokenUsage);
        }
        catch (Exception ex)
        {
            LogError("GetResponseWithNativeStructuredOutput", ex);
            throw;
        }
    }

    /// <summary>
    /// Get typed response using schema injection (works with all providers)
    /// </summary>
    private async Task<LLMResponse<ResponseType?>> GetResponseWithSchemaInjection<ResponseType>(
        List<DevGPTChatMessage> messages,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
        where ResponseType : ChatResponse<ResponseType>, new()
    {
        // Add JSON schema instruction to messages
        var messagesWithSchema = AddFormattingInstruction<ResponseType>(messages);

        // Get response with JSON format
        var response = await GetResponse(
            messagesWithSchema,
            DevGPTChatResponseFormat.Json,
            toolsContext,
            images,
            cancel);

        // Parse JSON response
        var parser = new PartialJsonParser();
        var parsed = parser.Parse<ResponseType>(response.Result);

        return new LLMResponse<ResponseType?>(parsed, response.TokenUsage);
    }

    /// <summary>
    /// Check if provider supports native structured output
    /// </summary>
    private bool SupportsNativeStructuredOutput()
    {
        // Check config setting
        if (!Config.UseNativeStructuredOutput)
            return false;

        // Only OpenAI and Azure OpenAI support ResponseFormat = typeof(T)
        return Config.Provider == LLMProvider.OpenAI || Config.Provider == LLMProvider.AzureOpenAI;
    }

    #endregion

    #region Streaming

    public async Task<LLMResponse<string>> GetResponseStream(
        List<DevGPTChatMessage> messages,
        Action<string> onChunkReceived,
        DevGPTChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        var chatHistory = messages.ToSemanticKernelChatHistory();
        var executionSettings = CreateExecutionSettings(responseFormat, toolsContext);

        var tokenUsage = new TokenUsageInfo { ModelName = Config.Model };

        // Register tools if present
        RegisterToolsInKernel(toolsContext, messages, cancel);

        try
        {
            var stream = _chatService.GetStreamingChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancel);

            var response = await _streamHandler.HandleStreamAsync(
                stream,
                onChunkReceived,
                tokenUsage,
                cancel);

            // Calculate costs
            CalculateCosts(tokenUsage);

            return new LLMResponse<string>(response, tokenUsage);
        }
        catch (Exception ex)
        {
            LogError("GetResponseStream", ex);
            throw;
        }
    }

    public async Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(
        List<DevGPTChatMessage> messages,
        Action<string> onChunkReceived,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
        where ResponseType : ChatResponse<ResponseType>, new()
    {
        // For streaming, we always use schema injection approach
        // Native structured output doesn't stream the JSON incrementally
        return await GetResponseStreamWithSchemaInjection<ResponseType>(
            messages,
            onChunkReceived,
            toolsContext,
            images,
            cancel);
    }

    /// <summary>
    /// Get typed streaming response with schema injection and partial JSON parsing
    /// </summary>
    private async Task<LLMResponse<ResponseType?>> GetResponseStreamWithSchemaInjection<ResponseType>(
        List<DevGPTChatMessage> messages,
        Action<string> onChunkReceived,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
        where ResponseType : ChatResponse<ResponseType>, new()
    {
        // Add JSON schema instruction
        var messagesWithSchema = AddFormattingInstruction<ResponseType>(messages);

        var chatHistory = messagesWithSchema.ToSemanticKernelChatHistory();
        var executionSettings = CreateExecutionSettings(DevGPTChatResponseFormat.Json, toolsContext);

        var tokenUsage = new TokenUsageInfo { ModelName = Config.Model };

        // Register tools if present
        RegisterToolsInKernel(toolsContext, messagesWithSchema, cancel);

        // Partial JSON parser for incremental parsing
        var parser = new PartialJsonParser();
        var fullResponse = new System.Text.StringBuilder();

        // Wrapper callback for partial parsing attempts
        Action<string> wrappedCallback = chunk =>
        {
            fullResponse.Append(chunk);

            // Try partial parsing and invoke original callback
            try
            {
                onChunkReceived?.Invoke(chunk);

                // Optionally attempt partial parse (may fail until JSON is complete)
                // This is useful for progress tracking but not required
            }
            catch
            {
                // Ignore partial parse failures - normal during streaming
            }
        };

        try
        {
            var stream = _chatService.GetStreamingChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                _kernel,
                cancel);

            await _streamHandler.HandleStreamAsync(
                stream,
                wrappedCallback,
                tokenUsage,
                cancel);

            // Calculate costs
            CalculateCosts(tokenUsage);

            // Parse complete JSON response
            var parsed = parser.Parse<ResponseType>(fullResponse.ToString());

            return new LLMResponse<ResponseType?>(parsed, tokenUsage);
        }
        catch (Exception ex)
        {
            LogError("GetResponseStreamWithSchemaInjection", ex);
            throw;
        }
    }

    #endregion

    #region Embeddings

    public async Task<Embedding> GenerateEmbedding(string data)
    {
        if (_embeddingService == null)
            throw new InvalidOperationException("Embedding service not configured. Check SemanticKernelConfig.EmbeddingModel");

        try
        {
            var result = await _embeddingService.GenerateEmbeddingAsync(data);
            // SK returns ReadOnlyMemory<float>, convert to double[] for Embedding
            var floatArray = result.ToArray();
            var doubleArray = Array.ConvertAll(floatArray, x => (double)x);
            return new Embedding(doubleArray);
        }
        catch (Exception ex)
        {
            LogError("GenerateEmbedding", ex);
            throw;
        }
    }

    #endregion

    #region Image Generation

    public async Task<LLMResponse<DevGPTGeneratedImage>> GetImage(
        string prompt,
        DevGPTChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        if (_imageService == null)
            throw new InvalidOperationException("Image service not configured. Check SemanticKernelConfig.ImageModel");

        var tokenUsage = new TokenUsageInfo { ModelName = Config.ImageModel };

        try
        {
            var result = await _imageService.GenerateImageAsync(prompt, 1024, 1024, _kernel, cancel);

            // SK returns a URL string, convert to Uri
            Uri? imageUri = null;
            if (!string.IsNullOrEmpty(result))
            {
                Uri.TryCreate(result, UriKind.Absolute, out imageUri);
            }

            var generatedImage = new DevGPTGeneratedImage(imageUri, null);

            return new LLMResponse<DevGPTGeneratedImage>(generatedImage, tokenUsage);
        }
        catch (Exception ex)
        {
            LogError("GetImage", ex);
            throw;
        }
    }

    #endregion

    #region Text-to-Speech

    public Task SpeakStream(string text, string voice, Action<byte[]> onAudioChunk, string mimeType, CancellationToken cancel)
    {
        // Text-to-speech not yet supported in Semantic Kernel standard connectors
        // Will need custom implementation or wait for SK to add TTS support
        throw new NotImplementedException("Text-to-speech streaming not yet supported via Semantic Kernel");
    }

    #endregion

    #region Helper Methods

    private PromptExecutionSettings CreateExecutionSettings(
        DevGPTChatResponseFormat responseFormat,
        IToolsContext? toolsContext)
    {
        PromptExecutionSettings settings;

        if (Config.Provider == LLMProvider.OpenAI || Config.Provider == LLMProvider.AzureOpenAI)
        {
            var openAISettings = new OpenAIPromptExecutionSettings
            {
                Temperature = Config.Temperature,
                MaxTokens = Config.MaxTokens,
                TopP = Config.TopP,
                FrequencyPenalty = Config.FrequencyPenalty,
                PresencePenalty = Config.PresencePenalty,
                ToolCallBehavior = toolsContext?.Tools?.Any() == true
                    ? ToolCallBehavior.AutoInvokeKernelFunctions
                    : null
            };

            // Apply response format
            if (responseFormat == DevGPTChatResponseFormat.Json)
            {
                openAISettings.ResponseFormat = "json_object";
            }

            settings = openAISettings;
        }
        else
        {
            settings = new PromptExecutionSettings
            {
                ExtensionData = new Dictionary<string, object>
                {
                    ["temperature"] = Config.Temperature,
                    ["max_tokens"] = Config.MaxTokens
                }
            };
        }

        return settings;
    }

    /// <summary>
    /// Register tools from IToolsContext into the kernel as plugins
    /// </summary>
    private void RegisterToolsInKernel(IToolsContext? toolsContext, List<DevGPTChatMessage> messages, CancellationToken cancel)
    {
        if (toolsContext == null || !toolsContext.Tools.Any())
            return;

        var adapter = new Plugins.ToolsContextPluginAdapter(toolsContext);
        adapter.RegisterToolsAsPlugins(_kernel, messages, cancel);
    }

    private List<DevGPTChatMessage> AddFormattingInstruction<ResponseType>(List<DevGPTChatMessage> messages)
        where ResponseType : ChatResponse<ResponseType>, new()
    {
        var signature = ChatResponse<ResponseType>.Signature;
        var example = ChatResponse<ResponseType>.Example;

        var formattingMessage = new DevGPTChatMessage(
            DevGPTMessageRole.System,
            $"IMPORTANT: Respond ONLY with valid JSON matching this exact schema:\n{signature}\n\nExample:\n{Newtonsoft.Json.JsonConvert.SerializeObject(example, Newtonsoft.Json.Formatting.Indented)}"
        );

        var result = new List<DevGPTChatMessage> { formattingMessage };
        result.AddRange(messages);
        return result;
    }

    private TokenUsageInfo ExtractTokenUsageFromMetadata(IReadOnlyDictionary<string, object?>? metadata)
    {
        var tokenUsage = new TokenUsageInfo { ModelName = Config.Model };

        if (metadata == null)
            return tokenUsage;

        if (metadata.TryGetValue("Usage", out var usage) && usage is Dictionary<string, object> usageDict)
        {
            if (usageDict.TryGetValue("InputTokens", out var input) && input is int inputTokens)
                tokenUsage.InputTokens = inputTokens;
            if (usageDict.TryGetValue("OutputTokens", out var output) && output is int outputTokens)
                tokenUsage.OutputTokens = outputTokens;

            // Alternative OpenAI format
            if (usageDict.TryGetValue("PromptTokens", out var prompt) && prompt is int promptTokens)
                tokenUsage.InputTokens = promptTokens;
            if (usageDict.TryGetValue("CompletionTokens", out var completion) && completion is int completionTokens)
                tokenUsage.OutputTokens = completionTokens;
        }

        return tokenUsage;
    }

    private void CalculateCosts(TokenUsageInfo tokenUsage)
    {
        // Cost calculation logic (preserve from OpenAI wrapper)
        // This should be moved to a shared utility in future refactoring
        var inputCostPer1M = 0.0m;
        var outputCostPer1M = 0.0m;

        // Model-specific pricing (simplified - should be in config)
        if (Config.Model.Contains("gpt-4o"))
        {
            inputCostPer1M = 2.50m;
            outputCostPer1M = 10.00m;
        }
        else if (Config.Model.Contains("gpt-4"))
        {
            inputCostPer1M = 30.00m;
            outputCostPer1M = 60.00m;
        }
        else if (Config.Model.Contains("gpt-3.5"))
        {
            inputCostPer1M = 0.50m;
            outputCostPer1M = 1.50m;
        }

        tokenUsage.InputCost = (tokenUsage.InputTokens / 1_000_000m) * inputCostPer1M;
        tokenUsage.OutputCost = (tokenUsage.OutputTokens / 1_000_000m) * outputCostPer1M;
    }

    private void LogError(string method, Exception ex)
    {
        if (!string.IsNullOrEmpty(Config.LogPath))
        {
            try
            {
                var logMessage = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] ERROR in {method}: {ex.Message}\n{ex.StackTrace}\n";
                File.AppendAllText(Config.LogPath, logMessage);
            }
            catch
            {
                // Ignore logging errors
            }
        }
    }

    #endregion
}
