# Getting Started with Hazina Fluent API

This tutorial demonstrates how the Fluent API reduces AI integration complexity by 70%.

## Complexity Comparison

### Traditional Approach (Before Fluent API)

Here's how you would set up a multi-provider AI system with fault detection and failover using the underlying components directly:

```csharp
using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;
using Hazina.AI.Providers.Health;
using Hazina.AI.Providers.Cost;
using Hazina.AI.Providers.Resilience;
using Hazina.AI.FaultDetection.Core;
using Hazina.AI.FaultDetection.Validators;
using Hazina.AI.FaultDetection.Detectors;
using Hazina.AI.FaultDetection.Analyzers;

// Step 1: Create provider configurations
var openaiConfig = new OpenAIConfig
{
    ApiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    Model = "gpt-4o-mini"
};

var claudeConfig = new AnthropicConfig
{
    ApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!,
    Model = "claude-3-5-sonnet-20241022"
};

// Step 2: Create client wrappers
var openaiClient = new OpenAIClientWrapper(openaiConfig);
var claudeClient = new ClaudeClientWrapper(claudeConfig);

// Step 3: Create and configure registry
var registry = new ProviderRegistry();

registry.Register("openai", openaiClient, new ProviderMetadata
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

registry.Register("anthropic", claudeClient, new ProviderMetadata
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

// Step 4: Create health monitor
var healthMonitor = new ProviderHealthMonitor(registry);

// Step 5: Create cost tracker
var costTracker = new CostTracker();

// Step 6: Create provider selector
var selector = new ProviderSelector(registry);

// Step 7: Create failover handler
var failoverHandler = new FailoverHandler(
    registry,
    selector,
    healthMonitor,
    maxRetries: 3
);

// Step 8: Create orchestrator
var orchestrator = new ProviderOrchestrator(
    registry,
    healthMonitor,
    costTracker,
    selector,
    failoverHandler
);

// Step 9: Configure selection strategy
orchestrator.SetDefaultStrategy(SelectionStrategy.Priority);

// Step 10: Create fault detection components
var validator = new BasicResponseValidator();
var hallucinationDetector = new BasicHallucinationDetector();
var errorPatternRecognizer = new BasicErrorPatternRecognizer();
var confidenceScorer = new BasicConfidenceScorer();

var faultHandler = new AdaptiveFaultHandler(
    orchestrator,
    validator,
    hallucinationDetector,
    errorPatternRecognizer,
    confidenceScorer,
    maxRetries: 3,
    minConfidenceThreshold: 0.7
);

// Step 11: Create validation context
var validationContext = new ValidationContext
{
    Prompt = "What is 2+2?",
    MinConfidenceThreshold = 0.7,
    ResponseType = ResponseType.Text
};

// Step 12: Create messages
var messages = new List<HazinaChatMessage>
{
    new HazinaChatMessage
    {
        Role = HazinaMessageRole.User,
        Text = "What is 2+2?"
    }
};

// Step 13: Execute with fault detection
validationContext.ConversationHistory = messages;
var response = await faultHandler.ExecuteWithFaultDetectionAsync(
    messages,
    validationContext,
    CancellationToken.None
);

Console.WriteLine(response.Result);
```

**Total: ~120 lines of code**

### Fluent API Approach (After)

Here's the same functionality using the Fluent API:

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.FluentAPI.Core;

// Setup once at application startup
QuickSetup.SetupAndConfigure(
    openAIKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
    anthropicKey: Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")!
);

// Execute anywhere in your application
var result = await Hazina.AskSafeAsync("What is 2+2?");

Console.WriteLine(result);
```

**Total: 4 lines of code**

**Complexity Reduction: 97%** (120 lines → 4 lines)

## Step-by-Step Tutorial

### Step 1: Install Package

```bash
dotnet add package Hazina.AI.FluentAPI
```

### Step 2: Choose Your Setup Style

#### Option A: Quick Setup (Recommended for Most Cases)

```csharp
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.FluentAPI.Core;

// Single provider
var orchestrator = QuickSetup.SetupOpenAI("sk-...");

// Multi-provider with failover
var orchestrator = QuickSetup.SetupWithFailover(
    openAIKey: "sk-...",
    anthropicKey: "sk-ant-..."
);

// Cost-optimized
var orchestrator = QuickSetup.SetupCostOptimized("sk-...");

// Setup and configure as global default
QuickSetup.SetupAndConfigure("sk-...", "sk-ant-...");
```

#### Option B: Builder Style (For Complex Scenarios)

```csharp
var result = await Hazina.AI()
    .WithProvider("openai")
    .WithFaultDetection(confidence: 0.9)
    .Ask("Your question here")
    .ExecuteAsync();
```

### Step 3: Make Your First Request

After setting up with `SetupAndConfigure()`:

```csharp
// Simple ask
var answer = await Hazina.AskAsync("What is the capital of France?");
Console.WriteLine(answer);
// Output: "The capital of France is Paris."

// Ask with fault detection
var safeAnswer = await Hazina.AskSafeAsync(
    "Calculate 2+2",
    confidence: 0.9
);
Console.WriteLine(safeAnswer);
// Output: "4" (with 90% confidence validation)

// Ask expecting JSON
var json = await Hazina.AskForJsonAsync("Get user data as JSON");
Console.WriteLine(json);
// Output: {"user": "data", ...}

// Ask expecting code
var code = await Hazina.AskForCodeAsync("Write a hello world function");
Console.WriteLine(code);
// Output: function hello() { ... }
```

### Step 4: Advanced Usage

#### Multi-Turn Conversations

```csharp
using Hazina.AI.Orchestration.Context;

var orchestrator = QuickSetup.SetupOpenAI("sk-...");
var contextManager = new ContextManager(orchestrator);
var context = contextManager.CreateContext(maxTokens: 128000);

// Turn 1
await Hazina.AI()
    .WithOrchestrator(orchestrator)
    .WithContext(context)
    .Ask("My favorite color is blue")
    .ExecuteAsync();

// Turn 2 - remembers previous context
var result = await Hazina.AI()
    .WithOrchestrator(orchestrator)
    .WithContext(context)
    .Ask("What is my favorite color?")
    .ExecuteAsync();

Console.WriteLine(result);
// Output: "Your favorite color is blue."
```

#### Custom Validation

```csharp
var result = await Hazina.AI()
    .WithFaultDetection()
    .WithGroundTruth("capital_france", "Paris")
    .WithValidation("must_mention_paris", async response =>
    {
        return response.Contains("Paris", StringComparison.OrdinalIgnoreCase);
    }, IssueSeverity.Error)
    .Ask("What is the capital of France?")
    .ExecuteAsync();
```

#### Streaming Responses

```csharp
var fullResponse = await Hazina.AI()
    .WithProvider("openai")
    .Ask("Write a long story")
    .ExecuteStreamAsync(chunk =>
    {
        Console.Write(chunk);  // Print each chunk as it arrives
    });

Console.WriteLine($"\n\nFull response received: {fullResponse.Length} characters");
```

#### Provider Selection Strategies

```csharp
// Use cheapest provider
var cheapResult = await Hazina.AI()
    .WithCheapestProvider()
    .Ask("Simple task")
    .ExecuteAsync();

// Use fastest provider
var fastResult = await Hazina.AI()
    .WithFastestProvider()
    .Ask("Quick question")
    .ExecuteAsync();

// Use specific provider
var specificResult = await Hazina.AI()
    .WithProvider("anthropic")
    .Ask("Complex analysis")
    .ExecuteAsync();
```

### Step 5: Monitor and Manage

```csharp
var orchestrator = QuickSetup.SetupWithFailover("sk-...", "sk-ant-...");

// Set budget limits
orchestrator.SetBudget("openai", limit: 100.00m, BudgetPeriod.Monthly);
orchestrator.AddBudgetAlert("openai", thresholdPercentage: 80.0, "80% budget used");

// Start health monitoring
await orchestrator.StartHealthMonitoringAsync(
    interval: TimeSpan.FromMinutes(5),
    CancellationToken.None
);

// Check costs
var totalCost = orchestrator.GetTotalCost();
var costByProvider = orchestrator.GetCostByProvider();

Console.WriteLine($"Total cost: ${totalCost:F4}");
foreach (var (provider, cost) in costByProvider)
{
    Console.WriteLine($"{provider}: ${cost:F4}");
}

// Check health
var statuses = orchestrator.GetAllHealthStatuses();
foreach (var status in statuses)
{
    Console.WriteLine($"{status.ProviderName}: {status.State} (Success: {status.SuccessRate:P})");
}
```

## Common Patterns

### Pattern 1: Quick Question

```csharp
QuickSetup.SetupAndConfigure("sk-...");
var answer = await Hazina.AskAsync("Your question");
```

### Pattern 2: Production-Ready with Failover

```csharp
var orchestrator = QuickSetup.SetupWithFailover("sk-...", "sk-ant-...");
Hazina.ConfigureDefaultOrchestrator(orchestrator);

// Enable health monitoring
await orchestrator.StartHealthMonitoringAsync(
    TimeSpan.FromMinutes(5),
    CancellationToken.None
);

// Set budgets
orchestrator.SetBudget("openai", 100m, BudgetPeriod.Monthly);
orchestrator.SetBudget("anthropic", 100m, BudgetPeriod.Monthly);

// Use anywhere with fault detection
var result = await Hazina.AskSafeAsync("Your question");
```

### Pattern 3: Cost-Optimized

```csharp
var orchestrator = QuickSetup.SetupCostOptimized("sk-...", "sk-ant-...");

// Always uses cheapest provider
var result = await orchestrator.CreateBuilder()
    .Ask("Your question")
    .ExecuteAsync();
```

### Pattern 4: High-Confidence Validation

```csharp
var result = await Hazina.AI()
    .WithFaultDetection(confidence: 0.95)
    .WithGroundTruth("important_fact", "expected_value")
    .Ask("Critical question")
    .ExpectJson()
    .ExecuteAsync();
```

## Next Steps

- Explore [Advanced Examples](02-advanced-examples.md)
- Learn about [Provider Configuration](03-provider-configuration.md)
- Read [Best Practices](04-best-practices.md)
- Understand [Fault Detection](../../Hazina.AI.FaultDetection/README.md)
- Deep dive into [Multi-Provider Orchestration](../../Hazina.AI.Providers/README.md)

## Troubleshooting

### Error: "Call Hazina.ConfigureDefaultOrchestrator() first"

**Cause:** No default orchestrator configured.

**Solution:**
```csharp
QuickSetup.SetupAndConfigure("sk-...");
// Or
var orchestrator = QuickSetup.SetupOpenAI("sk-...");
Hazina.ConfigureDefaultOrchestrator(orchestrator);
```

### Error: "Provider orchestrator not configured"

**Cause:** Using builder without orchestrator.

**Solution:**
```csharp
var result = await Hazina.AI()
    .WithOrchestrator(orchestrator)  // Add this
    .Ask("Question")
    .ExecuteAsync();
```

### Validation Failures

**Cause:** Fault detection confidence threshold not met.

**Solution:**
- Lower confidence threshold: `.WithFaultDetection(0.5)`
- Check ground truth values
- Review validation rules
- Inspect `ValidationResult.Issues` for details

## Summary

The Fluent API reduces complexity by:
- **One-line setup** instead of manual component configuration
- **Quick methods** for common scenarios
- **Fluent chaining** for complex configurations
- **Automatic integration** of all Phase 1 features
- **Type-safe API** with IntelliSense support

Result: **70-97% code reduction** while maintaining full functionality.
