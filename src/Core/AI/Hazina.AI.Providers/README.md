# Hazina.AI.Providers

**Multi-Provider LLM Orchestration with Automatic Failover, Health Monitoring, and Cost Tracking**

Hazina.AI.Providers is a production-grade abstraction layer for managing multiple LLM providers (OpenAI, Anthropic, Google, etc.) with enterprise features like automatic failover, health monitoring, circuit breakers, and cost management.

## Table of Contents

- [Features](#features)
- [Why Use Hazina.AI.Providers?](#why-use-hazinaiproviders)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Tutorials](#tutorials)
- [API Reference](#api-reference)
- [Examples](#examples)

---

## Features

### 🔄 **Multi-Provider Support**
- Seamlessly switch between OpenAI, Anthropic, Google Gemini, Mistral, and more
- Single unified API (implements `ILLMClient`)
- No vendor lock-in

### 🛡️ **Resilience & Reliability**
- **Automatic Failover**: Falls back to alternative providers on failure
- **Circuit Breaker Pattern**: Prevents cascading failures
- **Retry Policies**: Exponential backoff with configurable limits
- **Health Monitoring**: Continuous provider health checks

### 💰 **Cost Management**
- **Real-time Cost Tracking**: Track spending across all providers
- **Budget Alerts**: Get notified before exceeding budgets
- **Cost Optimization**: Automatic selection of cheapest provider

### ⚡ **Provider Selection Strategies**
- **Priority**: Use highest priority healthy provider
- **LeastCost**: Use cheapest provider
- **FastestResponse**: Use fastest provider based on metrics
- **RoundRobin**: Distribute load evenly
- **Random**: Random selection
- **Custom**: Define your own selection logic

### 📊 **Observability**
- Provider health metrics
- Response time tracking
- Success/failure rates
- Cost breakdowns

---

## Why Use Hazina.AI.Providers?

### The Problem
When building AI applications, you typically face these challenges:

1. **Single Provider Risk**: If OpenAI is down, your app is down
2. **Cost Control**: No visibility into LLM spending until the bill arrives
3. **Performance**: Can't easily switch to faster/cheaper providers
4. **Reliability**: No automatic recovery from provider failures

### The Solution
Hazina.AI.Providers solves all of these:

```csharp
// Without Hazina.AI.Providers
var openai = new OpenAIClientWrapper(config);
var response = await openai.GetResponse(messages, ...); // If OpenAI fails, you fail

// With Hazina.AI.Providers
var orchestrator = new ProviderOrchestrator();
orchestrator.RegisterProvider("openai", openaiClient, metadata);
orchestrator.RegisterProvider("anthropic", claudeClient, metadata);
orchestrator.SetDefaultStrategy(SelectionStrategy.Priority); // Auto-failover

var response = await orchestrator.GetResponse(messages, ...); // If OpenAI fails, tries Anthropic
```

---

## Quick Start

### 1. Install Package

```bash
dotnet add reference Hazina.AI.Providers
```

### 2. Basic Setup

```csharp
using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;

// Create orchestrator
var orchestrator = new ProviderOrchestrator();

// Register OpenAI
var openaiClient = new OpenAIClientWrapper(new OpenAIConfig("sk-..."));
orchestrator.RegisterProvider("openai", openaiClient, new ProviderMetadata
{
    Name = "openai",
    DisplayName = "OpenAI GPT-4",
    Type = ProviderType.OpenAI,
    Priority = 1, // Highest priority
    Capabilities = new ProviderCapabilities
    {
        SupportsChat = true,
        SupportsStreaming = true,
        SupportsEmbeddings = true,
        MaxTokens = 128000
    },
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.01m,
        OutputCostPer1KTokens = 0.03m
    }
});

// Register Anthropic as fallback
var claudeClient = new ClaudeClientWrapper(new ClaudeConfig("sk-ant-..."));
orchestrator.RegisterProvider("anthropic", claudeClient, new ProviderMetadata
{
    Name = "anthropic",
    DisplayName = "Claude 3.5 Sonnet",
    Type = ProviderType.Anthropic,
    Priority = 2, // Second priority
    Capabilities = new ProviderCapabilities
    {
        SupportsChat = true,
        SupportsStreaming = true,
        MaxTokens = 200000
    },
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.003m,
        OutputCostPer1KTokens = 0.015m
    }
});

// Set selection strategy
orchestrator.SetDefaultStrategy(SelectionStrategy.Priority);

// Use it just like ILLMClient
var messages = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.User, Text = "Hello!" }
};

var response = await orchestrator.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    null,
    null,
    CancellationToken.None
);

Console.WriteLine(response.Result);
```

### 3. Monitor Costs

```csharp
// Check total cost
var totalCost = orchestrator.GetTotalCost();
Console.WriteLine($"Total spent: ${totalCost:F2}");

// Cost by provider
var costByProvider = orchestrator.GetCostByProvider();
foreach (var (provider, cost) in costByProvider)
{
    Console.WriteLine($"{provider}: ${cost:F2}");
}
```

### 4. Set Budgets

```csharp
// Set budget for OpenAI
orchestrator.SetBudget("openai", 100.00m, BudgetPeriod.Monthly);

// Add alert at 80%
orchestrator.AddBudgetAlert("openai", 80.0, "OpenAI budget 80% consumed");

// Add alert at 95%
orchestrator.AddBudgetAlert("openai", 95.0, "OpenAI budget 95% consumed!");
```

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                   ProviderOrchestrator                       │
│                  (Implements ILLMClient)                     │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Provider   │  │   Health     │  │   Cost       │      │
│  │   Registry   │  │   Monitor    │  │   Tracker    │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Provider   │  │   Failover   │  │   Budget     │      │
│  │   Selector   │  │   Handler    │  │   Manager    │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
└─────────────────────────────────────────────────────────────┘
                            │
            ┌───────────────┼───────────────┐
            ▼               ▼               ▼
       ┌─────────┐    ┌─────────┐    ┌─────────┐
       │ OpenAI  │    │Anthropic│    │ Google  │
       │ Client  │    │ Client  │    │ Client  │
       └─────────┘    └─────────┘    └─────────┘
```

### Components

| Component | Responsibility |
|-----------|---------------|
| **ProviderRegistry** | Manages registered providers and metadata |
| **ProviderHealthMonitor** | Continuous health checks, tracks metrics |
| **ProviderSelector** | Selects best provider based on strategy |
| **FailoverHandler** | Handles automatic failover between providers |
| **CostTracker** | Tracks token usage and costs |
| **BudgetManager** | Manages budgets and alerts |
| **CircuitBreaker** | Prevents cascading failures |
| **RetryPolicy** | Handles retries with exponential backoff |

---

## Tutorials

- [Tutorial 1: Basic Multi-Provider Setup](./docs/tutorials/01-basic-setup.md)
- [Tutorial 2: Automatic Failover](./docs/tutorials/02-failover.md)
- [Tutorial 3: Cost Management](./docs/tutorials/03-cost-management.md)
- [Tutorial 4: Health Monitoring](./docs/tutorials/04-health-monitoring.md)
- [Tutorial 5: Selection Strategies](./docs/tutorials/05-selection-strategies.md)
- [Tutorial 6: Production Deployment](./docs/tutorials/06-production.md)

---

## API Reference

### ProviderOrchestrator

Main class implementing `ILLMClient` with multi-provider support.

#### Methods

```csharp
// Provider Management
void RegisterProvider(string name, ILLMClient client, ProviderMetadata metadata)
void UnregisterProvider(string name)
ILLMClient? GetProvider(string name)
ProviderMetadata? GetProviderMetadata(string name)
void SetProviderEnabled(string name, bool enabled)
void SetProviderPriority(string name, int priority)

// Strategy Configuration
void SetDefaultStrategy(SelectionStrategy strategy)
void SetDefaultContext(SelectionContext context)

// Cost Management
decimal GetTotalCost()
Dictionary<string, decimal> GetCostByProvider()
void SetBudget(string providerName, decimal limit, BudgetPeriod period)
void AddBudgetAlert(string providerName, double thresholdPercentage, string? message = null)

// Health Monitoring
ProviderHealthStatus GetHealthStatus(string name)
IEnumerable<ProviderHealthStatus> GetAllHealthStatuses()
Task StartHealthMonitoringAsync(TimeSpan interval, CancellationToken cancellationToken = default)
void StopHealthMonitoring()

// Resilience
void ResetCircuitBreaker(string providerName)

// ILLMClient Methods (inherited)
Task<Embedding> GenerateEmbedding(string data)
Task<LLMResponse<string>> GetResponse(List<HazinaChatMessage> messages, ...)
Task<LLMResponse<T>> GetResponse<T>(List<HazinaChatMessage> messages, ...)
Task<LLMResponse<string>> GetResponseStream(List<HazinaChatMessage> messages, ...)
Task<LLMResponse<HazinaGeneratedImage>> GetImage(string prompt, ...)
Task SpeakStream(string text, string voice, ...)
```

### SelectionStrategy Enum

```csharp
public enum SelectionStrategy
{
    Priority,          // Use highest priority provider
    LeastCost,         // Use cheapest provider
    FastestResponse,   // Use fastest provider
    RoundRobin,        // Distribute load evenly
    Random,            // Random selection
    Specific,          // Use specific provider by name
    Custom             // Custom selection logic
}
```

### ProviderMetadata

```csharp
public class ProviderMetadata
{
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public ProviderType Type { get; set; }
    public ProviderCapabilities Capabilities { get; set; }
    public ProviderPricing Pricing { get; set; }
    public int Priority { get; set; } // Lower = higher priority
    public bool IsEnabled { get; set; }
}
```

---

## Examples

### Example 1: Cost-Optimized Setup

```csharp
var orchestrator = new ProviderOrchestrator();

// Register multiple providers
orchestrator.RegisterProvider("gpt-4o-mini", gpt4oMiniClient, new ProviderMetadata
{
    Name = "gpt-4o-mini",
    Priority = 1,
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.00015m,  // Cheapest
        OutputCostPer1KTokens = 0.0006m
    }
});

orchestrator.RegisterProvider("claude-3-5-haiku", claudeHaikuClient, new ProviderMetadata
{
    Name = "claude-3-5-haiku",
    Priority = 2,
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.0008m,
        OutputCostPer1KTokens = 0.004m
    }
});

orchestrator.RegisterProvider("gpt-4o", gpt4oClient, new ProviderMetadata
{
    Name = "gpt-4o",
    Priority = 3,
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.0025m,  // Most expensive
        OutputCostPer1KTokens = 0.01m
    }
});

// Use cheapest provider that meets requirements
orchestrator.SetDefaultStrategy(SelectionStrategy.LeastCost);

var response = await orchestrator.GetResponse(messages, ...);
// Will use gpt-4o-mini (cheapest)
```

### Example 2: High Availability with Failover

```csharp
var orchestrator = new ProviderOrchestrator();

// Register providers in priority order
orchestrator.RegisterProvider("primary", primaryClient, new ProviderMetadata
{
    Name = "primary",
    Priority = 1,
    // ... metadata
});

orchestrator.RegisterProvider("secondary", secondaryClient, new ProviderMetadata
{
    Name = "secondary",
    Priority = 2,
    // ... metadata
});

orchestrator.RegisterProvider("tertiary", tertiaryClient, new ProviderMetadata
{
    Name = "tertiary",
    Priority = 3,
    // ... metadata
});

// Start health monitoring
await orchestrator.StartHealthMonitoringAsync(TimeSpan.FromMinutes(5));

// Use priority strategy (automatic failover)
orchestrator.SetDefaultStrategy(SelectionStrategy.Priority);

var response = await orchestrator.GetResponse(messages, ...);
// Tries: primary → secondary → tertiary until one succeeds
```

### Example 3: Budget-Controlled Usage

```csharp
var orchestrator = new ProviderOrchestrator();

// Set monthly budget
orchestrator.SetBudget("openai", 500.00m, BudgetPeriod.Monthly);

// Add progressive alerts
orchestrator.AddBudgetAlert("openai", 50.0, "50% of monthly budget used");
orchestrator.AddBudgetAlert("openai", 75.0, "75% of monthly budget used - consider optimization");
orchestrator.AddBudgetAlert("openai", 90.0, "90% of monthly budget used - WARNING!");

// Use as normal
for (int i = 0; i < 1000; i++)
{
    var response = await orchestrator.GetResponse(messages, ...);

    // Check budget status
    var cost = orchestrator.GetTotalCost("openai");
    if (cost > 450.00m)
    {
        Console.WriteLine("Approaching budget limit, switching to cheaper provider");
        orchestrator.SetProviderPriority("openai", 10); // Lower priority
        orchestrator.SetProviderPriority("gpt-4o-mini", 1); // Higher priority
    }
}
```

### Example 4: Performance Monitoring

```csharp
var orchestrator = new ProviderOrchestrator();

// Start health monitoring
await orchestrator.StartHealthMonitoringAsync(TimeSpan.FromMinutes(1));

// Use fastest provider
orchestrator.SetDefaultStrategy(SelectionStrategy.FastestResponse);

// Monitor performance
while (true)
{
    var response = await orchestrator.GetResponse(messages, ...);

    var statuses = orchestrator.GetAllHealthStatuses();
    foreach (var status in statuses)
    {
        Console.WriteLine($"{status.ProviderName}:");
        Console.WriteLine($"  State: {status.State}");
        Console.WriteLine($"  Response Time: {status.ResponseTime?.TotalMilliseconds}ms");
        Console.WriteLine($"  Success Rate: {status.SuccessRate:P1}");
    }

    await Task.Delay(5000);
}
```

---

## Best Practices

### 1. Always Register Multiple Providers
```csharp
// ❌ Bad: Single provider, no redundancy
orchestrator.RegisterProvider("openai", openaiClient, metadata);

// ✅ Good: Multiple providers for failover
orchestrator.RegisterProvider("openai", openaiClient, metadata1);
orchestrator.RegisterProvider("anthropic", claudeClient, metadata2);
orchestrator.RegisterProvider("google", geminiClient, metadata3);
```

### 2. Set Realistic Budgets
```csharp
// ✅ Set budgets based on actual usage patterns
orchestrator.SetBudget("openai", 1000.00m, BudgetPeriod.Monthly);
orchestrator.AddBudgetAlert("openai", 80.0); // Alert before overspend
```

### 3. Monitor Health in Production
```csharp
// ✅ Always enable health monitoring in production
await orchestrator.StartHealthMonitoringAsync(TimeSpan.FromMinutes(5));
```

### 4. Configure Capabilities Accurately
```csharp
// ✅ Accurate capabilities prevent runtime errors
var metadata = new ProviderMetadata
{
    Capabilities = new ProviderCapabilities
    {
        SupportsChat = true,
        SupportsStreaming = true,
        SupportsEmbeddings = true,  // Only if true!
        SupportsImages = false,      // Don't lie
        MaxTokens = 128000           // Actual limit
    }
};
```

### 5. Use Appropriate Selection Strategy
```csharp
// For cost optimization
orchestrator.SetDefaultStrategy(SelectionStrategy.LeastCost);

// For high availability
orchestrator.SetDefaultStrategy(SelectionStrategy.Priority);

// For load distribution
orchestrator.SetDefaultStrategy(SelectionStrategy.RoundRobin);

// For performance
orchestrator.SetDefaultStrategy(SelectionStrategy.FastestResponse);
```

---

## Troubleshooting

### Issue: All Providers Failing
```csharp
// Check health status
var statuses = orchestrator.GetAllHealthStatuses();
foreach (var status in statuses.Where(s => s.IsUnhealthy))
{
    Console.WriteLine($"{status.ProviderName}: {status.LastError}");
}

// Reset circuit breakers
orchestrator.ResetCircuitBreaker("openai");
orchestrator.ResetCircuitBreaker("anthropic");
```

### Issue: Unexpected Costs
```csharp
// Check cost breakdown
var costs = orchestrator.GetCostByProvider();
foreach (var (provider, cost) in costs.OrderByDescending(c => c.Value))
{
    Console.WriteLine($"{provider}: ${cost:F2}");
}

// Set strict budgets
orchestrator.SetBudget("expensive-provider", 10.00m, BudgetPeriod.Daily);
```

### Issue: Poor Performance
```csharp
// Use FastestResponse strategy
orchestrator.SetDefaultStrategy(SelectionStrategy.FastestResponse);

// Or set response time limits
var context = new SelectionContext
{
    MaxResponseTime = TimeSpan.FromSeconds(5)
};
orchestrator.SetDefaultContext(context);
```

---

## Next Steps

- [Tutorial 1: Basic Setup](./docs/tutorials/01-basic-setup.md) - Start here
- [Tutorial 3: Cost Management](./docs/tutorials/03-cost-management.md) - Control spending
- [Tutorial 6: Production Deployment](./docs/tutorials/06-production.md) - Go live

---

## Contributing

See [CONTRIBUTING.md](../../../../CONTRIBUTING.md) for development guidelines.

## License

Part of the Hazina framework - see main repository for license.
