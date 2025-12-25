# Token Usage Tracking - Usage Examples

## Overview

Alle LLM client calls retourneren nu een `LLMResponse<T>` wrapper die zowel het resultaat als token usage informatie bevat.

## Data Structures

### TokenUsageInfo
```csharp
public class TokenUsageInfo
{
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens => InputTokens + OutputTokens;
    public decimal InputCost { get; set; }
    public decimal OutputCost { get; set; }
    public decimal TotalCost => InputCost + OutputCost;
    public string ModelName { get; set; }
}
```

### LLMResponse<T>
```csharp
public class LLMResponse<T>
{
    public T Result { get; set; }
    public TokenUsageInfo TokenUsage { get; set; }
}
```

## Usage Examples

### Basic String Response
```csharp
var response = await generator.GetResponse(
    "Write a hello world program",
    cancellationToken
);

// Access the actual response
Console.WriteLine(response.Result);

// Access token usage
Console.WriteLine($"Tokens used: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
Console.WriteLine($"Model: {response.TokenUsage.ModelName}");
```

### Typed Response
```csharp
var response = await generator.GetResponse<MyCustomResponse>(
    "Generate structured data",
    cancellationToken
);

// Access typed result
var data = response.Result;
Console.WriteLine($"Message: {data?.Message}");

// Track usage
Console.WriteLine($"Input tokens: {response.TokenUsage.InputTokens}");
Console.WriteLine($"Output tokens: {response.TokenUsage.OutputTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

### Multiple Calls - Aggregating Costs
```csharp
var totalUsage = new TokenUsageInfo();

for (int i = 0; i < 5; i++)
{
    var response = await generator.GetResponse($"Request {i}", cancellationToken);

    // Aggregate usage
    totalUsage += response.TokenUsage;

    Console.WriteLine($"Call {i}: {response.TokenUsage.TotalCost:F4}");
}

Console.WriteLine($"\nTotal tokens: {totalUsage.TotalTokens}");
Console.WriteLine($"Total cost: ${totalUsage.TotalCost:F4}");
```

### Stream Response
```csharp
var response = await generator.StreamResponse(
    "Tell me a story",
    cancellationToken,
    chunk => Console.Write(chunk) // Handle each chunk
);

Console.WriteLine($"\n\nTokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

### Image Generation
```csharp
var response = await generator.GetImage(
    "A red sunset over mountains",
    cancellationToken
);

var image = response.Result;
Console.WriteLine($"Image URL: {image.Url}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

### AgentManager Usage
```csharp
var agentManager = new AgentManager(
    storesPath, agentsPath, flowsPath,
    apiKey, logPath
);
await agentManager.LoadStoresAndAgents();

// Send message and get result
var result = await agentManager.SendMessage("Hello", cancellationToken);

// Note: AgentManager wraps the response and returns just the string
// To get token usage, you'd need to call agent.Generator directly:
var agent = agentManager.GetAgent("myAgent");
var response = await agent.Generator.GetResponse<IsReadyResult>(
    "Hello",
    cancellationToken,
    agentManager.History,
    true, true,
    agent.Tools, null
);

Console.WriteLine($"Message: {response.Result.Message}");
Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

## Pricing (as of implementation)

### OpenAI Models (per 1M tokens)
- GPT-4o: $2.50 input / $10.00 output
- GPT-4 Turbo: $10.00 input / $30.00 output
- GPT-4: $30.00 input / $60.00 output
- GPT-3.5 Turbo: $0.50 input / $1.50 output
- O1 Preview: $15.00 input / $60.00 output
- O1 Mini: $3.00 input / $12.00 output
- O3 Mini: $1.10 input / $4.40 output

### Anthropic Models (per 1M tokens)
- Claude 3.5 Sonnet: $3.00 input / $15.00 output
- Claude 3 Opus: $15.00 input / $75.00 output
- Claude 3 Sonnet: $3.00 input / $15.00 output
- Claude 3 Haiku: $0.25 input / $1.25 output

### Image Generation (estimated, per image)
- OpenAI DALL-E: ~$0.04
- HuggingFace Stable Diffusion: ~$0.05

## Benefits

1. **Cost Tracking**: Monitor exact costs per request
2. **Budget Management**: Track spending across multiple calls
3. **Optimization**: Identify expensive operations
4. **Reporting**: Generate usage reports for billing
5. **Transparency**: Users know exactly what each request costs
