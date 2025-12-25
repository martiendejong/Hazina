using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Hazina.LLMs;

/// <summary>
/// Extension methods for converting between Hazina types and Semantic Kernel types
/// </summary>
public static class HazinaSemanticKernelExtensions
{
    #region HazinaChatMessage <-> ChatHistory Conversion

    /// <summary>
    /// Convert HazinaChatMessage to Semantic Kernel ChatMessageContent
    /// </summary>
    public static ChatMessageContent ToSemanticKernel(this HazinaChatMessage message)
    {
        var role = message.Role.ToSemanticKernel();

        return new ChatMessageContent(
            role: role,
            content: message.Text,
            modelId: null,
            metadata: new Dictionary<string, object?>
            {
                ["MessageId"] = message.MessageId,
                ["AgentName"] = message.AgentName,
                ["FunctionName"] = message.FunctionName,
                ["FlowName"] = message.FlowName,
                ["Response"] = message.Response
            }
        );
    }

    /// <summary>
    /// Convert Semantic Kernel ChatMessageContent to HazinaChatMessage
    /// </summary>
    public static HazinaChatMessage ToHazina(this ChatMessageContent message)
    {
        var hazinaMessage = new HazinaChatMessage
        {
            Role = message.Role.ToHazina(),
            Text = message.Content ?? string.Empty
        };

        // Restore metadata if available
        if (message.Metadata != null)
        {
            if (message.Metadata.TryGetValue("MessageId", out var messageId) && messageId is Guid guid)
                hazinaMessage.MessageId = guid;
            if (message.Metadata.TryGetValue("AgentName", out var agentName) && agentName is string agent)
                hazinaMessage.AgentName = agent;
            if (message.Metadata.TryGetValue("FunctionName", out var functionName) && functionName is string func)
                hazinaMessage.FunctionName = func;
            if (message.Metadata.TryGetValue("FlowName", out var flowName) && flowName is string flow)
                hazinaMessage.FlowName = flow;
            if (message.Metadata.TryGetValue("Response", out var response) && response is string resp)
                hazinaMessage.Response = resp;
        }

        return hazinaMessage;
    }

    /// <summary>
    /// Convert list of HazinaChatMessage to Semantic Kernel ChatHistory
    /// </summary>
    public static ChatHistory ToSemanticKernelChatHistory(this List<HazinaChatMessage> messages)
    {
        var chatHistory = new ChatHistory();

        foreach (var message in messages)
        {
            chatHistory.Add(message.ToSemanticKernel());
        }

        return chatHistory;
    }

    /// <summary>
    /// Convert Semantic Kernel ChatHistory to list of HazinaChatMessage
    /// </summary>
    public static List<HazinaChatMessage> ToHazina(this ChatHistory chatHistory)
    {
        return chatHistory.Select(m => m.ToHazina()).ToList();
    }

    #endregion

    #region Role Conversion

    /// <summary>
    /// Convert HazinaMessageRole to Semantic Kernel AuthorRole
    /// </summary>
    public static AuthorRole ToSemanticKernel(this HazinaMessageRole role)
    {
        if (role.Role == HazinaMessageRole.User.Role)
            return AuthorRole.User;
        if (role.Role == HazinaMessageRole.Assistant.Role)
            return AuthorRole.Assistant;
        if (role.Role == HazinaMessageRole.System.Role)
            return AuthorRole.System;

        // Default to User for unknown roles
        return AuthorRole.User;
    }

    /// <summary>
    /// Convert Semantic Kernel AuthorRole to HazinaMessageRole
    /// </summary>
    public static HazinaMessageRole ToHazina(this AuthorRole role)
    {
        return role.Label switch
        {
            "REGULAR" => HazinaMessageRole.User,
            "assistant" => HazinaMessageRole.Assistant,
            "system" => HazinaMessageRole.System,
            _ => HazinaMessageRole.User
        };
    }

    #endregion

    #region Token Usage Extraction

    /// <summary>
    /// Extract token usage from Semantic Kernel function result metadata
    /// </summary>
    public static TokenUsageInfo ExtractTokenUsage(this FunctionResult result, string modelName = "")
    {
        var tokenUsage = new TokenUsageInfo { ModelName = modelName };

        if (result.Metadata == null)
            return tokenUsage;

        // Try to extract usage from metadata
        if (result.Metadata.TryGetValue("Usage", out var usage))
        {
            // SK stores usage in different formats depending on provider
            if (usage is Dictionary<string, object> usageDict)
            {
                if (usageDict.TryGetValue("InputTokens", out var inputTokens) && inputTokens is int input)
                    tokenUsage.InputTokens = input;
                if (usageDict.TryGetValue("OutputTokens", out var outputTokens) && outputTokens is int output)
                    tokenUsage.OutputTokens = output;

                // Alternative keys for OpenAI format
                if (usageDict.TryGetValue("PromptTokens", out var promptTokens) && promptTokens is int prompt)
                    tokenUsage.InputTokens = prompt;
                if (usageDict.TryGetValue("CompletionTokens", out var completionTokens) && completionTokens is int completion)
                    tokenUsage.OutputTokens = completion;
            }
        }

        return tokenUsage;
    }

    /// <summary>
    /// Extract token usage from streaming chat message content (deprecated - use SemanticKernelStreamHandler)
    /// </summary>
    [Obsolete("Use SemanticKernelStreamHandler.ExtractTokenUsageFromChunk instead")]
    public static void UpdateTokenUsage(this StreamingChatMessageContent chunk, TokenUsageInfo tokenUsage)
    {
        if (chunk.Metadata == null)
            return;

        if (chunk.Metadata.TryGetValue("Usage", out var usage) && usage is Dictionary<string, object> usageDict)
        {
            if (usageDict.TryGetValue("InputTokens", out var inputTokens) && inputTokens is int input)
                tokenUsage.InputTokens = Math.Max(tokenUsage.InputTokens, input);
            if (usageDict.TryGetValue("OutputTokens", out var outputTokens) && outputTokens is int output)
                tokenUsage.OutputTokens = Math.Max(tokenUsage.OutputTokens, output);

            // Alternative keys
            if (usageDict.TryGetValue("PromptTokens", out var promptTokens) && promptTokens is int prompt)
                tokenUsage.InputTokens = Math.Max(tokenUsage.InputTokens, prompt);
            if (usageDict.TryGetValue("CompletionTokens", out var completionTokens) && completionTokens is int completion)
                tokenUsage.OutputTokens = Math.Max(tokenUsage.OutputTokens, completion);
        }
    }

    /// <summary>
    /// Create an async enumerable wrapper for streaming with progress tracking
    /// </summary>
    public static async IAsyncEnumerable<T> WithProgress<T>(
        this IAsyncEnumerable<T> source,
        Action<int> onProgress,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var count = 0;
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            count++;
            onProgress?.Invoke(count);
            yield return item;
        }
    }

    /// <summary>
    /// Buffer streaming chunks for batch processing
    /// </summary>
    public static async IAsyncEnumerable<List<T>> Buffer<T>(
        this IAsyncEnumerable<T> source,
        int bufferSize,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var buffer = new List<T>(bufferSize);

        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            buffer.Add(item);

            if (buffer.Count >= bufferSize)
            {
                yield return buffer;
                buffer = new List<T>(bufferSize);
            }
        }

        // Return remaining items
        if (buffer.Count > 0)
        {
            yield return buffer;
        }
    }

    #endregion

    #region Response Format Conversion

    /// <summary>
    /// Convert HazinaChatResponseFormat to appropriate prompt execution settings
    /// </summary>
    public static void ApplyResponseFormat(this PromptExecutionSettings settings, HazinaChatResponseFormat format)
    {
        // Set response format based on Hazina enum
        if (format == HazinaChatResponseFormat.Json)
        {
            // For JSON responses, set the appropriate format
            if (settings is Microsoft.SemanticKernel.Connectors.OpenAI.OpenAIPromptExecutionSettings openAISettings)
            {
                openAISettings.ResponseFormat = "json_object";
            }
        }
        // Text format is default, no special handling needed
    }

    #endregion
}
