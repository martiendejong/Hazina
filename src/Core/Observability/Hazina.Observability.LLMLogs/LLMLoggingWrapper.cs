using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hazina.LLMs;
using Hazina.LLMs.Capabilities;
using Microsoft.Extensions.Logging;

namespace Hazina.Observability.LLMLogs;

/// <summary>
/// Wraps an LLM client to add basic logging
/// </summary>
public class LLMLoggingWrapper : ILLMClient
{
    private readonly ILLMClient _inner;
    private readonly ILogger _logger;
    private readonly string _providerName;

    public LLMLoggingWrapper(
        ILLMClient inner,
        ILogger logger,
        string providerName)
    {
        _inner = inner;
        _logger = logger;
        _providerName = providerName;
    }

    // ICapabilityProvider - delegate to inner
    public ProviderCapability SupportedCapabilities => _inner.SupportedCapabilities;
    public bool SupportsCapability(ProviderCapability capability) => _inner.SupportsCapability(capability);
    public IEnumerable<string> GetSupportedCapabilityNames() => _inner.GetSupportedCapabilityNames();
    public void RequireCapabilities(ProviderCapability requiredCapabilities) => _inner.RequireCapabilities(requiredCapabilities);

    public Task<Embedding> GenerateEmbedding(string data)
        => _inner.GenerateEmbedding(data);

    public Task<LLMResponse<HazinaGeneratedImage>> GetImage(
        string prompt,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
        => _inner.GetImage(prompt, responseFormat, toolsContext, images, cancel);

    public async Task<LLMResponse<string>> GetResponse(
        List<HazinaChatMessage> messages,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        var startTime = DateTime.UtcNow;

        _logger.LogInformation(
            "[LLM-REQUEST] Provider: {Provider} | Messages: {Count}",
            _providerName, messages.Count);

        try
        {
            var response = await _inner.GetResponse(messages, responseFormat, toolsContext, images, cancel);
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.LogInformation(
                "[LLM-RESPONSE] Provider: {Provider} | Duration: {DurationMs}ms | Tokens: {TotalTokens}",
                _providerName, duration, response.TokenUsage?.TotalTokens ?? 0);

            return response;
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;
            _logger.LogError(ex,
                "[LLM-ERROR] Provider: {Provider} | Duration: {DurationMs}ms | Error: {ErrorType}",
                _providerName, duration, ex.GetType().Name);
            throw;
        }
    }

    public Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(
        List<HazinaChatMessage> messages,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
        => _inner.GetResponse<ResponseType>(messages, toolsContext, images, cancel);

    public async Task<LLMResponse<string>> GetResponseStream(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        _logger.LogInformation(
            "[LLM-STREAM] Provider: {Provider} | Messages: {Count}",
            _providerName, messages.Count);

        var response = await _inner.GetResponseStream(messages, onChunkReceived, responseFormat, toolsContext, images, cancel);

        _logger.LogInformation(
            "[LLM-STREAM] Provider: {Provider} | Tokens: {TotalTokens}",
            _providerName, response.TokenUsage?.TotalTokens ?? 0);

        return response;
    }

    public Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
        => _inner.GetResponseStream<ResponseType>(messages, onChunkReceived, toolsContext, images, cancel);

    public Task SpeakStream(
        string text,
        string voice,
        Action<byte[]> onAudioChunk,
        string mimeType,
        CancellationToken cancel)
        => _inner.SpeakStream(text, voice, onAudioChunk, mimeType, cancel);
}
