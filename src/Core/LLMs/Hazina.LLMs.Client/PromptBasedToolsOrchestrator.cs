using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Hazina.LLMs;

/// <summary>
/// Provides prompt-engineering based tool calling for LLM models that don't support native function calling.
/// Works with any ILLMClient implementation by converting tools to text descriptions and parsing JSON responses.
/// </summary>
/// <remarks>
/// This orchestrator enables tool/function calling for models without native support (e.g., phi3, llama3, older models).
/// It works by:
/// 1. Generating a system prompt that describes available tools in human-readable format
/// 2. Instructing the model to respond with JSON when it wants to call a tool
/// 3. Detecting JSON tool call responses and executing the corresponding tool
/// 4. Feeding tool results back to the model and continuing the conversation
/// 5. Repeating until the model provides a final answer (max iterations configurable)
/// </remarks>
public class PromptBasedToolsOrchestrator
{
    private readonly int _maxToolCalls;
    private readonly ILogger<PromptBasedToolsOrchestrator> _logger;

    /// <summary>
    /// Creates a new instance of PromptBasedToolsOrchestrator
    /// </summary>
    /// <param name="maxToolCalls">Maximum number of tool calls allowed in a single conversation (default: 50)</param>
    /// <param name="logger">Optional logger for diagnostic output</param>
    public PromptBasedToolsOrchestrator(int maxToolCalls = 50, ILogger<PromptBasedToolsOrchestrator>? logger = null)
    {
        _maxToolCalls = maxToolCalls;
        _logger = logger ?? NullLogger<PromptBasedToolsOrchestrator>.Instance;
    }

    /// <summary>
    /// Orchestrates tool calling for a non-streaming LLM request
    /// </summary>
    /// <param name="messages">Conversation messages</param>
    /// <param name="toolsContext">Tools context with available tools</param>
    /// <param name="llmCall">Function to call the underlying LLM (without tools)</param>
    /// <param name="cancel">Cancellation token</param>
    /// <returns>Final LLM response after all tool calls are completed</returns>
    public async Task<LLMResponse<string>> GetResponseWithToolsAsync(
        List<HazinaChatMessage> messages,
        IToolsContext toolsContext,
        Func<List<HazinaChatMessage>, CancellationToken, Task<LLMResponse<string>>> llmCall,
        CancellationToken cancel)
    {
        int toolCallCount = 0;

        // Add tools system prompt
        var toolsPrompt = GenerateToolsSystemPrompt(toolsContext);
        var messagesWithTools = new List<HazinaChatMessage>(messages);
        if (!string.IsNullOrEmpty(toolsPrompt))
        {
            messagesWithTools.Insert(0, new HazinaChatMessage(HazinaMessageRole.System, toolsPrompt));
        }

        LLMResponse<string> response;
        bool requiresAction = true;

        while (requiresAction && toolCallCount < _maxToolCalls)
        {
            // Get response from model
            response = await llmCall(messagesWithTools, cancel);

            // Check if response is a tool call
            if (IsToolCallResponse(response.Result, out var toolCall) && toolCall != null)
            {
                toolCallCount++;

                // Add assistant message with tool call
                messagesWithTools.Add(new HazinaChatMessage(HazinaMessageRole.Assistant, response.Result));

                // Execute tool
                var toolResult = await ExecuteToolCallAsync(toolsContext, messages, toolCall, cancel);

                // Add tool result as system message
                messagesWithTools.Add(new HazinaChatMessage(
                    HazinaMessageRole.System,
                    $"Tool '{toolCall.Tool}' result:\n{toolResult}"
                ));

                // Continue loop
                requiresAction = true;
            }
            else
            {
                // Regular response, exit loop
                return response;
            }
        }

        if (toolCallCount >= _maxToolCalls)
        {
            throw new InvalidOperationException($"Maximum tool call limit ({_maxToolCalls}) exceeded.");
        }

        // This should never be reached, but return empty response if it does
        return new LLMResponse<string>("", new TokenUsageInfo());
    }

    /// <summary>
    /// Orchestrates tool calling for a streaming LLM request
    /// </summary>
    /// <param name="messages">Conversation messages</param>
    /// <param name="toolsContext">Tools context with available tools</param>
    /// <param name="llmCall">Function to call the underlying LLM with streaming (without tools)</param>
    /// <param name="onChunkReceived">Callback for each chunk received</param>
    /// <param name="cancel">Cancellation token</param>
    /// <returns>Final LLM response after all tool calls are completed</returns>
    public async Task<LLMResponse<string>> GetResponseStreamWithToolsAsync(
        List<HazinaChatMessage> messages,
        IToolsContext toolsContext,
        Func<List<HazinaChatMessage>, Action<string>, CancellationToken, Task<LLMResponse<string>>> llmCall,
        Action<string> onChunkReceived,
        CancellationToken cancel)
    {
        int toolCallCount = 0;

        // Add tools system prompt
        var toolsPrompt = GenerateToolsSystemPrompt(toolsContext);
        var messagesWithTools = new List<HazinaChatMessage>(messages);
        if (!string.IsNullOrEmpty(toolsPrompt))
        {
            messagesWithTools.Insert(0, new HazinaChatMessage(HazinaMessageRole.System, toolsPrompt));
        }

        LLMResponse<string> response;
        bool requiresAction = true;

        while (requiresAction && toolCallCount < _maxToolCalls)
        {
            // Get response from model
            response = await llmCall(messagesWithTools, onChunkReceived, cancel);

            // Check if response is a tool call
            if (IsToolCallResponse(response.Result, out var toolCall) && toolCall != null)
            {
                toolCallCount++;

                // Add assistant message with tool call
                messagesWithTools.Add(new HazinaChatMessage(HazinaMessageRole.Assistant, response.Result));

                // Execute tool
                var toolResult = await ExecuteToolCallAsync(toolsContext, messages, toolCall, cancel);

                // Add tool result as system message
                messagesWithTools.Add(new HazinaChatMessage(
                    HazinaMessageRole.System,
                    $"Tool '{toolCall.Tool}' result:\n{toolResult}"
                ));

                // Continue loop
                requiresAction = true;
            }
            else
            {
                // Regular response, exit loop
                return response;
            }
        }

        if (toolCallCount >= _maxToolCalls)
        {
            throw new InvalidOperationException($"Maximum tool call limit ({_maxToolCalls}) exceeded.");
        }

        // This should never be reached, but return empty response if it does
        return new LLMResponse<string>("", new TokenUsageInfo());
    }

    /// <summary>
    /// Generates a system prompt that describes available tools for models without native tool support.
    /// The model is instructed to respond with JSON when it wants to call a tool.
    /// </summary>
    private string GenerateToolsSystemPrompt(IToolsContext toolsContext)
    {
        if (toolsContext == null || toolsContext.Tools == null || !toolsContext.Tools.Any())
            return string.Empty;

        var toolsDescription = new StringBuilder();
        toolsDescription.AppendLine("AVAILABLE TOOLS:");
        toolsDescription.AppendLine("You have access to the following tools. When you need to use a tool, respond ONLY with a JSON object in this exact format:");
        toolsDescription.AppendLine("{");
        toolsDescription.AppendLine("  \"tool_call\": true,");
        toolsDescription.AppendLine("  \"tool\": \"tool_name\",");
        toolsDescription.AppendLine("  \"arguments\": { \"param1\": \"value1\", \"param2\": \"value2\" }");
        toolsDescription.AppendLine("}");
        toolsDescription.AppendLine();
        toolsDescription.AppendLine("IMPORTANT: When calling a tool, respond with ONLY the JSON object above, nothing else.");
        toolsDescription.AppendLine();

        foreach (var tool in toolsContext.Tools)
        {
            toolsDescription.AppendLine($"Tool: {tool.FunctionName}");
            toolsDescription.AppendLine($"Description: {tool.Description}");
            if (tool.Parameters.Any())
            {
                toolsDescription.AppendLine("Parameters:");
                foreach (var param in tool.Parameters)
                {
                    var required = param.Required ? "(required)" : "(optional)";
                    toolsDescription.AppendLine($"  - {param.Name} ({param.Type}): {param.Description} {required}");
                }
            }
            toolsDescription.AppendLine();
        }

        return toolsDescription.ToString();
    }

    /// <summary>
    /// Detects if the model response is a tool call request.
    /// </summary>
    private bool IsToolCallResponse(string response, out PromptToolCall? toolCall)
    {
        toolCall = null;

        try
        {
            // Try to parse as JSON
            var trimmed = response.Trim();
            if (!trimmed.StartsWith("{") || !trimmed.EndsWith("}"))
                return false;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            var parsed = JsonSerializer.Deserialize<PromptToolCall>(trimmed, options);
            if (parsed != null && parsed.ToolCall == true && !string.IsNullOrEmpty(parsed.Tool))
            {
                toolCall = parsed;
                return true;
            }
        }
        catch (JsonException)
        {
            // Not valid JSON, treat as regular response
        }

        return false;
    }

    /// <summary>
    /// Executes a tool call and returns the result.
    /// </summary>
    private async Task<string> ExecuteToolCallAsync(
        IToolsContext toolsContext,
        List<HazinaChatMessage> messages,
        PromptToolCall toolCallRequest,
        CancellationToken cancel)
    {
        var tool = toolsContext.Tools.FirstOrDefault(t => t.FunctionName == toolCallRequest.Tool);
        if (tool == null)
        {
            return $"Error: Tool '{toolCallRequest.Tool}' not found.";
        }

        try
        {
            // Convert arguments to BinaryData
            var argsJson = JsonSerializer.Serialize(toolCallRequest.Arguments ?? new Dictionary<string, object>());
            var argsBinary = BinaryData.FromString(argsJson);

            // Create HazinaChatToolCall
            var hazToolCall = new HazinaChatToolCall(
                id: Guid.NewGuid().ToString(),
                functionName: toolCallRequest.Tool,
                functionArguments: argsBinary
            );

            // Execute the tool
            _logger.LogDebug("Calling tool: {ToolName} with arguments: {Arguments}",
                tool.FunctionName, argsJson);

            var result = await tool.Execute(messages, hazToolCall, cancel);

            _logger.LogDebug("Tool {ToolName} returned: {Result}",
                tool.FunctionName, result.Length > 200 ? result[..200] + "..." : result);

            return result;
        }
        catch (Exception ex)
        {
            return $"Error executing tool '{toolCallRequest.Tool}': {ex.Message}";
        }
    }
}

/// <summary>
/// Model for prompt-based tool call requests (for models without native tool support)
/// </summary>
public class PromptToolCall
{
    [JsonPropertyName("tool_call")]
    public bool ToolCall { get; set; }

    [JsonPropertyName("tool")]
    public string Tool { get; set; } = string.Empty;

    [JsonPropertyName("arguments")]
    public Dictionary<string, object>? Arguments { get; set; }
}
