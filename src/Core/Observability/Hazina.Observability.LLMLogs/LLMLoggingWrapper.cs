using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hazina.LLMs;
using Hazina.Observability.Core.Correlation;
using Hazina.Observability.Core.Logging;
using Hazina.Observability.Core.Tracing;
using Microsoft.Extensions.Logging;

namespace Hazina.Observability.LLMLogs;

/// <summary>
/// Wraps an LLM client to add comprehensive logging, tracing, and correlation
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

    public async Task<LLMResponse?> GetResponse(
        string prompt,
        CancellationToken? cancel = null,
        List<HazinaChatMessage>? history = null,
        bool addRelevantDocuments = false,
        bool addFilesList = false,
        string? toolsContext = null,
        HazinaImage[]? images = null)
    {
        var correlationId = CorrelationContext.GetOrCreateCorrelationId();
        var startTime = DateTime.UtcNow;

        using var activity = HazinaActivitySource.StartLLMOperation(
            "GetResponse",
            _providerName);

        _logger.LogInformation(
            "[LLM-REQUEST] Starting request | Provider: {Provider} | PromptLength: {PromptLength} | HistoryCount: {HistoryCount} | AddDocs: {AddDocs} | AddFiles: {AddFiles} | CorrelationId: {CorrelationId}",
            _providerName,
            prompt.Length,
            history?.Count ?? 0,
            addRelevantDocuments,
            addFilesList,
            correlationId);

        // Redact sensitive data from prompt for logging
        var redactedPrompt = SensitiveDataRedactor.RedactForLogging(prompt, maxLength: 200);
        _logger.LogDebug(
            "[LLM-REQUEST] Prompt preview: {PromptPreview} | CorrelationId: {CorrelationId}",
            redactedPrompt,
            correlationId);

        try
        {
            var response = await _inner.GetResponse(
                prompt,
                cancel,
                history,
                addRelevantDocuments,
                addFilesList,
                toolsContext,
                images);

            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            if (response != null)
            {
                // Record metrics
                HazinaActivitySource.RecordCost(
                    activity,
                    0, // Cost tracking would be added separately
                    response.TokenUsage?.InputTokens ?? 0,
                    response.TokenUsage?.OutputTokens ?? 0);

                var redactedResponse = SensitiveDataRedactor.RedactForLogging(response.Result, maxLength: 200);

                _logger.LogInformation(
                    "[LLM-RESPONSE] Request completed | Provider: {Provider} | Duration: {DurationMs}ms | InputTokens: {InputTokens} | OutputTokens: {OutputTokens} | TotalTokens: {TotalTokens} | CorrelationId: {CorrelationId}",
                    _providerName,
                    duration,
                    response.TokenUsage?.InputTokens ?? 0,
                    response.TokenUsage?.OutputTokens ?? 0,
                    response.TokenUsage?.TotalTokens ?? 0,
                    correlationId);

                _logger.LogDebug(
                    "[LLM-RESPONSE] Response preview: {ResponsePreview} | CorrelationId: {CorrelationId}",
                    redactedResponse,
                    correlationId);
            }
            else
            {
                _logger.LogWarning(
                    "[LLM-RESPONSE] Null response | Provider: {Provider} | Duration: {DurationMs}ms | CorrelationId: {CorrelationId}",
                    _providerName,
                    duration,
                    correlationId);
            }

            return response;
        }
        catch (Exception ex)
        {
            var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

            HazinaActivitySource.RecordError(activity, ex);

            _logger.LogError(
                ex,
                "[LLM-ERROR] Request failed | Provider: {Provider} | Duration: {DurationMs}ms | Error: {ErrorType} | Message: {ErrorMessage} | CorrelationId: {CorrelationId}",
                _providerName,
                duration,
                ex.GetType().Name,
                ex.Message,
                correlationId);

            throw;
        }
    }

    public Task<LLMResponse?> GetResponse(
        List<HazinaChatMessage> messages,
        CancellationToken? cancel = null,
        bool addRelevantDocuments = false,
        bool addFilesList = false,
        string? toolsContext = null,
        HazinaImage[]? images = null)
    {
        var correlationId = CorrelationContext.GetOrCreateCorrelationId();

        _logger.LogInformation(
            "[LLM-REQUEST] Starting chat request | Provider: {Provider} | MessageCount: {MessageCount} | CorrelationId: {CorrelationId}",
            _providerName,
            messages.Count,
            correlationId);

        return GetResponse(
            messages.LastOrDefault()?.Content ?? string.Empty,
            cancel,
            messages.Take(messages.Count - 1).ToList(),
            addRelevantDocuments,
            addFilesList,
            toolsContext,
            images);
    }
}
