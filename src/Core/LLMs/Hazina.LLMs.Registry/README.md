# Hazina.LLMs.Registry

Configurable provider registry for LLM clients with JSON-based definitions and factory pattern.

## Features

- **Built-in Provider Definitions**: Ships with definitions for OpenAI, Anthropic, Gemini, Ollama, Mistral, HuggingFace, Local LLM, and Semantic Kernel
- **JSON Configuration**: Configure providers via `appsettings.json`
- **Extensible**: Add custom providers or override built-in definitions
- **Factory Pattern**: Create ILLMClient instances from provider IDs
- **Capability Discovery**: Query providers by capabilities (chat, embeddings, vision, etc.)

## Quick Start

### 1. Add Package Reference

```xml
<PackageReference Include="Hazina.LLMs.Registry" Version="1.0.0" />
```

### 2. Configure in appsettings.json

```json
{
  "LLMProviders": {
    "defaultProvider": "openai",
    "configurations": {
      "openai": {
        "apiKey": "sk-...",
        "model": "gpt-4o"
      },
      "anthropic": {
        "apiKey": "sk-ant-...",
        "model": "claude-sonnet-4-20250514"
      },
      "ollama": {
        "endpoint": "http://localhost:11434",
        "model": "llama3.2"
      }
    }
  }
}
```

### 3. Register in Startup

```csharp
services.AddProviderRegistry();
services.AddDefaultLLMClient(); // Adds ILLMClient for default provider
```

### 4. Use the Client

```csharp
public class MyService
{
    private readonly ILLMClient _llm;

    public MyService(ILLMClient llm)
    {
        _llm = llm;
    }

    public async Task<string> Chat(string message)
    {
        var messages = new List<HazinaChatMessage>
        {
            new(HazinaMessageRole.User, message)
        };

        var response = await _llm.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            null, null,
            CancellationToken.None);

        return response.Result;
    }
}
```

## Advanced Configuration

### Add Custom Providers

```json
{
  "LLMProviders": {
    "providers": [
      {
        "id": "azure-openai",
        "name": "Azure OpenAI",
        "clientKind": "openai",
        "endpoint": "https://your-resource.openai.azure.com",
        "requiresApiKey": true,
        "capabilities": {
          "chat": true,
          "streaming": true,
          "embeddings": true,
          "tools": true
        },
        "models": [
          { "id": "gpt-4o", "name": "GPT-4o (Azure)", "isDefault": true }
        ]
      }
    ],
    "configurations": {
      "azure-openai": {
        "apiKey": "your-azure-key",
        "settings": {
          "ApiVersion": "2024-02-15-preview"
        }
      }
    }
  }
}
```

### Multiple Providers with Keyed Services

```csharp
services.AddProviderRegistry();
services.AddKeyedLLMClient("fast", "openai");
services.AddKeyedLLMClient("smart", "anthropic");
services.AddKeyedLLMClient("local", "ollama");

// Usage
public class MyService
{
    public MyService(
        [FromKeyedServices("fast")] ILLMClient fastLlm,
        [FromKeyedServices("smart")] ILLMClient smartLlm)
    {
        // Use different providers for different tasks
    }
}
```

### Register Custom Factory

```csharp
services.AddProviderRegistry(factory =>
{
    factory.RegisterFactory("my-custom-provider", (provider, config) =>
    {
        return new MyCustomLLMClient(config?.ApiKey);
    });
});
```

### Query Providers by Capability

```csharp
var registry = serviceProvider.GetRequiredService<ProviderRegistry>();

// Get all providers with vision capability
var visionProviders = registry.GetProvidersWithCapability("vision");

// Get all local providers
var localProviders = registry.GetLocalProviders();

// Get all configured providers
var configured = registry.GetConfiguredProviders();
```

## Built-in Providers

| ID | Name | Client Kind | Features |
|---|---|---|---|
| `openai` | OpenAI | openai | Chat, Streaming, Embeddings, Images, Vision, TTS, Tools, JSON |
| `anthropic` | Anthropic Claude | anthropic | Chat, Streaming, Vision, Tools, JSON |
| `gemini` | Google Gemini | gemini | Chat, Streaming, Embeddings, Images, Vision, Tools, JSON |
| `ollama` | Ollama (Local) | ollama | Chat, Streaming, Embeddings, Vision, Tools, JSON |
| `mistral` | Mistral AI | mistral | Chat, Streaming, Embeddings, Vision, Tools, JSON |
| `huggingface` | Hugging Face | huggingface | Chat, Streaming, Embeddings, Images, TTS |
| `local-llm` | Local LLM (LLamaSharp) | local-llm | Chat, Streaming, Embeddings, JSON |
| `semantic-kernel` | Semantic Kernel | semantic-kernel | Chat, Streaming, Embeddings, Tools, JSON |

## Provider Definition Schema

```json
{
  "id": "string",           // Unique identifier
  "name": "string",         // Display name
  "clientKind": "string",   // Maps to factory or type resolver
  "configType": "string",   // Fully qualified config type name
  "clientType": "string",   // Fully qualified client type name
  "endpoint": "string",     // Default API endpoint
  "requiresApiKey": true,   // Whether API key is required
  "isLocal": false,         // Whether runs locally
  "capabilities": {
    "chat": true,
    "streaming": true,
    "embeddings": false,
    "imageGeneration": false,
    "vision": false,
    "tts": false,
    "tools": false,
    "jsonMode": false
  },
  "models": [
    {
      "id": "string",
      "name": "string",
      "isDefault": true,
      "contextWindow": 128000,
      "capabilities": ["chat", "vision"],
      "inputCostPerMillion": 2.50,
      "outputCostPerMillion": 10.00
    }
  ]
}
```

## Instance Configuration Schema

```json
{
  "apiKey": "string",       // API key for authentication
  "endpoint": "string",     // Override endpoint URL
  "model": "string",        // Override default model
  "embeddingModel": "string",
  "imageModel": "string",
  "ttsModel": "string",
  "logPath": "string",      // Path for logging
  "enabled": true,          // Whether provider is enabled
  "settings": {             // Provider-specific settings
    "ApiVersion": "2024-02-15"
  }
}
```
