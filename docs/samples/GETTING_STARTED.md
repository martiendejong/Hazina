# Getting Started with Hazina AI Framework

This guide will help you get started with the Hazina AI Framework.

## Installation

Install Hazina packages via NuGet:

```bash
# Core AI functionality
dotnet add package Hazina.AI.Core

# LLM providers
dotnet add package Hazina.LLMs.OpenAI
dotnet add package Hazina.LLMs.Anthropic
dotnet add package Hazina.LLMs.Ollama

# Storage
dotnet add package Hazina.Store.EmbeddingStore

# Observability
dotnet add package Hazina.Observability.Core
```

## Quick Start

### 1. Basic LLM Usage

```csharp
using Hazina.LLMs.OpenAI;
using Hazina.AI.Core;

// Configure OpenAI provider
var provider = new OpenAIProvider(new OpenAIConfig
{
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY"),
    Model = "gpt-4",
    Temperature = 0.7
});

// Send a simple prompt
var response = await provider.CompletionAsync(
    "Explain quantum computing in simple terms.",
    cancellationToken: CancellationToken.None
);

Console.WriteLine(response.Text);
```

### 2. Streaming Responses

```csharp
await foreach (var chunk in provider.StreamCompletionAsync(
    "Write a haiku about AI",
    cancellationToken: CancellationToken.None))
{
    Console.Write(chunk.Delta);
}
```

### 3. Using Embeddings

```csharp
using Hazina.Store.EmbeddingStore;

// Create embedding store
var store = new EmbeddingJsonFileStore(new EmbeddingStoreConfig
{
    StorePath = "./embeddings",
    Dimensions = 1536, // OpenAI ada-002 dimensions
    DistanceMetric = DistanceMetric.Cosine
});

await store.InitializeAsync(CancellationToken.None);

// Add documents
var embedding = await provider.GetEmbeddingAsync("Machine learning is fascinating");

await store.AddAsync(new EmbeddingEntry
{
    Id = "doc1",
    Embedding = embedding,
    Metadata = new Dictionary<string, object>
    {
        ["text"] = "Machine learning is fascinating",
        ["category"] = "ai"
    }
}, CancellationToken.None);

// Search similar documents
var results = await store.SearchAsync(
    queryEmbedding: embedding,
    topK: 5,
    cancellationToken: CancellationToken.None
);

foreach (var result in results)
{
    Console.WriteLine($"Score: {result.Score}, Text: {result.Metadata["text"]}");
}
```

### 4. RAG (Retrieval-Augmented Generation)

```csharp
using Hazina.AI.RAG;

var ragPipeline = new RAGPipeline(
    llmProvider: provider,
    embeddingStore: store,
    config: new RAGConfig
    {
        TopK = 3,
        MinimumSimilarity = 0.7,
        ContextTemplate = "Context:\n{context}\n\nQuestion: {question}\n\nAnswer:"
    }
);

var answer = await ragPipeline.QueryAsync(
    "What is machine learning?",
    cancellationToken: CancellationToken.None
);

Console.WriteLine(answer.Response);
Console.WriteLine($"Sources: {string.Join(", ", answer.Sources)}");
```

### 5. Agent Workflows

```csharp
using Hazina.AI.Agents;
using Hazina.AI.Workflows;

// Define an agent
var researchAgent = new Agent
{
    Name = "Researcher",
    SystemPrompt = "You are a helpful research assistant.",
    Provider = provider,
    Tools = new[]
    {
        new WebSearchTool(),
        new CalculatorTool()
    }
};

// Create workflow
var workflow = new SequentialWorkflow();
workflow.AddStep(researchAgent, "Research the topic");
workflow.AddStep(researchAgent, "Summarize findings");

// Execute
var result = await workflow.ExecuteAsync(
    input: "Research recent advances in quantum computing",
    cancellationToken: CancellationToken.None
);

Console.WriteLine(result.Output);
```

### 6. Multi-Provider Setup

```csharp
using Hazina.AI.Providers;

var orchestrator = new ProviderOrchestrator();

// Register multiple providers
orchestrator.Register("openai", new OpenAIProvider(openAIConfig));
orchestrator.Register("anthropic", new AnthropicProvider(anthropicConfig));
orchestrator.Register("ollama", new OllamaProvider(ollamaConfig));

// Use provider selector for automatic routing
var selector = new ProviderSelector(orchestrator);

var selectedProvider = await selector.SelectAsync(
    prompt: "Explain machine learning",
    requirements: new ProviderRequirements
    {
        MinSpeed = ProviderSpeed.Fast,
        MaxCostPerToken = 0.0001m,
        RequiredCapabilities = new[] { "chat", "streaming" }
    },
    cancellationToken: CancellationToken.None
);

var response = await selectedProvider.CompletionAsync("Explain machine learning");
```

### 7. Observability

```csharp
using Hazina.Observability.Core;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Add Hazina observability
services.AddHazinaObservability(options =>
{
    options.EnableMetrics = true;
    options.EnableTracing = true;
    options.LogLevel = LogLevel.Information;
});

// Add LLM logging
services.AddHazinaLLMLogging(options =>
{
    options.LogPrompts = true;
    options.LogResponses = true;
    options.LogTokenUsage = true;
});

var serviceProvider = services.BuildServiceProvider();

// All LLM calls will now be logged and tracked
var trackedProvider = serviceProvider.GetRequiredService<ILLMProvider>();
```

### 8. Cognitive Pipeline

```csharp
using Hazina.AI.CognitivePipeline;

var pipeline = new CognitivePipelineBuilder()
    .AddStage(new PromptEngineeringStage())
    .AddStage(new ContextAugmentationStage(store))
    .AddStage(new LLMInferenceStage(provider))
    .AddStage(new ResponseValidationStage())
    .AddStage(new PostProcessingStage())
    .Build();

var result = await pipeline.ExecuteAsync(
    input: new PipelineInput
    {
        Query = "What is the capital of France?",
        Context = new Dictionary<string, object>
        {
            ["language"] = "en",
            ["format"] = "concise"
        }
    },
    cancellationToken: CancellationToken.None
);

Console.WriteLine(result.Response);
```

## Configuration

### appsettings.json

```json
{
  "Hazina": {
    "LLMs": {
      "OpenAI": {
        "ApiKey": "sk-...",
        "Model": "gpt-4",
        "Temperature": 0.7,
        "MaxTokens": 2000
      },
      "Anthropic": {
        "ApiKey": "sk-ant-...",
        "Model": "claude-3-opus-20240229",
        "Temperature": 0.7
      }
    },
    "Storage": {
      "EmbeddingStore": {
        "StorePath": "./data/embeddings",
        "Dimensions": 1536,
        "DistanceMetric": "Cosine"
      }
    },
    "Observability": {
      "EnableMetrics": true,
      "EnableTracing": true,
      "LogLevel": "Information"
    }
  }
}
```

### Dependency Injection

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Hazina.AI.Core;

public class Startup
{
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Register Hazina services
        services.AddHazina(configuration.GetSection("Hazina"));

        // Or configure manually
        services.Configure<OpenAIConfig>(configuration.GetSection("Hazina:LLMs:OpenAI"));
        services.AddSingleton<ILLMProvider, OpenAIProvider>();
        services.AddSingleton<IEmbeddingStore, EmbeddingJsonFileStore>();
    }
}
```

## Error Handling

```csharp
using Hazina.AI.Core;

try
{
    var response = await provider.CompletionAsync("Hello");
}
catch (RateLimitException ex)
{
    Console.WriteLine($"Rate limited. Retry after: {ex.RetryAfter}");
    await Task.Delay(ex.RetryAfter);
    // Retry logic
}
catch (ModelNotFoundException ex)
{
    Console.WriteLine($"Model not found: {ex.ModelName}");
}
catch (LLMException ex)
{
    Console.WriteLine($"LLM error: {ex.Message}");
}
```

## Best Practices

### 1. Use Dependency Injection

```csharp
public class MyService
{
    private readonly ILLMProvider _provider;
    private readonly IEmbeddingStore _store;

    public MyService(ILLMProvider provider, IEmbeddingStore store)
    {
        _provider = provider;
        _store = store;
    }

    public async Task<string> ProcessAsync(string input)
    {
        // Use injected dependencies
        return await _provider.CompletionAsync(input);
    }
}
```

### 2. Handle Cancellation Properly

```csharp
public async Task ProcessWithCancellationAsync(CancellationToken ct)
{
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(30)); // Timeout

    var response = await provider.CompletionAsync(
        "Long-running query",
        cancellationToken: cts.Token
    );
}
```

### 3. Dispose Resources

```csharp
await using var store = new EmbeddingJsonFileStore(config);
await store.InitializeAsync(ct);

// Use store...

// Automatically disposed at end of using block
```

### 4. Use Retry Policies

```csharp
using Polly;

var retryPolicy = Policy
    .Handle<RateLimitException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt))
    );

var response = await retryPolicy.ExecuteAsync(async () =>
    await provider.CompletionAsync("Hello")
);
```

## Next Steps

- Explore [API Reference](../api/index.html)
- Read [Architecture Guide](./ARCHITECTURE.md)
- Check out [Advanced Scenarios](./ADVANCED.md)
- Join our [Community](https://github.com/martiendejong/Hazina/discussions)

## Support

- GitHub Issues: https://github.com/martiendejong/Hazina/issues
- Documentation: https://hazina.dev/docs
- Discord: https://discord.gg/hazina
