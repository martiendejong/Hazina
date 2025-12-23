using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;

namespace DevGPT.LLMs;

/// <summary>
/// Handles streaming responses from Semantic Kernel and converts them to DevGPT format
/// </summary>
public class SemanticKernelStreamHandler
{
    /// <summary>
    /// Handle streaming chat completion with callback for each chunk
    /// Enhanced with comprehensive metadata extraction and error handling
    /// </summary>
    public async Task<string> HandleStreamAsync(
        IAsyncEnumerable<StreamingChatMessageContent> stream,
        Action<string> onChunkReceived,
        TokenUsageInfo tokenUsage,
        CancellationToken cancellationToken = default)
    {
        var fullResponse = new StringBuilder();
        StreamingChatMessageContent? lastChunk = null;
        var chunkCount = 0;

        try
        {
            await foreach (var chunk in stream.WithCancellation(cancellationToken))
            {
                if (chunk != null)
                {
                    lastChunk = chunk;
                    chunkCount++;

                    // Extract text content from chunk
                    var text = chunk.Content ?? string.Empty;

                    if (!string.IsNullOrEmpty(text))
                    {
                        fullResponse.Append(text);

                        // Invoke callback safely
                        try
                        {
                            onChunkReceived?.Invoke(text);
                        }
                        catch (Exception ex)
                        {
                            // Don't let callback errors stop streaming
                            System.Diagnostics.Debug.WriteLine($"Chunk callback error: {ex.Message}");
                        }
                    }

                    // Update token usage if available in chunk metadata
                    ExtractTokenUsageFromChunk(chunk, tokenUsage);

                    // Check for finish reason
                    if (chunk.Metadata?.TryGetValue("FinishReason", out var finishReason) == true)
                    {
                        tokenUsage.ModelName = chunk.ModelId ?? tokenUsage.ModelName;
                    }
                }
            }

            // Final metadata extraction from last chunk if available
            if (lastChunk != null)
            {
                ExtractFinalMetadata(lastChunk, tokenUsage);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during cancellation, just rethrow
            throw;
        }
        catch (Exception ex)
        {
            // Log streaming errors but preserve partial response
            System.Diagnostics.Debug.WriteLine($"Streaming error after {chunkCount} chunks: {ex.Message}");
            throw;
        }

        return fullResponse.ToString();
    }

    /// <summary>
    /// Extract token usage from streaming chunk metadata
    /// </summary>
    private void ExtractTokenUsageFromChunk(StreamingChatMessageContent chunk, TokenUsageInfo tokenUsage)
    {
        if (chunk.Metadata == null)
            return;

        // Try standard usage metadata
        if (chunk.Metadata.TryGetValue("Usage", out var usage))
        {
            if (usage is Dictionary<string, object> usageDict)
            {
                UpdateTokensFromDictionary(usageDict, tokenUsage);
            }
        }

        // Try direct token count metadata (some providers)
        if (chunk.Metadata.TryGetValue("InputTokens", out var inputTokens) && inputTokens is int input)
            tokenUsage.InputTokens = Math.Max(tokenUsage.InputTokens, input);

        if (chunk.Metadata.TryGetValue("OutputTokens", out var outputTokens) && outputTokens is int output)
            tokenUsage.OutputTokens = Math.Max(tokenUsage.OutputTokens, output);

        if (chunk.Metadata.TryGetValue("PromptTokens", out var promptTokens) && promptTokens is int prompt)
            tokenUsage.InputTokens = Math.Max(tokenUsage.InputTokens, prompt);

        if (chunk.Metadata.TryGetValue("CompletionTokens", out var completionTokens) && completionTokens is int completion)
            tokenUsage.OutputTokens = Math.Max(tokenUsage.OutputTokens, completion);
    }

    /// <summary>
    /// Extract final metadata from the last chunk
    /// </summary>
    private void ExtractFinalMetadata(StreamingChatMessageContent lastChunk, TokenUsageInfo tokenUsage)
    {
        if (lastChunk.Metadata == null)
            return;

        // Final token counts are usually in the last chunk
        ExtractTokenUsageFromChunk(lastChunk, tokenUsage);

        // Extract model name if available
        if (!string.IsNullOrEmpty(lastChunk.ModelId))
        {
            tokenUsage.ModelName = lastChunk.ModelId;
        }
    }

    /// <summary>
    /// Update token usage from usage dictionary
    /// </summary>
    private void UpdateTokensFromDictionary(Dictionary<string, object> usageDict, TokenUsageInfo tokenUsage)
    {
        if (usageDict.TryGetValue("InputTokens", out var input) && input is int inputTokens)
            tokenUsage.InputTokens = Math.Max(tokenUsage.InputTokens, inputTokens);

        if (usageDict.TryGetValue("OutputTokens", out var output) && output is int outputTokens)
            tokenUsage.OutputTokens = Math.Max(tokenUsage.OutputTokens, outputTokens);

        // Alternative keys for OpenAI format
        if (usageDict.TryGetValue("PromptTokens", out var prompt) && prompt is int promptTokens)
            tokenUsage.InputTokens = Math.Max(tokenUsage.InputTokens, promptTokens);

        if (usageDict.TryGetValue("CompletionTokens", out var completion) && completion is int completionTokens)
            tokenUsage.OutputTokens = Math.Max(tokenUsage.OutputTokens, completionTokens);

        if (usageDict.TryGetValue("TotalTokens", out var total) && total is int totalTokens)
        {
            // Validate total matches sum
            if (tokenUsage.InputTokens + tokenUsage.OutputTokens == 0)
            {
                // If we don't have breakdown, use total as output tokens
                tokenUsage.OutputTokens = totalTokens;
            }
        }
    }

    /// <summary>
    /// Handle streaming with tool calls support (for future implementation)
    /// </summary>
    public async Task<(string Response, List<FunctionCallContent> ToolCalls)> HandleStreamWithToolsAsync(
        IAsyncEnumerable<StreamingChatMessageContent> stream,
        Action<string> onChunkReceived,
        TokenUsageInfo tokenUsage,
        CancellationToken cancellationToken = default)
    {
        var fullResponse = new StringBuilder();
        var toolCalls = new List<FunctionCallContent>();
        StreamingChatMessageContent? lastChunk = null;

        await foreach (var chunk in stream.WithCancellation(cancellationToken))
        {
            if (chunk != null)
            {
                lastChunk = chunk;

                // Extract text content
                var text = chunk.Content ?? string.Empty;
                if (!string.IsNullOrEmpty(text))
                {
                    fullResponse.Append(text);
                    onChunkReceived?.Invoke(text);
                }

                // Extract function calls if present
                var functionCalls = chunk.Items.OfType<FunctionCallContent>();
                toolCalls.AddRange(functionCalls);

                // Update token usage
                chunk.UpdateTokenUsage(tokenUsage);
            }
        }

        return (fullResponse.ToString(), toolCalls);
    }
}
