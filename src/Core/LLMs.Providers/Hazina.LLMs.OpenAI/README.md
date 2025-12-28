# Hazina.LLMs.OpenAI

OpenAI provider implementation of the `ILLMClient` interface for GPT models, embeddings, image generation, and text-to-speech.

## Overview

This library provides the OpenAI integration for the Hazina platform:

- **OpenAIClientWrapper**: Implements `ILLMClient` for GPT-4, GPT-3.5, and other OpenAI models
- **Configuration**: Flexible config via `OpenAIConfig` or appsettings.json
- **Streaming Support**: Real-time streaming responses
- **Tool Calling**: Function calling with automatic tool execution
- **Embeddings**: text-embedding-ada-002 and newer models
- **Image Generation**: DALL-E integration
- **Retry Logic**: Automatic retry on transient failures

## Quick Start

### Installation

```xml
<ProjectReference Include="..\path\to\Hazina.LLMs.OpenAI\Hazina.LLMs.OpenAI.csproj" />
```

### Basic Usage

```csharp
using Hazina.LLMs.OpenAI;

// Configure client
var config = new OpenAIConfig(
    apiKey: "sk-...",
    model: "gpt-4",
    embeddingModel: "text-embedding-ada-002"
);

var client = new OpenAIClientWrapper(config);

// Chat completion
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.System, "You are a helpful assistant."),
    new(HazinaMessageRole.User, "What is 2+2?")
};

var response = await client.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    null, null, CancellationToken.None
);

Console.WriteLine(response.Result); // "2+2 equals 4."
Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

## Key Classes

### OpenAIClientWrapper

**Location**: `Core/OpenAIClientWrapper.cs:13`

Main implementation of `ILLMClient` for OpenAI models.

**Constructor**:
```csharp
public OpenAIClientWrapper(OpenAIConfig config)
```

**Key Methods**:

```csharp
// Chat completion
Task<LLMResponse<string>> GetResponse(
    List<HazinaChatMessage> messages,
    HazinaChatResponseFormat responseFormat,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel)

// Streaming chat
Task<LLMResponse<string>> GetResponseStream(
    List<HazinaChatMessage> messages,
    Action<string> onChunkReceived,
    HazinaChatResponseFormat responseFormat,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel)

// Structured response
Task<LLMResponse<TResponse?>> GetResponse<TResponse>(
    List<HazinaChatMessage> messages,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel)
    where TResponse : ChatResponse<TResponse>, new()

// Generate embeddings
Task<Embedding> GenerateEmbedding(string text)

// Image generation
Task<LLMResponse<HazinaGeneratedImage>> GetImage(
    string prompt,
    HazinaChatResponseFormat responseFormat,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel)
```

**Usage Example**:
```csharp
var config = new OpenAIConfig("sk-...", model: "gpt-4");
var client = new OpenAIClientWrapper(config);

// Simple chat
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, "Explain quantum computing")
};

var response = await client.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    null, null, CancellationToken.None
);

Console.WriteLine(response.Result);

// Streaming response
await client.GetResponseStream(
    messages,
    chunk => Console.Write(chunk), // Real-time output
    HazinaChatResponseFormat.Text,
    null, null, CancellationToken.None
);

// Structured response
public class WeatherInfo : ChatResponse<WeatherInfo>
{
    public string Location { get; set; }
    public double Temperature { get; set; }
    public override WeatherInfo _example => new() { Location = "NYC", Temperature = 72 };
    public override string _signature => "WeatherInfo(Location: string, Temperature: double)";
}

var weatherMessages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, "What's the weather in New York?")
};

var weatherResponse = await client.GetResponse<WeatherInfo>(
    weatherMessages, null, null, CancellationToken.None
);

Console.WriteLine($"{weatherResponse.Result?.Location}: {weatherResponse.Result?.Temperature}°F");

// Generate embeddings
var embedding = await client.GenerateEmbedding("Hello, world!");
Console.WriteLine($"Embedding dimensions: {embedding.Count}");
```

### OpenAIConfig

**Location**: `Models/OpenAIConfig.cs:3`

Configuration for OpenAI client.

**Properties**:
```csharp
public string ApiKey { get; set; }
public string Model { get; set; }                    // Default: "gpt-4.1"
public string ImageModel { get; set; }               // Default: "gpt-image-1"
public string EmbeddingModel { get; set; }           // Default: "text-embedding-ada-002"
public string LogPath { get; set; }                  // Default: "c:\\projects\\hazinalogs.txt"
public string TtsModel { get; set; }                 // Default: "gpt-4o-mini-tts"
```

**Constructors**:
```csharp
public OpenAIConfig(
    string apiKey = "",
    string embeddingModel = "text-embedding-ada-002",
    string model = "gpt-4.1",
    string imageModel = "gpt-image-1",
    string logPath = "c:\\projects\\hazinalogs.txt",
    string ttsModel = "gpt-4o-mini-tts")
```

**Static Methods**:
```csharp
public static OpenAIConfig Load() // Loads from appsettings.json
```

**Usage Example**:
```csharp
// Manual configuration
var config = new OpenAIConfig(
    apiKey: "sk-proj-...",
    model: "gpt-4-turbo",
    embeddingModel: "text-embedding-3-small"
);

// Load from appsettings.json
var configFromFile = OpenAIConfig.Load();

// appsettings.json format:
// {
//   "OpenAI": {
//     "ApiKey": "sk-...",
//     "Model": "gpt-4",
//     "EmbeddingModel": "text-embedding-ada-002"
//   }
// }
```

### SimpleOpenAIClientChatInteraction

**Location**: `Core/SimpleOpenAIClientChatInteraction.cs:12`

Handles chat interactions with automatic tool calling and conversation management.

**Key Methods**:
```csharp
// Run chat with tool support
public async Task<ChatCompletion> Run(CancellationToken cancellationToken)

// Stream chat with tool support
public async IAsyncEnumerable<StreamingChatCompletionUpdate> Stream(
    CancellationToken cancellationToken)

// Generate image
public async Task<GeneratedImage> RunImage(
    string prompt,
    CancellationToken cancellationToken)
```

**Features**:
- Automatic tool calling loop (up to 50 iterations)
- Tool result integration into conversation
- Token usage tracking
- Cancellation support

**Usage Example**:
```csharp
var toolsContext = new ToolsContext();
toolsContext.Add(new HazinaChatTool(
    "get_weather",
    "Get weather for a location",
    new List<ChatToolParameter>
    {
        new() { Name = "location", Type = "string", Required = true }
    },
    async (msgs, call, cancel) => "Sunny, 72°F"
));

var interaction = new SimpleOpenAIClientChatInteraction(
    toolsContext,
    api,
    wrapper,
    apiKey,
    "gpt-4",
    logPath,
    chatClient,
    imageClient,
    messages,
    null,
    ChatResponseFormat.CreateTextFormat(),
    useWebSearch: false,
    useReasoning: false
);

var completion = await interaction.Run(CancellationToken.None);
```

### OpenAIStreamHandler

**Location**: `Handlers/OpenAIStreamHandler.cs:5`

Handles streaming responses from OpenAI API.

**Methods**:
```csharp
public async Task<string> HandleStream(
    Action<string> onChunkReceived,
    IAsyncEnumerable<StreamingChatCompletionUpdate> stream,
    TokenUsageInfo tokenUsage)
```

**Usage Example**:
```csharp
var handler = new OpenAIStreamHandler();
var tokenUsage = new TokenUsageInfo();

var fullResponse = await handler.HandleStream(
    chunk => Console.Write(chunk),
    streamingCompletion,
    tokenUsage
);

Console.WriteLine($"\n\nTotal tokens: {tokenUsage.TotalTokens}");
```

### HazinaOpenAIExtensions

**Location**: `Utilities/HazinaOpenAIExtensions.cs:5`

Extension methods to convert between Hazina and OpenAI types.

**Key Extensions**:
```csharp
// Convert Hazina messages to OpenAI
List<ChatMessage> OpenAI(this List<HazinaChatMessage> messages)

// Convert OpenAI messages to Hazina
List<HazinaChatMessage> Hazina(this List<ChatMessage> messages)

// Convert tools
ChatTool OpenAI(this HazinaChatTool tool)

// Convert response formats
ChatResponseFormat OpenAI(this HazinaChatResponseFormat format)
```

**Usage Example**:
```csharp
var hazinaMessages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, "Hello")
};

// Convert to OpenAI format
var openAIMessages = hazinaMessages.OpenAI();

// Convert back to Hazina format
var backToHazina = openAIMessages.Hazina();
```

### Retry

**Location**: `Utilities/Retry.cs`

Automatic retry logic for transient failures.

**Static Methods**:
```csharp
public static async Task<T> Run<T>(Func<Task<T>> action)
```

**Usage Example**:
```csharp
var result = await Retry.Run(async () =>
{
    // This will retry on transient failures
    return await client.GenerateEmbedding("text");
});
```

## Usage Examples

### Complete Chat with Tools

```csharp
// 1. Configure client
var config = new OpenAIConfig("sk-...", model: "gpt-4");
var client = new OpenAIClientWrapper(config);

// 2. Set up tools
var toolsContext = new ToolsContext();

toolsContext.Add(new HazinaChatTool(
    "search_docs",
    "Search documentation",
    new List<ChatToolParameter>
    {
        new() { Name = "query", Type = "string", Required = true }
    },
    async (msgs, call, cancel) =>
    {
        // Implement search logic
        return "Found 3 results about authentication";
    }
));

// 3. Chat with automatic tool calling
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.System, "You are a helpful assistant with access to documentation."),
    new(HazinaMessageRole.User, "How do I authenticate users?")
};

var response = await client.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    toolsContext, // Tools available for the LLM to call
    null,
    CancellationToken.None
);

Console.WriteLine(response.Result);
```

### Streaming with Real-Time Display

```csharp
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, "Write a story about a robot")
};

Console.Write("Response: ");
var response = await client.GetResponseStream(
    messages,
    chunk => Console.Write(chunk), // Print each chunk as it arrives
    HazinaChatResponseFormat.Text,
    null, null,
    CancellationToken.None
);

Console.WriteLine($"\n\nTotal tokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

### Embeddings for RAG

```csharp
var client = new OpenAIClientWrapper(new OpenAIConfig("sk-..."));

// Embed documents
var documents = new[] { "Doc 1", "Doc 2", "Doc 3" };
var embeddings = new List<(string doc, Embedding embedding)>();

foreach (var doc in documents)
{
    var embedding = await client.GenerateEmbedding(doc);
    embeddings.Add((doc, embedding));
}

// Find similar documents
var query = "search term";
var queryEmbedding = await client.GenerateEmbedding(query);

var results = embeddings
    .Select(e => new
    {
        Document = e.doc,
        Similarity = queryEmbedding.CosineSimilarity(e.embedding)
    })
    .OrderByDescending(r => r.Similarity)
    .Take(3);

foreach (var result in results)
{
    Console.WriteLine($"[{result.Similarity:F4}] {result.Document}");
}
```

### Image Generation

```csharp
var response = await client.GetImage(
    "A futuristic city with flying cars",
    HazinaChatResponseFormat.Text,
    null, null,
    CancellationToken.None
);

var image = response.Result;
Console.WriteLine($"Image URL: {image.ImageUri}");

// Save to file
if (image.ImageBytes != null)
{
    await File.WriteAllBytesAsync("generated.png", image.ImageBytes.ToArray());
}
```

## Dependencies

- **OpenAI SDK**: Official OpenAI .NET client
- **Hazina.LLMs.Client**: Core LLM abstractions
- **Hazina.LLMs.Classes**: Message and tool types
- **Hazina.LLMs.Helpers**: Embedding, token counting, parsing utilities
- **Microsoft.Extensions.Configuration**: Configuration loading

## Configuration

### appsettings.json

```json
{
  "OpenAI": {
    "ApiKey": "sk-proj-...",
    "Model": "gpt-4-turbo",
    "EmbeddingModel": "text-embedding-3-small",
    "ImageModel": "dall-e-3",
    "LogPath": "C:\\logs\\hazina.txt",
    "TtsModel": "tts-1"
  }
}
```

### Environment Variables

```csharp
var config = new OpenAIConfig(
    apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "",
    model: "gpt-4"
);
```

## Error Handling

The client automatically retries on transient failures using the `Retry` utility:

```csharp
try
{
    var response = await client.GetResponse(messages, ...);
}
catch (OperationCanceledException)
{
    // User cancelled the operation
}
catch (Exception ex)
{
    // Handle API errors
    Console.WriteLine($"Error: {ex.Message}");
}
```

## See Also

- [Hazina.LLMs.Client](../../LLMs/Hazina.LLMs.Client/README.md) - Core abstractions
- [Hazina.LLMs.Anthropic](../Hazina.LLMs.Anthropic/README.md) - Anthropic provider
- [Hazina.AgentFactory](../../../Agents/Hazina.AgentFactory/README.md) - Agent framework
- [OpenAI API Documentation](https://platform.openai.com/docs/api-reference)
