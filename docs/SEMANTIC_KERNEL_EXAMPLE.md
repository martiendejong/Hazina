# Semantic Kernel Integration Example

This document demonstrates how to use Semantic Kernel with Hazina agents.

## Configuration

Add the following to your `appsettings.json`:

```json
{
  "OpenAI": {
    "ApiKey": "sk-...",
    "Model": "gpt-4o",
    "ImageModel": "dall-e-3",
    "EmbeddingModel": "text-embedding-ada-002",
    "LogPath": "C:\\projects\\devgptlogs.txt"
  },
  "SemanticKernel": {
    "Provider": "OpenAI",
    "ApiKey": "sk-...",
    "Model": "gpt-4o",
    "EmbeddingModel": "text-embedding-ada-002",
    "ImageModel": "dall-e-3",
    "TtsModel": "tts-1",
    "LogPath": "C:\\projects\\devgptlogs.txt",
    "Temperature": 0.7,
    "MaxTokens": 4096
  }
}
```

### Provider Options

- **OpenAI**: Standard OpenAI API
- **AzureOpenAI**: Azure OpenAI Service (requires `Endpoint` and `DeploymentName`)
- **Anthropic**: Claude API (coming soon)
- **Ollama**: Local Ollama instance (coming soon)

### Azure OpenAI Example

```json
{
  "SemanticKernel": {
    "Provider": "AzureOpenAI",
    "ApiKey": "your-azure-api-key",
    "Endpoint": "https://your-resource.openai.azure.com/",
    "DeploymentName": "gpt-4o",
    "Model": "gpt-4o",
    "EmbeddingModel": "text-embedding-ada-002"
  }
}
```

## Usage Examples

### 1. Create Agent with Semantic Kernel (Code)

```csharp
using Hazina.LLMs;

// Load config from appsettings.json
var config = SemanticKernelConfig.Load();

// Or create manually
var config = new SemanticKernelConfig(
    provider: LLMProvider.OpenAI,
    apiKey: "sk-...",
    model: "gpt-4o",
    embeddingModel: "text-embedding-ada-002"
);

// Create agent factory
var factory = new AgentFactory("sk-...", "logs.txt");

// Create document store
var store = new DocumentStore(
    new EmbeddingFileStore(@"C:\myproject\repo.embed", new SemanticKernelClientWrapper(config)),
    new TextFileStore(@"C:\myproject"),
    new DocumentPartFileStore(@"C:\myproject\repo.parts"),
    new SemanticKernelClientWrapper(config)
);

// Create agent with Semantic Kernel
var agent = await factory.CreateAgentWithSemanticKernel(
    name: "my_agent",
    systemPrompt: "You are a helpful coding assistant.",
    stores: new[] { (store, Write: true) },
    functions: new[] { "git", "dotnet", "npm" },
    agents: new string[] { },
    flows: new string[] { },
    config: config,
    isCoder: false
);

// Use the agent
var response = await agent.Generator.GetResponse("What files are in this project?", CancellationToken.None);
Console.WriteLine(response.Result);
```

### 2. Using AgentManager with Semantic Kernel

```csharp
using Hazina.LLMs;

// Load Semantic Kernel config
var skConfig = SemanticKernelConfig.Load();
var llmClient = new SemanticKernelClientWrapper(skConfig);

// Create AgentManager with custom LLM client
var manager = new AgentManager(
    storesJsonPath: "stores.hazina",
    agentsJsonPath: "agents.hazina",
    flowsJsonPath: "flows.hazina",
    llmClient: llmClient,
    openAIApiKey: skConfig.ApiKey,
    logFilePath: "logs.txt"
);

// Load stores and agents
await manager.LoadStoresAndAgents();

// Send message to agent
var response = await manager.SendMessage(
    "Explain the project structure",
    CancellationToken.None,
    agentName: "devgpt_agent"
);

Console.WriteLine(response);
```

### 3. Multi-Provider Support

```csharp
// Switch between providers easily
var openAIConfig = new SemanticKernelConfig(
    provider: LLMProvider.OpenAI,
    apiKey: "sk-...",
    model: "gpt-4o"
);

var azureConfig = new SemanticKernelConfig(
    provider: LLMProvider.AzureOpenAI,
    apiKey: "azure-key",
    endpoint: "https://your-resource.openai.azure.com/",
    deploymentName: "gpt-4o",
    model: "gpt-4o"
);

// Use the same agent code with different providers
var openAIClient = new SemanticKernelClientWrapper(openAIConfig);
var azureClient = new SemanticKernelClientWrapper(azureConfig);
```

### 4. Custom Plugins (Advanced)

```csharp
using Hazina.LLMs.Plugins;
using Microsoft.SemanticKernel;

// Create a custom plugin
public class MyCustomPlugin
{
    [KernelFunction("custom_function")]
    [Description("Does something custom")]
    public async Task<string> CustomFunction(
        [Description("Input parameter")] string input,
        CancellationToken cancellationToken = default)
    {
        // Your custom logic here
        return $"Processed: {input}";
    }
}

// Register with Semantic Kernel client
var config = SemanticKernelConfig.Load();
var client = new SemanticKernelClientWrapper(config);

// Plugins are automatically registered from IToolsContext
// Or create Semantic Kernel plugins directly
```

## Features

### Automatic Tool Registration

Hazina tools are automatically converted to Semantic Kernel plugins:

- `{store}_list` → DocumentStorePlugin.list
- `{store}_read` → DocumentStorePlugin.read
- `{store}_relevancy` → DocumentStorePlugin.relevancy
- `{store}_write` → DocumentStorePlugin.write
- `git` → DeveloperToolsPlugin.git
- `dotnet` → DeveloperToolsPlugin.dotnet
- `npm` → DeveloperToolsPlugin.npm

### Streaming Support

```csharp
var response = await agent.Generator.StreamResponse(
    message: "Generate code for authentication",
    cancel: CancellationToken.None,
    onChunkReceived: chunk => Console.Write(chunk)
);
```

### Token Usage Tracking

```csharp
var response = await agent.Generator.GetResponse("Hello", CancellationToken.None);

Console.WriteLine($"Input tokens: {response.TokenUsage.InputTokens}");
Console.WriteLine($"Output tokens: {response.TokenUsage.OutputTokens}");
Console.WriteLine($"Total cost: ${response.TokenUsage.TotalCost}");
```

### Typed Responses

```csharp
// Use existing UpdateStoreResponse for safe code modifications
var updateResponse = await agent.Generator.UpdateStore(
    "Add authentication to the API",
    CancellationToken.None
);

// Or create custom response types
public class MyResponse : ChatResponse<MyResponse>
{
    public string Summary { get; set; }
    public List<string> Items { get; set; }

    public override MyResponse _example => new MyResponse
    {
        Summary = "Example summary",
        Items = new List<string> { "item1", "item2" }
    };

    public override string _signature => JsonSerializer.Serialize(this);
}

var typedResponse = await agent.Generator.GetResponse<MyResponse>(
    "Analyze this project",
    CancellationToken.None
);
```

## Migration from OpenAI Wrapper

Existing code using `OpenAIClientWrapper` works without changes:

```csharp
// Old code (still works)
var openAIConfig = new OpenAIConfig("sk-...");
var llmClient = new OpenAIClientWrapper(openAIConfig);

// New code (multi-provider support)
var skConfig = SemanticKernelConfig.FromOpenAI(
    apiKey: "sk-...",
    model: "gpt-4o",
    embeddingModel: "text-embedding-ada-002",
    imageModel: "dall-e-3",
    logPath: "logs.txt"
);
var llmClient = new SemanticKernelClientWrapper(skConfig);

// Both implement ILLMClient - same interface!
```

## Benefits

1. **Multi-Provider**: Switch between OpenAI, Azure, Anthropic, Ollama without code changes
2. **Enterprise Ready**: Azure OpenAI support for compliance requirements
3. **Plugin Ecosystem**: Access to Semantic Kernel's plugin marketplace
4. **Advanced Features**: Planners, memory, orchestration coming in future phases
5. **Backward Compatible**: Existing Hazina code works unchanged
6. **Local LLMs**: Support for Ollama and local models (coming soon)

## Troubleshooting

### "SKEXP0001" Warnings

These are expected - experimental APIs are suppressed in the project file.

### Tool Calls Not Working

Ensure `ToolCallBehavior` is set (automatic in SemanticKernelClientWrapper):

```csharp
// This is handled automatically, but for reference:
var settings = new OpenAIPromptExecutionSettings
{
    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
};
```

### Token Usage Not Tracked

Verify your provider supports usage metadata. Some local models may not return token counts.

## Next Steps

- Explore Phase 6 advanced features (planners, memory)
- Create custom plugins for domain-specific tools
- Integrate with Azure services via Azure OpenAI
- Set up multi-agent orchestration with SK agents
