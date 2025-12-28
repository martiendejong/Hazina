# Hazina.LLMs.Anthropic

Anthropic Claude provider implementation of the `ILLMClient` interface for Claude models (Opus, Sonnet, Haiku).

## Overview

This library provides lightweight Anthropic/Claude integration for the Hazina platform:

- **ClaudeClientWrapper**: Implements `ILLMClient` for Claude 3.5 Sonnet, Claude 3 Opus, Sonnet, and Haiku
- **Direct HTTP API**: Uses HttpClient to call Anthropic Messages API directly
- **Cost Calculation**: Automatic token cost calculation for all Claude models
- **Structured Responses**: JSON response formatting with automatic parsing
- **Streaming**: Chunked streaming emulation (full SSE streaming can be added)
- **Provider Agnostic**: Drop-in replacement for OpenAI in Hazina applications

**Note**: This is a lightweight implementation. Embeddings and image generation are not supported (Claude does not provide these features).

## Quick Start

### Installation

```xml
<ProjectReference Include="..\path\to\Hazina.LLMs.Anthropic\Hazina.LLMs.Anthropic.csproj" />
```

### Basic Usage

```csharp
using Hazina.LLMs.Anthropic;

// Configure client
var config = new AnthropicConfig
{
    ApiKey = "sk-ant-...",
    Model = "claude-3-5-sonnet-latest"
};

var client = new ClaudeClientWrapper(config);

// Chat completion
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.System, "You are a helpful assistant."),
    new(HazinaMessageRole.User, "What is the capital of France?")
};

var response = await client.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    null, null, CancellationToken.None
);

Console.WriteLine(response.Result); // "The capital of France is Paris."
Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

## Key Classes

### ClaudeClientWrapper

**Location**: `ClaudeClientWrapper.cs:6`

Main implementation of `ILLMClient` for Anthropic Claude models.

**Constructor**:
```csharp
public ClaudeClientWrapper(AnthropicConfig config)
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

// Structured JSON response
Task<LLMResponse<TResponse?>> GetResponse<TResponse>(
    List<HazinaChatMessage> messages,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel)
    where TResponse : ChatResponse<TResponse>, new()

// Streaming (chunked emulation)
Task<LLMResponse<string>> GetResponseStream(
    List<HazinaChatMessage> messages,
    Action<string> onChunkReceived,
    HazinaChatResponseFormat responseFormat,
    IToolsContext? toolsContext,
    List<ImageData>? images,
    CancellationToken cancel)

// NOT SUPPORTED: Embeddings
Task<Embedding> GenerateEmbedding(string data)
// Throws NotSupportedException - Claude doesn't provide embeddings

// NOT SUPPORTED: Image generation
Task<LLMResponse<HazinaGeneratedImage>> GetImage(...)
// Throws NotSupportedException - Claude doesn't generate images
```

**Usage Example**:
```csharp
var config = new AnthropicConfig
{
    ApiKey = "sk-ant-...",
    Model = "claude-3-5-sonnet-latest"
};
var client = new ClaudeClientWrapper(config);

// Simple chat
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, "Explain quantum entanglement")
};

var response = await client.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    null, null, CancellationToken.None
);

Console.WriteLine(response.Result);
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F6}");

// Structured response
public class Summary : ChatResponse<Summary>
{
    public string MainPoint { get; set; }
    public List<string> KeyTakeaways { get; set; }

    public override Summary _example => new()
    {
        MainPoint = "Example point",
        KeyTakeaways = new List<string> { "Takeaway 1", "Takeaway 2" }
    };

    public override string _signature => "Summary(MainPoint: string, KeyTakeaways: string[])";
}

var structuredMessages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, "Summarize the theory of relativity")
};

var summaryResponse = await client.GetResponse<Summary>(
    structuredMessages, null, null, CancellationToken.None
);

Console.WriteLine($"Main: {summaryResponse.Result?.MainPoint}");
foreach (var takeaway in summaryResponse.Result?.KeyTakeaways ?? new())
{
    Console.WriteLine($"- {takeaway}");
}

// Chunked streaming
await client.GetResponseStream(
    messages,
    chunk => Console.Write(chunk), // Print 60-char chunks
    HazinaChatResponseFormat.Text,
    null, null, CancellationToken.None
);
```

### AnthropicConfig

**Location**: `AnthropicConfig.cs:3`

Configuration for Anthropic Claude client.

**Properties**:
```csharp
public string ApiKey { get; set; }        // Required: "sk-ant-..."
public string Model { get; set; }         // Default: "claude-3-5-sonnet-latest"
public string Endpoint { get; set; }      // Default: "https://api.anthropic.com"
public string ApiVersion { get; set; }    // Default: "2023-06-01"
```

**Usage Example**:
```csharp
// Basic configuration
var config = new AnthropicConfig
{
    ApiKey = "sk-ant-api03-...",
    Model = "claude-3-5-sonnet-latest"
};

// Use different model
var opusConfig = new AnthropicConfig
{
    ApiKey = "sk-ant-api03-...",
    Model = "claude-3-opus-latest"
};

// Use Haiku for faster/cheaper responses
var haikuConfig = new AnthropicConfig
{
    ApiKey = "sk-ant-api03-...",
    Model = "claude-3-haiku-20240307"
};

// Environment variable
var envConfig = new AnthropicConfig
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "",
    Model = "claude-3-5-sonnet-latest"
};
```

## Supported Models

The client automatically calculates costs for the following Claude models:

| Model | Input Cost (per 1M tokens) | Output Cost (per 1M tokens) | Best For |
|-------|---------------------------|----------------------------|----------|
| **claude-3-5-sonnet-latest** | $3.00 | $15.00 | Balanced performance and cost |
| **claude-3-opus-latest** | $15.00 | $75.00 | Complex tasks, highest quality |
| **claude-3-sonnet-20240229** | $3.00 | $15.00 | General purpose |
| **claude-3-haiku-20240307** | $0.25 | $1.25 | Fast, cost-effective tasks |

**Cost Calculation Example**:
```csharp
var response = await client.GetResponse(messages, ...);

// Automatic cost calculation based on model
Console.WriteLine($"Input tokens: {response.TokenUsage.InputTokens}");
Console.WriteLine($"Output tokens: {response.TokenUsage.OutputTokens}");
Console.WriteLine($"Input cost: ${response.TokenUsage.InputCost:F6}");
Console.WriteLine($"Output cost: ${response.TokenUsage.OutputCost:F6}");
Console.WriteLine($"Total cost: ${response.TokenUsage.TotalCost:F6}");
```

## Usage Examples

### Provider-Agnostic Code

```csharp
// Your code works with any ILLMClient implementation
public async Task<string> GetAIResponse(ILLMClient client, string question)
{
    var messages = new List<HazinaChatMessage>
    {
        new(HazinaMessageRole.User, question)
    };

    var response = await client.GetResponse(
        messages,
        HazinaChatResponseFormat.Text,
        null, null, CancellationToken.None
    );

    return response.Result;
}

// Swap providers easily
var openAI = new OpenAIClientWrapper(openAIConfig);
var claude = new ClaudeClientWrapper(anthropicConfig);

var answer1 = await GetAIResponse(openAI, "What is AI?");
var answer2 = await GetAIResponse(claude, "What is AI?");
```

### Multi-Turn Conversation

```csharp
var client = new ClaudeClientWrapper(config);
var conversation = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.System, "You are a helpful coding assistant."),
    new(HazinaMessageRole.User, "How do I sort a list in C#?")
};

var response1 = await client.GetResponse(conversation, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);
Console.WriteLine($"Claude: {response1.Result}");

// Continue conversation
conversation.Add(new HazinaChatMessage(HazinaMessageRole.Assistant, response1.Result));
conversation.Add(new HazinaChatMessage(HazinaMessageRole.User, "Can you show an example with LINQ?"));

var response2 = await client.GetResponse(conversation, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);
Console.WriteLine($"Claude: {response2.Result}");

// Calculate total cost
var totalCost = response1.TokenUsage.TotalCost + response2.TokenUsage.TotalCost;
Console.WriteLine($"Conversation cost: ${totalCost:F6}");
```

### Structured Data Extraction

```csharp
public class PersonInfo : ChatResponse<PersonInfo>
{
    public string Name { get; set; }
    public int Age { get; set; }
    public string Occupation { get; set; }

    public override PersonInfo _example => new()
    {
        Name = "John Doe",
        Age = 30,
        Occupation = "Software Engineer"
    };

    public override string _signature => "PersonInfo(Name: string, Age: int, Occupation: string)";
}

var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, "Extract person info: Alice, 28, data scientist")
};

var response = await client.GetResponse<PersonInfo>(messages, null, null, CancellationToken.None);

if (response.Result != null)
{
    Console.WriteLine($"Name: {response.Result.Name}");
    Console.WriteLine($"Age: {response.Result.Age}");
    Console.WriteLine($"Occupation: {response.Result.Occupation}");
}
```

### Streaming Response

```csharp
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, "Write a short poem about coding")
};

Console.Write("Claude: ");
var response = await client.GetResponseStream(
    messages,
    chunk => Console.Write(chunk), // Prints 60-char chunks
    HazinaChatResponseFormat.Text,
    null, null, CancellationToken.None
);

Console.WriteLine($"\n\nTokens: {response.TokenUsage.TotalTokens}");
```

### Cost Comparison Across Models

```csharp
var question = "Explain machine learning in simple terms";
var messages = new List<HazinaChatMessage>
{
    new(HazinaMessageRole.User, question)
};

// Test all models
var models = new[] { "claude-3-haiku-20240307", "claude-3-5-sonnet-latest", "claude-3-opus-latest" };

foreach (var model in models)
{
    var config = new AnthropicConfig { ApiKey = apiKey, Model = model };
    var client = new ClaudeClientWrapper(config);

    var response = await client.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);

    Console.WriteLine($"\n{model}:");
    Console.WriteLine($"Response length: {response.Result.Length} chars");
    Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens}");
    Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F6}");
}
```

## Implementation Details

### Message Mapping

The client automatically maps Hazina messages to Anthropic's format:

- **System messages**: Combined and sent in the `system` parameter
- **User/Assistant messages**: Converted to Anthropic's message array format
- **Tool messages**: Not yet supported in this lightweight implementation

### JSON Response Formatting

For structured responses, the client:
1. Injects a system instruction with the expected JSON schema
2. Calls Claude with this instruction
3. Extracts the JSON response
4. Falls back to extracting JSON from text if needed

### Token Usage Tracking

Token usage is extracted from Anthropic's response and automatically calculated into costs based on the model being used.

### Error Handling

```csharp
try
{
    var response = await client.GetResponse(messages, ...);
}
catch (NotSupportedException ex)
{
    // Embeddings or image generation attempted
    Console.WriteLine($"Feature not supported: {ex.Message}");
}
catch (Exception ex)
{
    // API error
    Console.WriteLine($"Claude API error: {ex.Message}");
}
```

## Limitations

This is a lightweight implementation with the following limitations:

1. **No Embeddings**: Claude doesn't provide an embeddings API - use OpenAI's text-embedding models instead
2. **No Image Generation**: Claude doesn't generate images - use DALL-E or Stable Diffusion
3. **No Tool Calling**: Function calling is not yet implemented (can be added)
4. **Basic Streaming**: Uses chunked emulation instead of true SSE streaming
5. **No Vision**: Image input is not yet supported (Claude does support this via the API)

## Dependencies

- **Hazina.LLMs.Client**: Core LLM abstractions
- **Hazina.LLMs.Classes**: Message and response types
- **System.Text.Json**: JSON serialization
- **System.Net.Http**: Direct HTTP API calls

## Configuration

### Environment Variables

```bash
export ANTHROPIC_API_KEY="sk-ant-api03-..."
```

```csharp
var config = new AnthropicConfig
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? throw new Exception("API key required"),
    Model = "claude-3-5-sonnet-latest"
};
```

### appsettings.json (Custom)

```json
{
  "Anthropic": {
    "ApiKey": "sk-ant-api03-...",
    "Model": "claude-3-5-sonnet-latest",
    "Endpoint": "https://api.anthropic.com",
    "ApiVersion": "2023-06-01"
  }
}
```

## See Also

- [Hazina.LLMs.Client](../../LLMs/Hazina.LLMs.Client/README.md) - Core abstractions
- [Hazina.LLMs.OpenAI](../Hazina.LLMs.OpenAI/README.md) - OpenAI provider
- [Hazina.AgentFactory](../../../Agents/Hazina.AgentFactory/README.md) - Agent framework
- [Anthropic API Documentation](https://docs.anthropic.com/claude/reference/messages_post)
