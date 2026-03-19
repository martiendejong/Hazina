# Provider Switching - Multi-Provider Setup

**Build AI applications that work with OpenAI, Anthropic, Google, or local models**

## What You'll Learn

- How to configure multiple LLM providers
- How to use the same code with different providers
- How to set up automatic failover
- How to select providers based on task requirements
- Cost and latency comparison across providers

## Prerequisites

- .NET 8.0 or higher
- At least one LLM API key (OpenAI, Anthropic, or Google)

## What is Provider Switching?

Hazina's **provider abstraction** (`ILLMClient` interface) allows you to:

1. **Write once, run anywhere**: Same code works with all providers
2. **Zero vendor lock-in**: Switch providers via configuration, not code changes
3. **Automatic failover**: If OpenAI is down, automatically use Claude
4. **Task-based routing**: Use cheap models for simple tasks, powerful models for complex ones

## Running the Example

```bash
# Set API keys for the providers you want to test
export OPENAI_API_KEY=sk-your-key-here
export ANTHROPIC_API_KEY=sk-ant-your-key-here
export GOOGLE_API_KEY=your-key-here

# Run (at least one key must be set)
dotnet run
```

Expected output:
```
=== Provider Switching Example ===

✓ Registered OpenAI (GPT-4)
✓ Registered Anthropic (Claude 3 Opus)
✓ Registered Google (Gemini Pro)

Total providers: 3

Default provider: openai
Fallback chain: openai → claude → gemini

--- Testing Each Provider ---

[OPENAI]
Question: Explain what a Large Language Model is in one sentence.
Answer: A Large Language Model is an AI system trained on vast amounts of text data...

Tokens: 145 (in: 18, out: 127)
Latency: 1250ms
Estimated cost: $0.007620
------------------------------------------------------------

[CLAUDE]
Question: Explain what a Large Language Model is in one sentence.
Answer: A Large Language Model is a neural network trained on massive text datasets...

Tokens: 138 (in: 18, out: 120)
Latency: 980ms
Estimated cost: $0.009270
------------------------------------------------------------

[GEMINI]
Question: Explain what a Large Language Model is in one sentence.
Answer: Large Language Models are AI systems trained on vast text data...

Tokens: 142 (in: 18, out: 124)
Latency: 620ms
Estimated cost: $0.000067
------------------------------------------------------------

✓ Success! You can now switch providers based on your needs.
```

## Code Walkthrough

### 1. Setup Provider Registry

```csharp
using Hazina.LLMs.Registry;

var registry = new LLMProviderRegistry();
```

**What's happening:**
- `LLMProviderRegistry` manages multiple providers
- Handles provider selection and failover
- Central configuration point for all LLM access

### 2. Register Providers

```csharp
using Hazina.LLMs.OpenAI;
using Hazina.LLMs.Anthropic;
using Hazina.LLMs.Gemini;

// OpenAI
var openai = new OpenAIClientWrapper(new OpenAIConfig
{
    ApiKey = openaiKey,
    Model = "gpt-4"
});
registry.Register("openai", openai);

// Anthropic Claude
var claude = new ClaudeClientWrapper(new ClaudeConfig
{
    ApiKey = anthropicKey,
    Model = "claude-3-opus-20240229"
});
registry.Register("claude", claude);

// Google Gemini
var gemini = new GeminiClientWrapper(new GeminiConfig
{
    ApiKey = googleKey,
    Model = "gemini-pro"
});
registry.Register("gemini", gemini);
```

**What's happening:**
- Each provider is configured with its own API key and model
- All providers implement `ILLMClient` (same interface!)
- Registered with friendly names ("openai", "claude", "gemini")

### 3. Configure Failover

```csharp
registry.SetDefaultProvider("openai");
registry.SetFallbackChain(new[] { "openai", "claude", "gemini" });
```

**What's happening:**
- Default provider is tried first
- If it fails (rate limit, downtime, error), automatically try next in chain
- Transparent to your application code

### 4. Use Providers

```csharp
// Get a specific provider
var llm = registry.GetProvider("openai");

// Use the same interface for all providers
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = "Hello!" }
};

var response = await llm.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);
Console.WriteLine(response.Result);
```

**What's happening:**
- Same code works with any provider
- Swap "openai" for "claude" or "gemini" - no other changes needed
- Response format is standardized across all providers

## Provider Comparison

### OpenAI (GPT-4)
**Strengths**:
- Excellent general-purpose performance
- Strong coding capabilities
- Function calling support
- Good balance of speed and quality

**Pricing**: ~$0.03/1K input tokens, $0.06/1K output tokens

**Best for**: General-purpose tasks, code generation, balanced use cases

### Anthropic (Claude 3 Opus)
**Strengths**:
- Best-in-class reasoning capabilities
- Long context windows (200K tokens)
- Excellent instruction following
- Strong at complex analytical tasks

**Pricing**: ~$0.015/1K input tokens, $0.075/1K output tokens

**Best for**: Complex reasoning, long documents, research tasks

### Google (Gemini Pro)
**Strengths**:
- Very cost-effective
- Fast response times
- Good for simple tasks
- Multimodal capabilities

**Pricing**: ~$0.00025/1K input tokens, $0.0005/1K output tokens (40x cheaper than GPT-4!)

**Best for**: High-volume simple tasks, cost optimization, translation

### Local Models (Ollama)
**Strengths**:
- Zero API costs (after setup)
- No data leaves your infrastructure
- No rate limits
- Privacy and compliance

**Pricing**: Free (hardware costs only)

**Best for**: High-volume use, privacy-sensitive data, cost elimination

## Task-Based Provider Selection

Smart applications choose providers based on task requirements:

```csharp
// Helper function for routing
string SelectProvider(string taskType)
{
    return taskType switch
    {
        "translation" => "gemini",      // Cheap + fast
        "summarization" => "gemini",    // Cheap + fast
        "code_generation" => "openai",  // Best for code
        "complex_reasoning" => "claude", // Best reasoning
        "general" => "openai",          // Balanced
        _ => "openai"
    };
}

// Use it
var provider = SelectProvider("translation");
var llm = registry.GetProvider(provider);
```

**Cost savings example**:
- 10,000 translations with GPT-4: ~$60
- 10,000 translations with Gemini: ~$1.50
- **Savings: 97.5%** by routing simple tasks to cheaper models

## Automatic Failover

Failover protects against provider outages:

```csharp
registry.SetFallbackChain(new[] { "openai", "claude", "gemini" });

try
{
    var llm = registry.GetDefaultProvider();
    var response = await llm.GetResponse(...);
    // If OpenAI fails, automatically tries Claude, then Gemini
}
catch (Exception ex)
{
    Console.WriteLine("All providers failed!");
}
```

**Real-world scenario**:
1. OpenAI returns 503 (service unavailable)
2. Registry automatically retries with Claude
3. Request succeeds with Claude (user never knows)
4. Your app continues running

## Configuration-Based Provider Selection

For production, use configuration files:

```json
// appsettings.json
{
  "LLMProviders": {
    "Default": "openai",
    "FallbackChain": ["openai", "claude", "gemini"],
    "Providers": {
      "openai": {
        "ApiKey": "sk-...",
        "Model": "gpt-4"
      },
      "claude": {
        "ApiKey": "sk-ant-...",
        "Model": "claude-3-opus-20240229"
      }
    }
  }
}
```

Load from configuration:
```csharp
var config = builder.Configuration.GetSection("LLMProviders");
// Setup registry from config
```

**Benefits**:
- No code changes to switch providers
- Different providers per environment (dev vs prod)
- Easy A/B testing

## Cost Tracking

Track costs across providers:

```csharp
decimal totalCost = 0;

var response = await llm.GetResponse(...);

// Calculate cost based on provider
decimal cost = CalculateCost(providerName, response.TokenUsage);
totalCost += cost;

Console.WriteLine($"Total spent: ${totalCost:F4}");
```

## Troubleshooting

### "Provider not found" Error

**Problem**: Provider name not registered.

**Solution**:
```csharp
// Check available providers
var availableProviders = registry.GetRegisteredProviders();
Console.WriteLine($"Available: {string.Join(", ", availableProviders)}");
```

### Different Response Quality

**Problem**: Providers return different quality responses.

**Explanation**: This is expected! Each provider has different strengths:
- Claude: Better at reasoning
- GPT-4: Better at code
- Gemini: Faster but less nuanced

**Solution**: Choose provider based on task requirements.

### High Latency

**Problem**: Slow response times.

**Solutions**:
- Use faster models (GPT-3.5 instead of GPT-4)
- Use Gemini for simple tasks (fastest)
- Use local Ollama models (no network latency)
- Enable response streaming for better perceived performance

## Next Steps

- [Custom Tools](../03-custom-tools/) - Add tool calling to your providers
- [Basic RAG](../04-basic-rag/) - Use providers with document retrieval
- [Agent Orchestration](../05-agent-orchestration/) - Multi-agent systems with provider selection
- [Dynamic Provider Selection](../11-dynamic-providers/) - Advanced routing logic

## Full Code

See [Program.cs](Program.cs) for the complete, runnable code.

---

**Congratulations! You now have zero vendor lock-in.**

Switch providers via configuration, optimize costs with task-based routing, and sleep well knowing failover protects against outages.
