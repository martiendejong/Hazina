# Prompt-Based Tool Calling

This guide explains how to add tool/function calling support to **any LLM provider** using the `PromptBasedToolsOrchestrator`.

## Overview

The `PromptBasedToolsOrchestrator` enables tool calling for models that don't support native function calling (like older models, local models via Ollama, or custom endpoints). It works through prompt engineering:

1. **Describes tools** in a system prompt
2. **Instructs the model** to respond with JSON when calling tools
3. **Parses JSON responses** to detect tool calls
4. **Executes tools** using the existing `HazinaChatTool` infrastructure
5. **Feeds results back** and continues the conversation

## Architecture

```
┌─────────────────────────────────┐
│   Any ILLMClient Implementation │
│   (Gemini, Mistral, Custom)     │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│  PromptBasedToolsOrchestrator   │  ← Generic orchestrator
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│   Existing Tool Infrastructure  │
│   (HazinaChatTool, IToolsContext)│
└─────────────────────────────────┘
```

## How to Use

### Step 1: Add the Orchestrator to Your Client

```csharp
public class YourLLMClientWrapper : ILLMClient
{
    private readonly PromptBasedToolsOrchestrator _toolsOrchestrator;

    public YourLLMClientWrapper(YourConfig config)
    {
        // Initialize your LLM client
        // ...

        // Create orchestrator with max tool calls limit
        _toolsOrchestrator = new PromptBasedToolsOrchestrator(maxToolCalls: 50);
    }
}
```

### Step 2: Implement Internal Methods (Without Tools)

Create internal versions of your `GetResponse` methods that DON'T handle tools:

```csharp
// Public method - with tool support
public async Task<LLMResponse<string>> GetResponse(
    List<HazinaChatMessage> messages,
    HazinaChatResponseFormat responseFormat,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel)
{
    // Use orchestrator if tools are available
    if (toolsContext != null && toolsContext.Tools != null && toolsContext.Tools.Any())
    {
        return await _toolsOrchestrator.GetResponseWithToolsAsync(
            messages,
            toolsContext,
            async (msgs, ct) => await GetResponseInternal(msgs, responseFormat, images, ct),
            cancel
        );
    }

    // No tools - call directly
    return await GetResponseInternal(messages, responseFormat, images, cancel);
}

// Internal method - just calls your LLM, no tool handling
private async Task<LLMResponse<string>> GetResponseInternal(
    List<HazinaChatMessage> messages,
    HazinaChatResponseFormat responseFormat,
    List<ImageData>? images,
    CancellationToken cancel)
{
    // Your actual LLM API call here
    // Example: await _client.CompleteChatAsync(messages)
    // ...
}
```

### Step 3: Add Streaming Support (Optional)

For streaming responses, use `GetResponseStreamWithToolsAsync`:

```csharp
public async Task<LLMResponse<string>> GetResponseStream(
    List<HazinaChatMessage> messages,
    Action<string> onChunkReceived,
    HazinaChatResponseFormat responseFormat,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel)
{
    if (toolsContext != null && toolsContext.Tools != null && toolsContext.Tools.Any())
    {
        return await _toolsOrchestrator.GetResponseStreamWithToolsAsync(
            messages,
            toolsContext,
            async (msgs, onChunk, ct) => await GetResponseStreamInternal(msgs, onChunk, responseFormat, images, ct),
            onChunkReceived,
            cancel
        );
    }

    return await GetResponseStreamInternal(messages, onChunkReceived, responseFormat, images, cancel);
}

private async Task<LLMResponse<string>> GetResponseStreamInternal(
    List<HazinaChatMessage> messages,
    Action<string> onChunkReceived,
    HazinaChatResponseFormat responseFormat,
    List<ImageData>? images,
    CancellationToken cancel)
{
    // Your streaming implementation
    // ...
}
```

## Complete Example: Gemini Client

Here's a full example for adding tool support to a Gemini client:

```csharp
using Hazina.LLMs;

namespace Hazina.LLMs.Gemini;

public class GeminiClientWrapper : ILLMClient
{
    public GeminiConfig Config { get; set; }
    private readonly HttpClient _http;
    private readonly PromptBasedToolsOrchestrator _toolsOrchestrator;

    public GeminiClientWrapper(GeminiConfig config)
    {
        Config = config;
        _http = new HttpClient();
        _toolsOrchestrator = new PromptBasedToolsOrchestrator(maxToolCalls: 50);
    }

    #region Chat Completion

    public async Task<LLMResponse<string>> GetResponse(
        List<HazinaChatMessage> messages,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        // Check if tools are available
        if (toolsContext != null && toolsContext.Tools != null && toolsContext.Tools.Any())
        {
            // Use orchestrator for tool calling
            return await _toolsOrchestrator.GetResponseWithToolsAsync(
                messages,
                toolsContext,
                async (msgs, ct) => await GetResponseInternal(msgs, responseFormat, images, ct),
                cancel
            );
        }

        // No tools - call directly
        return await GetResponseInternal(messages, responseFormat, images, cancel);
    }

    private async Task<LLMResponse<string>> GetResponseInternal(
        List<HazinaChatMessage> messages,
        HazinaChatResponseFormat responseFormat,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        // Your Gemini API implementation here
        var response = await _http.PostAsync("https://generativelanguage.googleapis.com/v1/...", ...);
        // Parse and return
        // ...
    }

    #endregion
}
```

## Tool Call Format

The model is instructed to respond with this JSON format when calling tools:

```json
{
  "tool_call": true,
  "tool": "function_name",
  "arguments": {
    "param1": "value1",
    "param2": "value2"
  }
}
```

The orchestrator:
1. **Detects** this JSON pattern
2. **Parses** the tool name and arguments
3. **Finds** the matching tool in `IToolsContext.Tools`
4. **Executes** the tool via `tool.Execute()`
5. **Adds** the result to the conversation
6. **Continues** until a final answer

## Benefits

✅ **Works with any model** - No native tool support required
✅ **Reuses existing infrastructure** - Uses `HazinaChatTool` and `IToolsContext`
✅ **Zero changes to tools** - Tools don't know they're being called via prompts
✅ **Configurable** - Set max tool call limits
✅ **Logging** - Built-in console logging: `[PromptTools] Calling tool: ...`
✅ **Error handling** - Gracefully handles invalid JSON, missing tools, exceptions

## Performance Considerations

- Each tool call = separate LLM request (unlike native tool calling)
- Smaller models may struggle with complex tool chains
- Larger, instruction-tuned models work better
- Consider setting lower `maxToolCalls` for cost control

## Supported Providers

This can be used with:
- ✅ **Ollama** (phi3, llama3, mistral, qwen) - Already implemented
- ⚡ **Gemini** (older models without native tools)
- ⚡ **Claude** (Anthropic - older models)
- ⚡ **Mistral API**
- ⚡ **HuggingFace** inference endpoints
- ⚡ **Custom/self-hosted** models

## See Also

- **OllamaClientWrapper.cs** - Reference implementation
- **PromptBasedToolsOrchestrator.cs** - Source code with XML documentation
- **IToolsContext.cs** - Tool infrastructure interface
