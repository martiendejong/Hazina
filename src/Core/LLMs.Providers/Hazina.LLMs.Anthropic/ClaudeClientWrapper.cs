using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hazina.LLMs.Anthropic;
using Hazina.LLMs;
using Hazina.LLMs.Capabilities;

/// <summary>
/// Anthropic Claude API client with full tool calling support.
/// Implements the Messages API with streaming and function calling.
/// </summary>
public class ClaudeClientWrapper : CapabilityProviderBase, ILLMClient
{
    private readonly AnthropicConfig _config;
    private readonly HttpClient _http;

    /// <inheritdoc/>
    public override ProviderCapability SupportedCapabilities =>
        ProviderCapability.Chat |
        ProviderCapability.Streaming |
        ProviderCapability.Tools |
        ProviderCapability.Vision |
        ProviderCapability.JsonMode |
        ProviderCapability.SystemMessages |
        ProviderCapability.StreamingTools;

    public ClaudeClientWrapper(AnthropicConfig config)
    {
        _config = config;
        _http = new HttpClient
        {
            BaseAddress = new Uri(config.Endpoint),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _http.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", _config.ApiVersion);
    }

    // Anthropic does not currently expose embeddings
    public Task<Embedding> GenerateEmbedding(string data)
        => throw new NotSupportedException("Anthropic Claude embeddings are not supported by this client.");

    #region Anthropic API Models

    // Request models - using classes with JsonPropertyName for proper serialization
    private sealed class AnthropicMessageRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = "";

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; } = 4096;

        [JsonPropertyName("messages")]
        public List<object> Messages { get; set; } = new();

        [JsonPropertyName("system")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? System { get; set; }

        [JsonPropertyName("tools")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<AnthropicTool>? Tools { get; set; }

        [JsonPropertyName("stream")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool Stream { get; set; }
    }

    private sealed class AnthropicSimpleMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("content")]
        public object Content { get; set; } = "";
    }

    private sealed class AnthropicTool
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("input_schema")]
        public object InputSchema { get; set; } = new object();
    }

    // Response models
    private sealed class AnthropicMessageResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("content")]
        public List<AnthropicContentBlock>? Content { get; set; }

        [JsonPropertyName("model")]
        public string? Model { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }

        [JsonPropertyName("usage")]
        public AnthropicUsage? Usage { get; set; }
    }

    private sealed class AnthropicContentBlock
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("input")]
        public JsonElement? Input { get; set; }
    }

    private sealed class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }
    }

    // Streaming event models
    private sealed class StreamEvent
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("message")]
        public AnthropicMessageResponse? Message { get; set; }

        [JsonPropertyName("index")]
        public int? Index { get; set; }

        [JsonPropertyName("content_block")]
        public AnthropicContentBlock? ContentBlock { get; set; }

        [JsonPropertyName("delta")]
        public StreamDelta? Delta { get; set; }

        [JsonPropertyName("usage")]
        public AnthropicUsage? Usage { get; set; }
    }

    private sealed class StreamDelta
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("partial_json")]
        public string? PartialJson { get; set; }

        [JsonPropertyName("stop_reason")]
        public string? StopReason { get; set; }
    }

    #endregion

    #region Message Mapping

    private static string BuildJsonFormatInstruction<ResponseType>() where ResponseType : ChatResponse<ResponseType>, new()
    {
        return $"YOUR OUTPUT WILL ALWAYS BE ONLY A JSON RESPONSE IN THIS FORMAT AND NOTHING ELSE: {ChatResponse<ResponseType>.Signature} EXAMPLE: {JsonSerializer.Serialize(ChatResponse<ResponseType>.Example)}";
    }

    private (string? system, List<object> msgs) MapMessages(List<HazinaChatMessage> messages)
    {
        // Gather system as a single string
        var systemParts = messages
            .Where(m => m.Role == HazinaMessageRole.System || m.Role?.Role == HazinaMessageRole.System?.Role)
            .Select(m => m.Text)
            .ToList();
        string? system = systemParts.Count > 0 ? string.Join("\n\n", systemParts) : null;

        var mapped = new List<object>();
        foreach (var m in messages)
        {
            if (m.Role == HazinaMessageRole.System || m.Role?.Role == HazinaMessageRole.System?.Role)
                continue;

            var role = (m.Role == HazinaMessageRole.Assistant || m.Role?.Role == HazinaMessageRole.Assistant?.Role)
                ? "assistant"
                : "user";

            mapped.Add(new AnthropicSimpleMessage { Role = role, Content = m.Text });
        }
        return (system, mapped);
    }

    private List<AnthropicTool>? MapTools(IToolsContext? toolsContext)
    {
        if (toolsContext?.Tools == null || toolsContext.Tools.Count == 0)
            return null;

        return toolsContext.Tools.Select(tool => new AnthropicTool
        {
            Name = tool.FunctionName,
            Description = tool.Description,
            InputSchema = CreateInputSchema(tool.Parameters)
        }).ToList();
    }

    private object CreateInputSchema(List<ChatToolParameter> parameters)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var param in parameters)
        {
            var propDef = new Dictionary<string, object>
            {
                ["type"] = param.Type,
                ["description"] = param.Description
            };

            if (string.Equals(param.Type, "array", StringComparison.OrdinalIgnoreCase))
            {
                propDef["items"] = new Dictionary<string, object>
                {
                    ["type"] = string.IsNullOrWhiteSpace(param.ItemsType) ? "string" : param.ItemsType
                };
            }

            properties[param.Name] = propDef;

            if (param.Required)
                required.Add(param.Name);
        }

        return new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
    }

    #endregion

    #region Core API Methods

    private async Task<(string text, TokenUsageInfo tokenUsage, List<AnthropicContentBlock> toolCalls)> CallClaudeWithTools(
        List<object> messages,
        string? system,
        IToolsContext? toolsContext,
        CancellationToken cancel)
    {
        var request = new AnthropicMessageRequest
        {
            Model = _config.Model,
            MaxTokens = 4096,
            Messages = messages,
            System = system,
            Tools = MapTools(toolsContext)
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var resp = await _http.PostAsync("/v1/messages", content, cancel);
        var respText = await resp.Content.ReadAsStringAsync(cancel);

        if (!resp.IsSuccessStatusCode)
        {
            throw new Exception($"Claude API error: {resp.StatusCode}: {respText}");
        }

        var response = JsonSerializer.Deserialize<AnthropicMessageResponse>(respText);
        if (response == null)
            throw new Exception("Failed to parse Anthropic response");

        var textContent = response.Content?
            .Where(c => c.Type == "text")
            .Select(c => c.Text)
            .FirstOrDefault() ?? "";

        var toolUseBlocks = response.Content?
            .Where(c => c.Type == "tool_use")
            .ToList() ?? new List<AnthropicContentBlock>();

        var tokenUsage = ExtractTokenUsage(response);

        return (textContent, tokenUsage, toolUseBlocks);
    }

    private async Task<(string text, TokenUsageInfo tokenUsage, List<AnthropicContentBlock> toolCalls)> CallClaudeStreaming(
        List<object> messages,
        string? system,
        Action<string> onChunkReceived,
        IToolsContext? toolsContext,
        CancellationToken cancel)
    {
        var request = new AnthropicMessageRequest
        {
            Model = _config.Model,
            MaxTokens = 4096,
            Messages = messages,
            System = system,
            Tools = MapTools(toolsContext),
            Stream = true
        };

        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/messages")
        {
            Content = content
        };
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        using var resp = await _http.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancel);

        if (!resp.IsSuccessStatusCode)
        {
            var errorText = await resp.Content.ReadAsStringAsync(cancel);
            throw new Exception($"Claude API error: {resp.StatusCode}: {errorText}");
        }

        var fullText = new StringBuilder();
        var toolCalls = new List<AnthropicContentBlock>();
        var currentToolCall = new Dictionary<int, AnthropicContentBlock>();
        var currentToolJson = new Dictionary<int, StringBuilder>();
        var tokenUsage = new TokenUsageInfo(0, 0, 0, 0, _config.Model);

        using var stream = await resp.Content.ReadAsStreamAsync(cancel);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancel)) != null)
        {
            cancel.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!line.StartsWith("data: "))
                continue;

            var eventData = line.Substring(6);
            if (eventData == "[DONE]")
                break;

            try
            {
                var streamEvent = JsonSerializer.Deserialize<StreamEvent>(eventData);
                if (streamEvent == null) continue;

                switch (streamEvent.Type)
                {
                    case "content_block_start":
                        if (streamEvent.ContentBlock?.Type == "tool_use" && streamEvent.Index.HasValue)
                        {
                            currentToolCall[streamEvent.Index.Value] = new AnthropicContentBlock
                            {
                                Type = "tool_use",
                                Id = streamEvent.ContentBlock.Id,
                                Name = streamEvent.ContentBlock.Name
                            };
                            currentToolJson[streamEvent.Index.Value] = new StringBuilder();
                        }
                        break;

                    case "content_block_delta":
                        if (streamEvent.Delta?.Text != null)
                        {
                            fullText.Append(streamEvent.Delta.Text);
                            onChunkReceived(streamEvent.Delta.Text);
                        }
                        else if (streamEvent.Delta?.PartialJson != null && streamEvent.Index.HasValue)
                        {
                            if (currentToolJson.ContainsKey(streamEvent.Index.Value))
                            {
                                currentToolJson[streamEvent.Index.Value].Append(streamEvent.Delta.PartialJson);
                            }
                        }
                        break;

                    case "content_block_stop":
                        if (streamEvent.Index.HasValue && currentToolCall.ContainsKey(streamEvent.Index.Value))
                        {
                            var tool = currentToolCall[streamEvent.Index.Value];
                            var jsonStr = currentToolJson.GetValueOrDefault(streamEvent.Index.Value)?.ToString() ?? "{}";
                            tool.Input = JsonDocument.Parse(jsonStr).RootElement;
                            toolCalls.Add(tool);
                        }
                        break;

                    case "message_delta":
                        if (streamEvent.Usage != null)
                        {
                            tokenUsage = new TokenUsageInfo(
                                tokenUsage.InputTokens,
                                streamEvent.Usage.OutputTokens,
                                tokenUsage.InputCost,
                                CalculateClaudeCost(_config.Model, streamEvent.Usage.OutputTokens, false),
                                _config.Model
                            );
                        }
                        break;

                    case "message_start":
                        if (streamEvent.Message?.Usage != null)
                        {
                            tokenUsage = new TokenUsageInfo(
                                streamEvent.Message.Usage.InputTokens,
                                tokenUsage.OutputTokens,
                                CalculateClaudeCost(_config.Model, streamEvent.Message.Usage.InputTokens, true),
                                tokenUsage.OutputCost,
                                _config.Model
                            );
                        }
                        break;
                }
            }
            catch (JsonException)
            {
                // Skip malformed events
            }
        }

        return (fullText.ToString(), tokenUsage, toolCalls);
    }

    private async Task<(string finalText, TokenUsageInfo totalUsage)> ExecuteWithToolLoop(
        List<HazinaChatMessage> hazMessages,
        IToolsContext? toolsContext,
        Action<string>? onChunkReceived,
        bool useStreaming,
        CancellationToken cancel)
    {
        var (system, mappedMessages) = MapMessages(hazMessages);
        var totalUsage = new TokenUsageInfo(0, 0, 0, 0, _config.Model);
        var finalText = new StringBuilder();
        var maxIterations = 50;
        var iteration = 0;

        while (iteration < maxIterations)
        {
            cancel.ThrowIfCancellationRequested();
            iteration++;

            string text;
            TokenUsageInfo usage;
            List<AnthropicContentBlock> toolCalls;

            if (useStreaming && onChunkReceived != null)
            {
                (text, usage, toolCalls) = await CallClaudeStreaming(
                    mappedMessages, system, onChunkReceived, toolsContext, cancel);
            }
            else
            {
                (text, usage, toolCalls) = await CallClaudeWithTools(
                    mappedMessages, system, toolsContext, cancel);
            }

            // Accumulate usage
            totalUsage = new TokenUsageInfo(
                totalUsage.InputTokens + usage.InputTokens,
                totalUsage.OutputTokens + usage.OutputTokens,
                totalUsage.InputCost + usage.InputCost,
                totalUsage.OutputCost + usage.OutputCost,
                _config.Model
            );

            if (!string.IsNullOrEmpty(text))
            {
                finalText.Append(text);
            }

            // No tool calls = we're done
            if (toolCalls.Count == 0)
                break;

            // Handle tool calls
            if (toolsContext?.Tools == null)
                break;

            // Build assistant message with tool_use content
            var assistantContent = new List<object>();
            if (!string.IsNullOrEmpty(text))
            {
                assistantContent.Add(new Dictionary<string, object>
                {
                    ["type"] = "text",
                    ["text"] = text
                });
            }
            foreach (var toolCall in toolCalls)
            {
                assistantContent.Add(new Dictionary<string, object>
                {
                    ["type"] = "tool_use",
                    ["id"] = toolCall.Id ?? "",
                    ["name"] = toolCall.Name ?? "",
                    ["input"] = toolCall.Input.HasValue
                        ? JsonSerializer.Deserialize<object>(toolCall.Input.Value.GetRawText()) ?? new object()
                        : new object()
                });
            }
            mappedMessages.Add(new Dictionary<string, object>
            {
                ["role"] = "assistant",
                ["content"] = assistantContent
            });

            // Execute tools and build user message with tool_result content
            var toolResults = new List<object>();
            foreach (var toolCall in toolCalls)
            {
                var tool = toolsContext.Tools.FirstOrDefault(t => t.FunctionName == toolCall.Name);
                if (tool == null)
                {
                    toolResults.Add(new Dictionary<string, object>
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = toolCall.Id ?? "",
                        ["content"] = $"Error: Unknown tool '{toolCall.Name}'"
                    });
                    continue;
                }

                var toolCallObj = new HazinaChatToolCall(
                    toolCall.Id ?? "",
                    toolCall.Name ?? "",
                    BinaryData.FromString(toolCall.Input?.GetRawText() ?? "{}")
                );

                // Log tool call
                toolsContext.SendMessage?.Invoke(
                    toolCallObj.Id,
                    tool.FunctionName,
                    $"Calling {tool.FunctionName}\n{toolCallObj.FunctionArguments}"
                );

                try
                {
                    var result = await tool.Execute(hazMessages, toolCallObj, cancel);

                    // Log tool result
                    toolsContext.SendMessage?.Invoke(
                        toolCallObj.Id,
                        tool.FunctionName,
                        result
                    );

                    toolResults.Add(new Dictionary<string, object>
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = toolCall.Id ?? "",
                        ["content"] = result
                    });
                }
                catch (Exception ex)
                {
                    toolResults.Add(new Dictionary<string, object>
                    {
                        ["type"] = "tool_result",
                        ["tool_use_id"] = toolCall.Id ?? "",
                        ["content"] = $"Error executing tool: {ex.Message}"
                    });
                }
            }

            // Add tool results as user message
            mappedMessages.Add(new Dictionary<string, object>
            {
                ["role"] = "user",
                ["content"] = toolResults
            });
        }

        return (finalText.ToString(), totalUsage);
    }

    #endregion

    #region Token/Cost Calculation

    private TokenUsageInfo ExtractTokenUsage(AnthropicMessageResponse? response)
    {
        if (response?.Usage == null)
            return new TokenUsageInfo(0, 0, 0, 0, _config.Model);

        var inputTokens = response.Usage.InputTokens;
        var outputTokens = response.Usage.OutputTokens;

        decimal inputCost = CalculateClaudeCost(_config.Model, inputTokens, true);
        decimal outputCost = CalculateClaudeCost(_config.Model, outputTokens, false);

        return new TokenUsageInfo(inputTokens, outputTokens, inputCost, outputCost, _config.Model);
    }

    private decimal CalculateClaudeCost(string model, int tokens, bool isInput)
    {
        decimal pricePerMillion = model.ToLower() switch
        {
            var m when m.Contains("claude-3-5-sonnet") || m.Contains("claude-sonnet-4") => isInput ? 3m : 15m,
            var m when m.Contains("claude-3-opus") || m.Contains("claude-opus-4") => isInput ? 15m : 75m,
            var m when m.Contains("claude-3-sonnet") => isInput ? 3m : 15m,
            var m when m.Contains("claude-3-haiku") || m.Contains("claude-haiku") => isInput ? 0.25m : 1.25m,
            _ => isInput ? 3m : 15m
        };

        return (tokens / 1_000_000m) * pricePerMillion;
    }

    #endregion

    #region ILLMClient Implementation

    public async Task<LLMResponse<string>> GetResponse(
        List<HazinaChatMessage> messages,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        var id = Guid.NewGuid().ToString();
        toolsContext?.SendMessage?.Invoke(id, "LLM Request (Claude)",
            string.Join("\n", messages.Select(m => $"{m.Role?.Role}: {m.Text}")));

        var (text, tokenUsage) = await ExecuteWithToolLoop(messages, toolsContext, null, false, cancel);

        toolsContext?.SendMessage?.Invoke(id, "LLM Response (Claude)", text);
        return new LLMResponse<string>(text, tokenUsage);
    }

    public async Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(
        List<HazinaChatMessage> messages,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var withFormat = new List<HazinaChatMessage>(messages);
        var instruction = BuildJsonFormatInstruction<ResponseType>();
        var insertIndex = Math.Max(0, withFormat.Count - 1);
        withFormat.Insert(insertIndex, new HazinaChatMessage { Role = HazinaMessageRole.System, Text = instruction });

        var response = await GetResponse(withFormat, HazinaChatResponseFormat.Json, toolsContext, images, cancel);

        try
        {
            var result = JsonSerializer.Deserialize<ResponseType>(response.Result);
            return new LLMResponse<ResponseType?>(result, response.TokenUsage);
        }
        catch
        {
            // Try to salvage by trimming to first JSON object
            var text = response.Result;
            var start = text.IndexOf('{');
            var end = text.LastIndexOf('}');
            if (start >= 0 && end > start)
            {
                var json = text.Substring(start, end - start + 1);
                var result = JsonSerializer.Deserialize<ResponseType>(json);
                return new LLMResponse<ResponseType?>(result, response.TokenUsage);
            }
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
        var id = Guid.NewGuid().ToString();
        toolsContext?.SendMessage?.Invoke(id, "LLM Request (Claude)",
            string.Join("\n", messages.Select(m => $"{m.Role?.Role}: {m.Text}")));

        var (text, tokenUsage) = await ExecuteWithToolLoop(messages, toolsContext, onChunkReceived, true, cancel);

        toolsContext?.SendMessage?.Invoke(id, "LLM Response (Claude)", text);
        return new LLMResponse<string>(text, tokenUsage);
    }

    public async Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel) where ResponseType : ChatResponse<ResponseType>, new()
    {
        var response = await GetResponseStream(messages, onChunkReceived, HazinaChatResponseFormat.Json, toolsContext, images, cancel);
        var result = JsonSerializer.Deserialize<ResponseType>(response.Result);
        return new LLMResponse<ResponseType?>(result, response.TokenUsage);
    }

    public Task<LLMResponse<HazinaGeneratedImage>> GetImage(
        string prompt,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
        => throw new NotSupportedException("Claude does not generate images in this client.");

    public Task SpeakStream(
        string text,
        string voice,
        Action<byte[]> onAudioChunk,
        string mimeType,
        CancellationToken cancel)
        => throw new NotSupportedException("Voice streaming is not supported for Anthropic in this client.");

    #endregion
}
