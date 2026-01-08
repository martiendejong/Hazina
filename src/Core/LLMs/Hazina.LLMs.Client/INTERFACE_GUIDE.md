# ILLMClient Interface Guide

## Purpose

`ILLMClient` is the foundational interface for all LLM interactions in Hazina. Every provider (OpenAI, Anthropic, Gemini, etc.) implements this interface, enabling provider-agnostic code.

## Why It Exists

```
┌─────────────────────────────────────────────────────────────────┐
│                     WITHOUT ILLMClient                          │
│                                                                  │
│   Your Code ──► OpenAI SDK ──► OpenAI API                       │
│                                                                  │
│   Problem: Locked to one provider                               │
│   Problem: Different API for each provider                      │
│   Problem: Hard to test (real API calls)                        │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                      WITH ILLMClient                            │
│                                                                  │
│   Your Code ──► ILLMClient ──► Any Provider                     │
│                     │                                            │
│                     ├──► OpenAIClient ──► OpenAI API            │
│                     ├──► ClaudeClient ──► Anthropic API         │
│                     ├──► GeminiClient ──► Google API            │
│                     └──► MockClient ──► Tests                   │
│                                                                  │
│   Benefit: Swap providers without code changes                  │
│   Benefit: Easy testing with mocks                              │
│   Benefit: Consistent API everywhere                            │
└─────────────────────────────────────────────────────────────────┘
```

## Interface Definition

```csharp
public interface ILLMClient
{
    // Generate vector embedding from text
    Task<Embedding> GenerateEmbedding(string data);

    // Generate image from prompt
    Task<LLMResponse<HazinaGeneratedImage>> GetImage(
        string prompt,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel);

    // Chat completion (returns string)
    Task<LLMResponse<string>> GetResponse(
        List<HazinaChatMessage> messages,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel);

    // Chat completion (returns structured type)
    Task<LLMResponse<ResponseType?>> GetResponse<ResponseType>(
        List<HazinaChatMessage> messages,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
        where ResponseType : ChatResponse<ResponseType>, new();

    // Streaming chat (returns string)
    Task<LLMResponse<string>> GetResponseStream(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel);

    // Streaming chat (returns structured type)
    Task<LLMResponse<ResponseType?>> GetResponseStream<ResponseType>(
        List<HazinaChatMessage> messages,
        Action<string> onChunkReceived,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
        where ResponseType : ChatResponse<ResponseType>, new();

    // Text-to-speech streaming
    Task SpeakStream(
        string text,
        string voice,
        Action<byte[]> onAudioChunk,
        string mimeType,
        CancellationToken cancel);
}
```

## Method Reference

### GenerateEmbedding
Converts text into a vector embedding for semantic search.

```csharp
var embedding = await client.GenerateEmbedding("Hello world");
// Returns: Embedding with 1536 dimensions (OpenAI default)
```

**When to use**: RAG indexing, semantic search, similarity comparisons.

### GetResponse (string)
Standard chat completion returning text.

```csharp
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.System, "You are helpful."),
    new(HazinaMessageRole.User, "What is 2+2?")
};

var response = await client.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    toolsContext: null,
    images: null,
    CancellationToken.None);

Console.WriteLine(response.Result); // "4"
Console.WriteLine(response.TokenUsage.TotalTokens); // e.g., 25
```

**When to use**: Simple Q&A, text generation, general chat.

### GetResponse<T> (structured)
Chat completion with strongly-typed response.

```csharp
public class WeatherInfo : ChatResponse<WeatherInfo>
{
    public string Location { get; set; }
    public double Temperature { get; set; }
    public string Conditions { get; set; }
}

var response = await client.GetResponse<WeatherInfo>(
    messages,
    toolsContext: null,
    images: null,
    CancellationToken.None);

Console.WriteLine(response.Result.Temperature); // 72.5
```

**When to use**: Extracting structured data, JSON responses, typed outputs.

### GetResponseStream
Streaming response for real-time output.

```csharp
var fullResponse = await client.GetResponseStream(
    messages,
    chunk => Console.Write(chunk), // Called for each token
    HazinaChatResponseFormat.Text,
    toolsContext: null,
    images: null,
    CancellationToken.None);

// Chunks arrive one by one: "The" "answer" "is" "42"
```

**When to use**: Chat UIs, long responses, perceived latency reduction.

### GetImage
Generate images from text prompts (DALL-E, etc.).

```csharp
var response = await client.GetImage(
    "A sunset over mountains",
    HazinaChatResponseFormat.Url, // or Base64
    toolsContext: null,
    images: null,
    CancellationToken.None);

Console.WriteLine(response.Result.Url);
```

**When to use**: Image generation features.

### SpeakStream
Text-to-speech with streaming audio.

```csharp
await client.SpeakStream(
    "Hello, how are you?",
    "alloy", // Voice name
    audioChunk => PlayAudio(audioChunk),
    "audio/mp3",
    CancellationToken.None);
```

**When to use**: Voice assistants, audio responses.

## Supporting Types

### HazinaChatMessage
```csharp
public record HazinaChatMessage(HazinaMessageRole Role, string Text);

public enum HazinaMessageRole
{
    System,    // System instructions
    User,      // User input
    Assistant, // AI response
    Tool       // Tool/function result
}
```

### LLMResponse<T>
```csharp
public class LLMResponse<T>
{
    public T Result { get; set; }           // The actual response
    public TokenUsage TokenUsage { get; set; } // Token counts
    public string? Model { get; set; }      // Model used
    public string? FinishReason { get; set; } // "stop", "length", etc.
}
```

### IToolsContext
```csharp
public interface IToolsContext
{
    List<HazinaChatTool> Tools { get; set; }
    Action<string, string, string>? SendMessage { get; set; }
    string? ProjectId { get; set; }
    Action<string, int, int, string>? OnTokensUsed { get; set; }

    void Add(HazinaChatTool info);
}
```

## Provider Implementations

| Provider | Class | Package |
|----------|-------|---------|
| OpenAI | `OpenAIClientWrapper` | `Hazina.LLMs.OpenAI` |
| Anthropic | `ClaudeClientWrapper` | `Hazina.LLMs.Anthropic` |
| Google Gemini | `GeminiClient` | `Hazina.LLMs.Gemini` |
| Mistral | `MistralClient` | `Hazina.LLMs.Mistral` |
| HuggingFace | `HuggingFaceClient` | `Hazina.LLMs.HuggingFace` |
| Local | Various | `Hazina.LLMs.Local` |

## Implementing a New Provider

1. Create new project: `Hazina.LLMs.{ProviderName}`
2. Implement `ILLMClient`:

```csharp
public class MyProviderClient : ILLMClient
{
    private readonly MyProviderSDK _sdk;

    public MyProviderClient(string apiKey)
    {
        _sdk = new MyProviderSDK(apiKey);
    }

    public async Task<LLMResponse<string>> GetResponse(
        List<HazinaChatMessage> messages,
        HazinaChatResponseFormat responseFormat,
        IToolsContext? toolsContext,
        List<ImageData>? images,
        CancellationToken cancel)
    {
        // 1. Convert Hazina messages to provider format
        var providerMessages = messages.Select(m => new ProviderMessage
        {
            Role = MapRole(m.Role),
            Content = m.Text
        }).ToList();

        // 2. Call provider API
        var response = await _sdk.ChatAsync(providerMessages, cancel);

        // 3. Convert response to Hazina format
        return new LLMResponse<string>
        {
            Result = response.Content,
            TokenUsage = new TokenUsage
            {
                PromptTokens = response.Usage.Input,
                CompletionTokens = response.Usage.Output,
                TotalTokens = response.Usage.Total
            },
            Model = response.Model,
            FinishReason = response.StopReason
        };
    }

    // Implement other methods...
}
```

3. Handle provider-specific features:
   - Tool/function calling format
   - Streaming protocol
   - Error mapping
   - Rate limit headers

## Testing with ILLMClient

```csharp
public class MyServiceTests
{
    [Fact]
    public async Task MyService_UsesLLMCorrectly()
    {
        // Arrange
        var mockClient = new Mock<ILLMClient>();
        mockClient
            .Setup(c => c.GetResponse(
                It.IsAny<List<HazinaChatMessage>>(),
                It.IsAny<HazinaChatResponseFormat>(),
                It.IsAny<IToolsContext>(),
                It.IsAny<List<ImageData>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LLMResponse<string> { Result = "Mocked response" });

        var service = new MyService(mockClient.Object);

        // Act
        var result = await service.DoSomething();

        // Assert
        Assert.Equal("Mocked response", result);
    }
}
```

## Common Patterns

### Retry Wrapper
```csharp
public class RetryingLLMClient : ILLMClient
{
    private readonly ILLMClient _inner;
    private readonly int _maxRetries;

    public async Task<LLMResponse<string>> GetResponse(...)
    {
        for (int i = 0; i <= _maxRetries; i++)
        {
            try
            {
                return await _inner.GetResponse(...);
            }
            catch (RateLimitException) when (i < _maxRetries)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
            }
        }
        throw new Exception("Max retries exceeded");
    }
}
```

### Logging Wrapper
```csharp
public class LoggingLLMClient : ILLMClient
{
    private readonly ILLMClient _inner;
    private readonly ILogger _logger;

    public async Task<LLMResponse<string>> GetResponse(...)
    {
        _logger.LogInformation("LLM request: {MessageCount} messages", messages.Count);
        var sw = Stopwatch.StartNew();

        var response = await _inner.GetResponse(...);

        _logger.LogInformation("LLM response: {Tokens} tokens in {Elapsed}ms",
            response.TokenUsage.TotalTokens, sw.ElapsedMilliseconds);

        return response;
    }
}
```

## See Also

- [Hazina.AI.Providers](../../../AI/Hazina.AI.Providers/README.md) - Multi-provider orchestration
- [Hazina.LLMs.OpenAI](../../LLMs.Providers/Hazina.LLMs.OpenAI/README.md) - OpenAI implementation
- [Hazina.LLMs.Anthropic](../../LLMs.Providers/Hazina.LLMs.Anthropic/README.md) - Anthropic implementation
