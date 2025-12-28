# Hazina.LLMs.Client

Core abstraction layer for interacting with Large Language Model providers. Defines the `ILLMClient` interface and tools context that all LLM providers (OpenAI, Anthropic, Gemini, etc.) must implement.

## Overview

This library provides the fundamental abstractions for LLM interactions:

- **ILLMClient**: Core interface for chat completions, embeddings, image generation, and TTS
- **IToolsContext**: Context for function calling and tool execution
- **Provider Agnostic**: Write code once, swap providers easily
- **Streaming Support**: Real-time response and audio streaming
- **Tool Integration**: Built-in support for function calling

## Quick Start

### Installation

```xml
<ProjectReference Include="..\path\to\Hazina.LLMs.Client\Hazina.LLMs.Client.csproj" />
```

### Basic Usage

```csharp
using Hazina.LLMs.Client;

// Use with any provider (OpenAI, Anthropic, etc.)
ILLMClient client = new OpenAIClient("api-key");

// Simple chat
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, "Hello!")
};

var response = await client.GetResponse(messages,
    HazinaChatResponseFormat.Text,
    toolsContext: null,
    images: null,
    CancellationToken.None);

Console.WriteLine(response.Result);
Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens}");
```

## Key Interfaces

### ILLMClient

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Client\ILLMClient.cs`

Main interface for LLM provider implementations.

**Methods**:

```csharp
// Generate embeddings
Task<Embedding> GenerateEmbedding(string data);

// Generate images
Task<LLMResponse<HazinaGeneratedImage>> GetImage(
    string prompt,
    HazinaChatResponseFormat responseFormat,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel);

// Chat completion (string response)
Task<LLMResponse<string>> GetResponse(
    List<HazinaChatMessage> messages,
    HazinaChatResponseFormat responseFormat,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel);

// Chat completion (structured response)
Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(
    List<HazinaChatMessage> messages,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel)
    where ResponseType : ChatResponse<ResponseType>, new();

// Streaming chat (string)
Task<LLMResponse<string>> GetResponseStream(
    List<HazinaChatMessage> messages,
    Action<string> onChunkReceived,
    HazinaChatResponseFormat responseFormat,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel);

// Streaming chat (structured)
Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(
    List<HazinaChatMessage> messages,
    Action<string> onChunkReceived,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel)
    where ResponseType : ChatResponse<ResponseType>, new();

// Text-to-Speech streaming
Task SpeakStream(
    string text,
    string voice,
    Action<byte[]> onAudioChunk,
    string mimeType,
    CancellationToken cancel);
```

**Usage Examples**:

```csharp
// 1. Simple chat
var response = await client.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    null, null, CancellationToken.None
);

// 2. Structured response
public class WeatherData : ChatResponse<WeatherData>
{
    public string Location { get; set; }
    public double Temperature { get; set; }
    // ... override _example and _signature
}

var weatherResponse = await client.GetResponse<WeatherData>(
    messages, null, null, CancellationToken.None
);

// 3. Streaming chat
await client.GetResponseStream(
    messages,
    chunk => Console.Write(chunk), // Called for each chunk
    HazinaChatResponseFormat.Text,
    null, null, CancellationToken.None
);

// 4. Generate embeddings
var embedding = await client.GenerateEmbedding("Hello world");
Console.WriteLine($"Dimensions: {embedding.Count}");

// 5. Text-to-speech
await client.SpeakStream(
    "Hello world",
    "alloy",
    audioChunk => ProcessAudio(audioChunk),
    "audio/mp3",
    CancellationToken.None
);
```

### IToolsContext

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Client\IToolsContext.cs`

Context for managing tools/functions available to the LLM.

**Properties**:
```csharp
List<HazinaChatTool> Tools { get; set; }
Action<string, string, string>? SendMessage { get; set; }
string? ProjectId { get; set; }
Action<string, int, int, string>? OnTokensUsed { get; set; }
```

**Methods**:
```csharp
void Add(HazinaChatTool info);
```

**Usage Example**:
```csharp
var toolsContext = new ToolsContext();

// Add a tool
toolsContext.Add(new HazinaChatTool(
    "get_weather",
    "Get weather for a location",
    new List<ChatToolParameter>
    {
        new() { Name = "location", Type = "string", Required = true }
    },
    async (messages, toolCall, cancel) =>
    {
        // Tool implementation
        return "Sunny, 72°F";
    }
));

// Set up callbacks
toolsContext.SendMessage = (agentName, functionName, message) =>
{
    Console.WriteLine($"[{agentName}/{functionName}] {message}");
};

toolsContext.OnTokensUsed = (model, inputTokens, outputTokens, usage) =>
{
    Console.WriteLine($"Used {inputTokens + outputTokens} tokens on {model}");
};

// Use in chat
var response = await client.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    toolsContext, // Tools now available to LLM
    null,
    CancellationToken.None
);
```

### ToolsContext

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Client\ToolsContext.cs`

Default implementation of `IToolsContext`.

**Constructor**:
```csharp
public ToolsContext()
```

**Usage Example**:
```csharp
var context = new ToolsContext
{
    ProjectId = "my-project-123",
    SendMessage = (agent, func, msg) => Logger.Log($"{agent}.{func}: {msg}"),
    OnTokensUsed = (model, input, output, usage) => Metrics.Record(model, input + output)
};

// Add multiple tools
context.Add(weatherTool);
context.Add(searchTool);
context.Add(calculatorTool);
```

## Usage Examples

### Provider-Agnostic Code

```csharp
// Your business logic works with any provider
public async Task<string> GetAIResponse(ILLMClient client, string userQuery)
{
    var messages = new List<HazinaChatMessage>
    {
        new(HazinaMessageRole.System, "You are a helpful assistant."),
        new(HazinaMessageRole.User, userQuery)
    };

    var response = await client.GetResponse(
        messages,
        HazinaChatResponseFormat.Text,
        null, null, CancellationToken.None
    );

    return response.Result;
}

// Swap providers easily
var openAIResponse = await GetAIResponse(new OpenAIClient("key"), "Hello");
var anthropicResponse = await GetAIResponse(new AnthropicClient("key"), "Hello");
var geminiResponse = await GetAIResponse(new GeminiClient("key"), "Hello");
```

### Multi-Tool Agent

```csharp
var toolsContext = new ToolsContext();

// Database tool
toolsContext.Add(new HazinaChatTool(
    "query_database",
    "Query the database",
    new List<ChatToolParameter>
    {
        new() { Name = "sql", Type = "string", Required = true }
    },
    async (msgs, call, cancel) =>
    {
        var param = new ChatToolParameter { Name = "sql" };
        if (param.TryGetValue(call, out string sql))
        {
            return await ExecuteQuery(sql);
        }
        return "Error: SQL not provided";
    }
));

// File system tool
toolsContext.Add(new HazinaChatTool(
    "read_file",
    "Read a file",
    new List<ChatToolParameter>
    {
        new() { Name = "path", Type = "string", Required = true }
    },
    async (msgs, call, cancel) =>
    {
        var param = new ChatToolParameter { Name = "path" };
        if (param.TryGetValue(call, out string path))
        {
            return await File.ReadAllTextAsync(path);
        }
        return "Error: Path not provided";
    }
));

// Agent can now use both tools
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, "Read the config.json file and query the database for users")
};

var response = await client.GetResponse(messages,
    HazinaChatResponseFormat.Text,
    toolsContext,
    null,
    CancellationToken.None);
```

### Real-Time Streaming

```csharp
var fullResponse = "";

await client.GetResponseStream(
    messages,
    chunk =>
    {
        fullResponse += chunk;
        Console.Write(chunk); // Print as it arrives
    },
    HazinaChatResponseFormat.Text,
    null, null, CancellationToken.None
);

Console.WriteLine($"\n\nFull response: {fullResponse}");
```

## Dependencies

- **Hazina.LLMs.Classes**: Message types, tool definitions, response formats
- **Hazina.LLMs.Helpers**: Embedding class and utilities

## Architecture

The client abstraction follows these principles:

1. **Interface Segregation**: Single `ILLMClient` interface for all operations
2. **Provider Agnostic**: Business logic independent of provider
3. **Extensibility**: Easy to add new providers
4. **Consistency**: All providers return the same types
5. **Streaming First**: Built-in support for real-time responses

## Implementation Notes

To implement a new LLM provider:

1. Implement `ILLMClient` interface
2. Map provider-specific types to Hazina types
3. Handle provider-specific features (tools, streaming, etc.)
4. Return `LLMResponse<T>` with token usage info

See existing implementations:
- `Hazina.LLMs.OpenAI`
- `Hazina.LLMs.Anthropic`
- `Hazina.LLMs.Gemini`

## See Also

- [Hazina.LLMs.Classes](../Hazina.LLMs.Classes/README.md) - Core data types
- [Hazina.LLMs.OpenAI](../../LLMs.Providers/Hazina.LLMs.OpenAI/README.md) - OpenAI implementation
- [Hazina.LLMs.Anthropic](../../LLMs.Providers/Hazina.LLMs.Anthropic/README.md) - Anthropic implementation
- [Hazina.AgentFactory](../../Agents/Hazina.AgentFactory/README.md) - Agent framework using this interface
