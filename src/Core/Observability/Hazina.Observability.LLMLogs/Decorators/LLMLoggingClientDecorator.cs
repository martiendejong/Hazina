using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hazina.Observability.LLMLogs.Configuration;
using Hazina.Observability.LLMLogs.Context;
using Hazina.Observability.LLMLogs.Storage;
using Hazina.Observability.LLMLogs.Storage.Models;
using Microsoft.Extensions.Options;

namespace Hazina.Observability.LLMLogs.Decorators
{
    /// <summary>
    /// Decorator for ILLMClient that logs all LLM calls to a repository.
    /// Provider-agnostic and works with any ILLMClient implementation.
    /// </summary>
    public class LLMLoggingClientDecorator : ILLMClient
    {
        private readonly ILLMClient _innerClient;
        private readonly ILLMLogRepository _repository;
        private readonly LLMLoggingOptions _options;
        private readonly string _providerName;

        public LLMLoggingClientDecorator(
            ILLMClient innerClient,
            ILLMLogRepository repository,
            IOptions<LLMLoggingOptions> options,
            string providerName = "Unknown")
        {
            _innerClient = innerClient ?? throw new ArgumentNullException(nameof(innerClient));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _options = options.Value;
            _providerName = providerName;
        }

        public async Task<Embedding> GenerateEmbedding(string data)
        {
            // Embeddings don't have token usage in the same way, just pass through
            return await _innerClient.GenerateEmbedding(data);
        }

        public async Task<LLMResponse<HazinaGeneratedImage>> GetImage(
            string prompt,
            HazinaChatResponseFormat responseFormat,
            IToolsContext? toolsContext,
            List<ImageData>? images,
            CancellationToken cancel)
        {
            if (!_options.Enabled)
                return await _innerClient.GetImage(prompt, responseFormat, toolsContext, images, cancel);

            var callId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var context = LLMLoggingContext.Current;

            try
            {
                var result = await _innerClient.GetImage(prompt, responseFormat, toolsContext, images, cancel);
                stopwatch.Stop();

                await LogCallAsync(
                    callId,
                    context,
                    new List<HazinaChatMessage> { new HazinaChatMessage(HazinaMessageRole.User, prompt) },
                    result.Result?.ToString() ?? string.Empty,
                    result.TokenUsage,
                    stopwatch.ElapsedMilliseconds,
                    toolsContext,
                    success: true,
                    errorMessage: null);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogCallAsync(
                    callId,
                    context,
                    new List<HazinaChatMessage> { new HazinaChatMessage(HazinaMessageRole.User, prompt) },
                    string.Empty,
                    new TokenUsageInfo(),
                    stopwatch.ElapsedMilliseconds,
                    toolsContext,
                    success: false,
                    errorMessage: ex.Message);
                throw;
            }
        }

        public async Task<LLMResponse<string>> GetResponse(
            List<HazinaChatMessage> messages,
            HazinaChatResponseFormat responseFormat,
            IToolsContext? toolsContext,
            List<ImageData>? images,
            CancellationToken cancel)
        {
            if (!_options.Enabled)
                return await _innerClient.GetResponse(messages, responseFormat, toolsContext, images, cancel);

            var callId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var context = LLMLoggingContext.Current;

            try
            {
                var result = await _innerClient.GetResponse(messages, responseFormat, toolsContext, images, cancel);
                stopwatch.Stop();

                await LogCallAsync(
                    callId,
                    context,
                    messages,
                    result.Result,
                    result.TokenUsage,
                    stopwatch.ElapsedMilliseconds,
                    toolsContext,
                    success: true,
                    errorMessage: null);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogCallAsync(
                    callId,
                    context,
                    messages,
                    string.Empty,
                    new TokenUsageInfo(),
                    stopwatch.ElapsedMilliseconds,
                    toolsContext,
                    success: false,
                    errorMessage: ex.Message);
                throw;
            }
        }

        public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(
            List<HazinaChatMessage> messages,
            IToolsContext? toolsContext,
            List<ImageData>? images,
            CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
        {
            if (!_options.Enabled)
                return await _innerClient.GetResponse<ResponseType>(messages, toolsContext, images, cancel);

            var callId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var context = LLMLoggingContext.Current;

            try
            {
                var result = await _innerClient.GetResponse<ResponseType>(messages, toolsContext, images, cancel);
                stopwatch.Stop();

                await LogCallAsync(
                    callId,
                    context,
                    messages,
                    JsonSerializer.Serialize(result.Result),
                    result.TokenUsage,
                    stopwatch.ElapsedMilliseconds,
                    toolsContext,
                    success: true,
                    errorMessage: null);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogCallAsync(
                    callId,
                    context,
                    messages,
                    string.Empty,
                    new TokenUsageInfo(),
                    stopwatch.ElapsedMilliseconds,
                    toolsContext,
                    success: false,
                    errorMessage: ex.Message);
                throw;
            }
        }

        public async Task<LLMResponse<string>> GetResponseStream(
            List<HazinaChatMessage> messages,
            Action<string> onChunkReceived,
            HazinaChatResponseFormat responseFormat,
            IToolsContext? toolsContext,
            List<ImageData>? images,
            CancellationToken cancel)
        {
            if (!_options.Enabled)
                return await _innerClient.GetResponseStream(messages, onChunkReceived, responseFormat, toolsContext, images, cancel);

            var callId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var context = LLMLoggingContext.Current;

            try
            {
                var result = await _innerClient.GetResponseStream(messages, onChunkReceived, responseFormat, toolsContext, images, cancel);
                stopwatch.Stop();

                await LogCallAsync(
                    callId,
                    context,
                    messages,
                    result.Result,
                    result.TokenUsage,
                    stopwatch.ElapsedMilliseconds,
                    toolsContext,
                    success: true,
                    errorMessage: null);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogCallAsync(
                    callId,
                    context,
                    messages,
                    string.Empty,
                    new TokenUsageInfo(),
                    stopwatch.ElapsedMilliseconds,
                    toolsContext,
                    success: false,
                    errorMessage: ex.Message);
                throw;
            }
        }

        public async Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(
            List<HazinaChatMessage> messages,
            Action<string> onChunkReceived,
            IToolsContext? toolsContext,
            List<ImageData>? images,
            CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
        {
            if (!_options.Enabled)
                return await _innerClient.GetResponseStream<ResponseType>(messages, onChunkReceived, toolsContext, images, cancel);

            var callId = Guid.NewGuid().ToString();
            var stopwatch = Stopwatch.StartNew();
            var context = LLMLoggingContext.Current;

            try
            {
                var result = await _innerClient.GetResponseStream<ResponseType>(messages, onChunkReceived, toolsContext, images, cancel);
                stopwatch.Stop();

                await LogCallAsync(
                    callId,
                    context,
                    messages,
                    JsonSerializer.Serialize(result.Result),
                    result.TokenUsage,
                    stopwatch.ElapsedMilliseconds,
                    toolsContext,
                    success: true,
                    errorMessage: null);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await LogCallAsync(
                    callId,
                    context,
                    messages,
                    string.Empty,
                    new TokenUsageInfo(),
                    stopwatch.ElapsedMilliseconds,
                    toolsContext,
                    success: false,
                    errorMessage: ex.Message);
                throw;
            }
        }

        public async Task SpeakStream(
            string text,
            string voice,
            Action<byte[]> onAudioChunk,
            string mimeType,
            CancellationToken cancel)
        {
            // Voice synthesis doesn't have traditional token usage, just pass through
            await _innerClient.SpeakStream(text, voice, onAudioChunk, mimeType, cancel);
        }

        private async Task LogCallAsync(
            string callId,
            LLMLoggingContext? context,
            List<HazinaChatMessage> messages,
            string responseData,
            TokenUsageInfo tokenUsage,
            long executionTimeMs,
            IToolsContext? toolsContext,
            bool success,
            string? errorMessage)
        {
            try
            {
                var embeddedDocuments = context?.EmbeddedDocuments;
                var embeddedDocumentsJson = embeddedDocuments != null && embeddedDocuments.Count > 0
                    ? JsonSerializer.Serialize(embeddedDocuments)
                    : null;

                var log = new LLMCallLog
                {
                    CallId = callId,
                    ParentCallId = context?.ParentCallId,
                    Username = context?.Username ?? "Unknown",
                    Feature = context?.Feature ?? "Unknown",
                    Step = context?.Step,
                    DateTimeUtc = DateTime.UtcNow,
                    Provider = _providerName,
                    Model = tokenUsage.ModelName ?? "Unknown",
                    IsToolCall = toolsContext?.Tools?.Count > 0,
                    ToolName = toolsContext?.Tools?.Count > 0 ? string.Join(", ", toolsContext.Tools.Select(t => t.FunctionName)) : null,
                    ToolArguments = null, // Could extract from toolsContext if needed
                    RequestMessages = _options.LogRequestMessages ? JsonSerializer.Serialize(messages) : string.Empty,
                    ResponseData = _options.LogResponseData ? responseData : string.Empty,
                    MessageCount = messages?.Count ?? 0,
                    EmbeddedDocuments = embeddedDocumentsJson,
                    EmbeddedDocumentCount = embeddedDocuments?.Count ?? 0,
                    InputTokens = tokenUsage.InputTokens,
                    OutputTokens = tokenUsage.OutputTokens,
                    TotalTokens = tokenUsage.TotalTokens,
                    InputCost = _options.EstimateCosts ? tokenUsage.InputCost : 0,
                    OutputCost = _options.EstimateCosts ? tokenUsage.OutputCost : 0,
                    TotalCost = _options.EstimateCosts ? tokenUsage.TotalCost : 0,
                    ExecutionTimeMs = executionTimeMs,
                    Success = success,
                    ErrorMessage = errorMessage
                };

                await _repository.LogCallAsync(log);
            }
            catch (Exception ex)
            {
                // Don't fail the LLM call if logging fails
                Console.WriteLine($"[LLMLogging] Failed to log LLM call: {ex.Message}");
            }
        }
    }
}
