using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevGPT.LLMs;

/// <summary>
/// Extension methods for converting between DevGPT types and Semantic Kernel types
/// </summary>
public static class DevGPTSemanticKernelExtensions
{
    #region DevGPTChatMessage <-> ChatHistory Conversion

    /// <summary>
    /// Convert DevGPTChatMessage to Semantic Kernel ChatMessageContent
    /// </summary>
    public static ChatMessageContent ToSemanticKernel(this DevGPTChatMessage message)
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
    /// Convert Semantic Kernel ChatMessageContent to DevGPTChatMessage
    /// </summary>
    public static DevGPTChatMessage ToDevGPT(this ChatMessageContent message)
    {
        var devGptMessage = new DevGPTChatMessage
        {
            Role = message.Role.ToDevGPT(),
            Text = message.Content ?? string.Empty
        };

        // Restore metadata if available
        if (message.Metadata != null)
        {
            if (message.Metadata.TryGetValue("MessageId", out var messageId) && messageId is Guid guid)
                devGptMessage.MessageId = guid;
            if (message.Metadata.TryGetValue("AgentName", out var agentName) && agentName is string agent)
                devGptMessage.AgentName = agent;
            if (message.Metadata.TryGetValue("FunctionName", out var functionName) && functionName is string func)
                devGptMessage.FunctionName = func;
            if (message.Metadata.TryGetValue("FlowName", out var flowName) && flowName is string flow)
                devGptMessage.FlowName = flow;
            if (message.Metadata.TryGetValue("Response", out var response) && response is string resp)
                devGptMessage.Response = resp;
        }

        return devGptMessage;
    }

    /// <summary>
    /// Convert list of DevGPTChatMessage to Semantic Kernel ChatHistory
    /// </summary>
    public static ChatHistory ToSemanticKernelChatHistory(this List<DevGPTChatMessage> messages)
    {
        var chatHistory = new ChatHistory();

        foreach (var message in messages)
        {
            chatHistory.Add(message.ToSemanticKernel());
        }

        return chatHistory;
    }

    /// <summary>
    /// Convert Semantic Kernel ChatHistory to list of DevGPTChatMessage
    /// </summary>
    public static List<DevGPTChatMessage> ToDevGPT(this ChatHistory chatHistory)
    {
        return chatHistory.Select(m => m.ToDevGPT()).ToList();
    }

    #endregion

    #region Role Conversion

    /// <summary>
    /// Convert DevGPTMessageRole to Semantic Kernel AuthorRole
    /// </summary>
    public static AuthorRole ToSemanticKernel(this DevGPTMessageRole role)
    {
        if (role.Role == DevGPTMessageRole.User.Role)
            return AuthorRole.User;
        if (role.Role == DevGPTMessageRole.Assistant.Role)
            return AuthorRole.Assistant;
        if (role.Role == DevGPTMessageRole.System.Role)
            return AuthorRole.System;

        // Default to User for unknown roles
        return AuthorRole.User;
    }

    /// <summary>
    /// Convert Semantic Kernel AuthorRole to DevGPTMessageRole
    /// </summary>
    public static DevGPTMessageRole ToDevGPT(this AuthorRole role)
    {
        return role.Label switch
        {
            "REGULAR" => DevGPTMessageRole.User,
            "assistant" => DevGPTMessageRole.Assistant,
            "system" => DevGPTMessageRole.System,
            _ => DevGPTMessageRole.User
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
    /// Convert DevGPTChatResponseFormat to appropriate prompt execution settings
    /// </summary>
    public static void ApplyResponseFormat(this PromptExecutionSettings settings, DevGPTChatResponseFormat format)
    {
        // Set response format based on DevGPT enum
        if (format == DevGPTChatResponseFormat.Json)
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
