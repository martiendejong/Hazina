# Tutorial 1: Basic Multi-Provider Setup

**Time to Complete:** 10 minutes
**Difficulty:** Beginner
**Prerequisites:** .NET 9.0, Basic C# knowledge

## What You'll Learn

- How to set up ProviderOrchestrator
- How to register multiple LLM providers
- How to configure provider metadata
- How to make your first multi-provider request

## Step 1: Create a New Console Application

```bash
dotnet new console -n HazinaProvidersDemo
cd HazinaProvidersDemo
```

## Step 2: Add References

Add references to the required projects:

```bash
dotnet add reference path/to/Hazina.AI.Providers.csproj
dotnet add reference path/to/Hazina.LLMs.OpenAI.csproj
dotnet add reference path/to/Hazina.LLMs.Anthropic.csproj
```

## Step 3: Create ProviderOrchestrator

Create a new file `Program.cs`:

```csharp
using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;

// Create the orchestrator
var orchestrator = new ProviderOrchestrator();

Console.WriteLine("✅ ProviderOrchestrator created");
```

## Step 4: Register OpenAI Provider

```csharp
// Configure OpenAI
var openaiConfig = new OpenAIConfig
{
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "your-api-key",
    Model = "gpt-4o-mini",
    EmbeddingModel = "text-embedding-3-small"
};

var openaiClient = new OpenAIClientWrapper(openaiConfig);

// Register with metadata
orchestrator.RegisterProvider("openai", openaiClient, new ProviderMetadata
{
    Name = "openai",
    DisplayName = "OpenAI GPT-4o Mini",
    Type = ProviderType.OpenAI,
    Priority = 1, // Highest priority (lower number = higher priority)
    IsEnabled = true,
    Capabilities = new ProviderCapabilities
    {
        SupportsChat = true,
        SupportsStreaming = true,
        SupportsEmbeddings = true,
        SupportsImages = true,
        MaxTokens = 128000,
        MaxContextWindow = 128000
    },
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.00015m,
        OutputCostPer1KTokens = 0.0006m,
        EmbeddingCostPer1KTokens = 0.00002m,
        Currency = "USD"
    }
});

Console.WriteLine("✅ OpenAI provider registered");
```

### Understanding Provider Metadata

| Field | Description |
|-------|-------------|
| `Name` | Unique identifier for the provider |
| `DisplayName` | Human-readable name |
| `Type` | Provider type (OpenAI, Anthropic, etc.) |
| `Priority` | Selection priority (1 = highest) |
| `IsEnabled` | Whether provider is active |
| `Capabilities` | What the provider supports |
| `Pricing` | Cost per 1K tokens |

## Step 5: Register Anthropic as Fallback

```csharp
// Configure Anthropic (Claude)
var claudeConfig = new ClaudeConfig
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "your-api-key",
    Model = "claude-3-5-sonnet-20241022"
};

var claudeClient = new ClaudeClientWrapper(claudeConfig);

// Register with lower priority (fallback)
orchestrator.RegisterProvider("anthropic", claudeClient, new ProviderMetadata
{
    Name = "anthropic",
    DisplayName = "Claude 3.5 Sonnet",
    Type = ProviderType.Anthropic,
    Priority = 2, // Second priority
    IsEnabled = true,
    Capabilities = new ProviderCapabilities
    {
        SupportsChat = true,
        SupportsStreaming = true,
        SupportsEmbeddings = false, // Claude doesn't do embeddings
        SupportsImages = false,
        SupportsVision = true, // But supports vision!
        MaxTokens = 8192,
        MaxContextWindow = 200000
    },
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.003m,
        OutputCostPer1KTokens = 0.015m,
        Currency = "USD"
    }
});

Console.WriteLine("✅ Anthropic provider registered");
```

## Step 6: Configure Selection Strategy

```csharp
// Use priority-based selection (tries highest priority first)
orchestrator.SetDefaultStrategy(SelectionStrategy.Priority);

Console.WriteLine("✅ Selection strategy set to Priority");
```

### Available Strategies

- **Priority**: Use highest priority healthy provider
- **LeastCost**: Use cheapest provider
- **FastestResponse**: Use fastest provider (based on metrics)
- **RoundRobin**: Distribute load evenly
- **Random**: Random selection
- **Specific**: Always use specific provider by name

## Step 7: Make Your First Request

```csharp
// Create a simple chat message
var messages = new List<HazinaChatMessage>
{
    new HazinaChatMessage
    {
        Role = HazinaMessageRole.System,
        Text = "You are a helpful assistant."
    },
    new HazinaChatMessage
    {
        Role = HazinaMessageRole.User,
        Text = "What is 2+2? Answer in one sentence."
    }
};

// Make the request
Console.WriteLine("\n📤 Sending request...");

var response = await orchestrator.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    toolsContext: null,
    images: null,
    cancel: CancellationToken.None
);

Console.WriteLine($"\n📥 Response: {response.Result}");
Console.WriteLine($"💰 Cost: ${response.TokenUsage.TotalCost:F4}");
Console.WriteLine($"🔢 Tokens: {response.TokenUsage.TotalTokens}");
```

### Expected Output

```
✅ ProviderOrchestrator created
✅ OpenAI provider registered
✅ Anthropic provider registered
✅ Selection strategy set to Priority

📤 Sending request...

📥 Response: 2+2 equals 4.
💰 Cost: $0.0002
🔢 Tokens: 23
```

## Step 8: Check Which Provider Was Used

```csharp
// The orchestrator tracks which provider handled each request
var totalCost = orchestrator.GetTotalCost();
var costByProvider = orchestrator.GetCostByProvider();

Console.WriteLine($"\n💰 Total cost: ${totalCost:F4}");
Console.WriteLine("\n📊 Cost by provider:");
foreach (var (provider, cost) in costByProvider)
{
    Console.WriteLine($"  {provider}: ${cost:F4}");
}
```

### Expected Output

```
💰 Total cost: $0.0002

📊 Cost by provider:
  openai: $0.0002
  anthropic: $0.0000
```

This shows that OpenAI (priority 1) handled the request.

## Step 9: Test Automatic Failover

Let's test what happens when the primary provider fails:

```csharp
// Disable OpenAI
Console.WriteLine("\n🔴 Disabling OpenAI provider...");
orchestrator.SetProviderEnabled("openai", false);

// Make another request
messages.Add(new HazinaChatMessage
{
    Role = HazinaMessageRole.User,
    Text = "What is 3+3? Answer in one sentence."
});

var response2 = await orchestrator.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    null,
    null,
    CancellationToken.None
);

Console.WriteLine($"📥 Response: {response2.Result}");

// Check costs again
costByProvider = orchestrator.GetCostByProvider();
Console.WriteLine("\n📊 Cost by provider:");
foreach (var (provider, cost) in costByProvider)
{
    Console.WriteLine($"  {provider}: ${cost:F4}");
}

// Re-enable OpenAI
orchestrator.SetProviderEnabled("openai", true);
```

### Expected Output

```
🔴 Disabling OpenAI provider...
📥 Response: 3+3 equals 6.

📊 Cost by provider:
  openai: $0.0002
  anthropic: $0.0015
```

The orchestrator automatically failed over to Anthropic (priority 2)!

## Complete Code

Here's the complete `Program.cs`:

```csharp
using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;

// Create orchestrator
var orchestrator = new ProviderOrchestrator();

// Register OpenAI
var openaiConfig = new OpenAIConfig
{
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "your-api-key",
    Model = "gpt-4o-mini"
};
var openaiClient = new OpenAIClientWrapper(openaiConfig);

orchestrator.RegisterProvider("openai", openaiClient, new ProviderMetadata
{
    Name = "openai",
    DisplayName = "OpenAI GPT-4o Mini",
    Type = ProviderType.OpenAI,
    Priority = 1,
    Capabilities = new ProviderCapabilities
    {
        SupportsChat = true,
        SupportsStreaming = true,
        MaxTokens = 128000
    },
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.00015m,
        OutputCostPer1KTokens = 0.0006m
    }
});

// Register Anthropic
var claudeConfig = new ClaudeConfig
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? "your-api-key",
    Model = "claude-3-5-sonnet-20241022"
};
var claudeClient = new ClaudeClientWrapper(claudeConfig);

orchestrator.RegisterProvider("anthropic", claudeClient, new ProviderMetadata
{
    Name = "anthropic",
    DisplayName = "Claude 3.5 Sonnet",
    Type = ProviderType.Anthropic,
    Priority = 2,
    Capabilities = new ProviderCapabilities
    {
        SupportsChat = true,
        SupportsStreaming = true,
        MaxContextWindow = 200000
    },
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.003m,
        OutputCostPer1KTokens = 0.015m
    }
});

// Set strategy
orchestrator.SetDefaultStrategy(SelectionStrategy.Priority);

// Make request
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.System, Text = "You are a helpful assistant." },
    new() { Role = HazinaMessageRole.User, Text = "What is 2+2? Answer in one sentence." }
};

var response = await orchestrator.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    null,
    null,
    CancellationToken.None
);

Console.WriteLine($"Response: {response.Result}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");

// Show provider usage
var costByProvider = orchestrator.GetCostByProvider();
foreach (var (provider, cost) in costByProvider)
{
    Console.WriteLine($"{provider}: ${cost:F4}");
}
```

## Run It

```bash
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."
dotnet run
```

## What's Next?

- [Tutorial 2: Automatic Failover](./02-failover.md) - Deep dive into resilience
- [Tutorial 3: Cost Management](./03-cost-management.md) - Control your spending
- [Tutorial 5: Selection Strategies](./05-selection-strategies.md) - Optimize provider choice

## Key Takeaways

✅ **ProviderOrchestrator** is a drop-in replacement for `ILLMClient`
✅ **Multiple providers** give you automatic redundancy
✅ **Priority-based selection** provides automatic failover
✅ **Cost tracking** is automatic and real-time
✅ **Provider metadata** controls selection and capabilities

You're now ready to build resilient, multi-provider AI applications!
