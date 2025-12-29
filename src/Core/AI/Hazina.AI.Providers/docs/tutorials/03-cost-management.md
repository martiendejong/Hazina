# Tutorial 3: Cost Management and Budget Control

**Time to Complete:** 15 minutes
**Difficulty:** Beginner
**Prerequisites:** Tutorial 1 completed

## What You'll Learn

- How to track LLM costs in real-time
- How to set budgets and prevent overspending
- How to set up budget alerts
- How to optimize costs with provider selection

## The Problem

LLM costs can spiral out of control quickly:

```csharp
// Traditional approach - no cost visibility
for (int i = 0; i < 10000; i++)
{
    await client.GetResponse(messages, ...);
    // How much did this cost? You won't know until the bill arrives!
}
```

## The Solution

Hazina.AI.Providers gives you real-time cost tracking and budget controls.

## Step 1: Set Up Cost Tracking

```csharp
using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Cost;

var orchestrator = new ProviderOrchestrator();

// Register providers (as in Tutorial 1)
orchestrator.RegisterProvider("gpt-4o", gpt4oClient, new ProviderMetadata
{
    Name = "gpt-4o",
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.0025m,
        OutputCostPer1KTokens = 0.01m,
        Currency = "USD"
    }
});

orchestrator.RegisterProvider("gpt-4o-mini", gpt4oMiniClient, new ProviderMetadata
{
    Name = "gpt-4o-mini",
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.00015m,  // 16x cheaper!
        OutputCostPer1KTokens = 0.0006m,   // 16x cheaper!
        Currency = "USD"
    }
});
```

## Step 2: Track Costs in Real-Time

```csharp
// Make some requests
for (int i = 0; i < 10; i++)
{
    var response = await orchestrator.GetResponse(messages, ...);

    // Check cost after each request
    var totalCost = orchestrator.GetTotalCost();
    Console.WriteLine($"Request #{i + 1}: Total spent so far: ${totalCost:F4}");
}

// Get final breakdown
var costByProvider = orchestrator.GetCostByProvider();
Console.WriteLine("\n💰 Final Cost Breakdown:");
foreach (var (provider, cost) in costByProvider)
{
    Console.WriteLine($"  {provider}: ${cost:F4}");
}
```

### Example Output

```
Request #1: Total spent so far: $0.0008
Request #2: Total spent so far: $0.0016
Request #3: Total spent so far: $0.0024
...
Request #10: Total spent so far: $0.0080

💰 Final Cost Breakdown:
  gpt-4o: $0.0080
  gpt-4o-mini: $0.0000
```

## Step 3: Set Monthly Budgets

```csharp
// Set a monthly budget for expensive providers
orchestrator.SetBudget("gpt-4o", limit: 100.00m, BudgetPeriod.Monthly);
orchestrator.SetBudget("claude-3-5-opus", limit: 150.00m, BudgetPeriod.Monthly);

// Set a daily budget for testing
orchestrator.SetBudget("gpt-4o-mini", limit: 5.00m, BudgetPeriod.Daily);

Console.WriteLine("✅ Budgets configured");
```

### Budget Periods

```csharp
public enum BudgetPeriod
{
    Daily,   // Resets every 24 hours
    Weekly,  // Resets every 7 days
    Monthly, // Resets every 30 days
    Total    // Never resets (lifetime)
}
```

## Step 4: Add Budget Alerts

```csharp
// Add alerts at different thresholds
orchestrator.AddBudgetAlert("gpt-4o",
    thresholdPercentage: 50.0,
    message: "⚠️  GPT-4o: 50% of monthly budget used");

orchestrator.AddBudgetAlert("gpt-4o",
    thresholdPercentage: 75.0,
    message: "⚠️  GPT-4o: 75% of monthly budget used - consider switching to cheaper model");

orchestrator.AddBudgetAlert("gpt-4o",
    thresholdPercentage: 90.0,
    message: "🚨 GPT-4o: 90% of monthly budget used - URGENT!");

orchestrator.AddBudgetAlert("gpt-4o",
    thresholdPercentage: 95.0,
    message: "🚨 GPT-4o: 95% of monthly budget used - switching to fallback!");

Console.WriteLine("✅ Budget alerts configured");
```

### How Alerts Work

Alerts trigger automatically when budget thresholds are crossed:

```csharp
// The orchestrator checks budgets after each request
await orchestrator.GetResponse(messages, ...);
// If $50 spent out of $100 budget → "50% of monthly budget used" alert fires
```

## Step 5: Monitor Budget Utilization

```csharp
// Check budget status
var budget = orchestrator.GetBudget("gpt-4o");
if (budget != null)
{
    var utilization = orchestrator.GetBudgetUtilization("gpt-4o");
    var currentCost = orchestrator.GetTotalCost("gpt-4o");

    Console.WriteLine($"\n📊 GPT-4o Budget Status:");
    Console.WriteLine($"  Limit: ${budget.Limit:F2}");
    Console.WriteLine($"  Spent: ${currentCost:F2}");
    Console.WriteLine($"  Utilization: {utilization:F1}%");
    Console.WriteLine($"  Remaining: ${(budget.Limit - currentCost):F2}");
}
```

### Example Output

```
📊 GPT-4o Budget Status:
  Limit: $100.00
  Spent: $45.32
  Utilization: 45.3%
  Remaining: $54.68
```

## Step 6: Implement Cost-Aware Logic

```csharp
// Automatically switch to cheaper provider when approaching budget
var gpt4oCost = orchestrator.GetTotalCost("gpt-4o");
var gpt4oBudget = orchestrator.GetBudget("gpt-4o");

if (gpt4oBudget != null && gpt4oCost >= gpt4oBudget.Limit * 0.9m)
{
    Console.WriteLine("⚠️  Approaching GPT-4o budget limit");
    Console.WriteLine("🔄 Switching to GPT-4o-mini");

    // Lower GPT-4o priority, raise GPT-4o-mini priority
    orchestrator.SetProviderPriority("gpt-4o", 10);
    orchestrator.SetProviderPriority("gpt-4o-mini", 1);
}
```

## Step 7: Use LeastCost Strategy

```csharp
// Automatically use cheapest provider
orchestrator.SetDefaultStrategy(SelectionStrategy.LeastCost);

// Now all requests use the cheapest available provider
var response = await orchestrator.GetResponse(messages, ...);
// Will use gpt-4o-mini ($0.00015/1K) instead of gpt-4o ($0.0025/1K)
```

### Cost Comparison

For 1M input tokens:

| Model | Cost | Savings vs GPT-4o |
|-------|------|-------------------|
| GPT-4o | $2.50 | - |
| GPT-4o Mini | $0.15 | **94% cheaper!** |
| Claude 3.5 Haiku | $0.80 | 68% cheaper |
| Claude 3.5 Sonnet | $3.00 | 20% more expensive |

## Step 8: Implement Tiered Cost Strategy

```csharp
// Use cheap models for simple tasks, expensive models for complex tasks
async Task<string> GetResponse(string prompt, bool isComplex)
{
    if (isComplex)
    {
        // Use high-quality model for complex tasks
        var context = new SelectionContext
        {
            SpecificProvider = "gpt-4o"  // Force high-quality model
        };
        orchestrator.SetDefaultContext(context);
    }
    else
    {
        // Use cheapest for simple tasks
        orchestrator.SetDefaultStrategy(SelectionStrategy.LeastCost);
    }

    var messages = new List<HazinaChatMessage>
    {
        new() { Role = HazinaMessageRole.User, Text = prompt }
    };

    var response = await orchestrator.GetResponse(messages, ...);
    return response.Result;
}

// Usage
await GetResponse("What is 2+2?", isComplex: false);  // Uses gpt-4o-mini ($$$)
await GetResponse("Write a complex algorithm", isComplex: true);  // Uses gpt-4o ($$$$)
```

## Step 9: Global Budget Control

```csharp
// Set a global budget across all providers
orchestrator.SetGlobalBudget(limit: 500.00m, BudgetPeriod.Monthly);

// Add global alerts
orchestrator.AddBudgetAlert("__global__", 80.0, "80% of total budget used");
orchestrator.AddBudgetAlert("__global__", 95.0, "95% of total budget used - STOP!");

// Check if global budget exceeded
if (orchestrator.IsBudgetExceeded("__global__"))
{
    Console.WriteLine("🚨 Global budget exceeded - stopping all requests");
    throw new InvalidOperationException("Budget exceeded");
}
```

## Complete Example: Cost-Optimized Application

```csharp
using Hazina.AI.Providers.Core;
using Hazina.AI.Providers.Selection;
using Hazina.AI.Providers.Cost;

var orchestrator = new ProviderOrchestrator();

// Register providers with accurate pricing
orchestrator.RegisterProvider("gpt-4o-mini", gpt4oMiniClient, new ProviderMetadata
{
    Name = "gpt-4o-mini",
    Priority = 1,  // Prefer cheap model
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.00015m,
        OutputCostPer1KTokens = 0.0006m
    }
});

orchestrator.RegisterProvider("gpt-4o", gpt4oClient, new ProviderMetadata
{
    Name = "gpt-4o",
    Priority = 2,  // Use as fallback
    Pricing = new ProviderPricing
    {
        InputCostPer1KTokens = 0.0025m,
        OutputCostPer1KTokens = 0.01m
    }
});

// Set budgets
orchestrator.SetBudget("gpt-4o-mini", 50.00m, BudgetPeriod.Monthly);
orchestrator.SetBudget("gpt-4o", 100.00m, BudgetPeriod.Monthly);
orchestrator.SetGlobalBudget(150.00m, BudgetPeriod.Monthly);

// Add progressive alerts
orchestrator.AddBudgetAlert("__global__", 50.0, "50% of monthly budget");
orchestrator.AddBudgetAlert("__global__", 75.0, "75% of monthly budget");
orchestrator.AddBudgetAlert("__global__", 90.0, "90% - consider optimizations");

// Use cheapest provider by default
orchestrator.SetDefaultStrategy(SelectionStrategy.LeastCost);

// Process requests
for (int i = 0; i < 1000; i++)
{
    // Check budget before request
    if (orchestrator.IsBudgetExceeded("__global__"))
    {
        Console.WriteLine("Budget exceeded - stopping");
        break;
    }

    var response = await orchestrator.GetResponse(messages, ...);

    // Log cost every 100 requests
    if (i % 100 == 0)
    {
        var cost = orchestrator.GetTotalCost();
        var utilization = orchestrator.GetBudgetUtilization("__global__");
        Console.WriteLine($"Requests: {i}, Cost: ${cost:F2}, Budget: {utilization:F1}%");
    }
}

// Final report
Console.WriteLine("\n📊 Final Cost Report:");
Console.WriteLine($"Total Cost: ${orchestrator.GetTotalCost():F2}");

var costByProvider = orchestrator.GetCostByProvider();
foreach (var (provider, cost) in costByProvider.OrderByDescending(c => c.Value))
{
    var percentage = (cost / orchestrator.GetTotalCost()) * 100;
    Console.WriteLine($"  {provider}: ${cost:F2} ({percentage:F1}%)");
}
```

## Best Practices

### 1. Set Realistic Budgets
```csharp
// ❌ Bad: No budget
// Code runs until bill shock

// ✅ Good: Set based on expected usage
orchestrator.SetBudget("gpt-4o", 100.00m, BudgetPeriod.Monthly);
```

### 2. Use Progressive Alerts
```csharp
// ✅ Good: Multiple threshold alerts
orchestrator.AddBudgetAlert("openai", 50.0);  // Early warning
orchestrator.AddBudgetAlert("openai", 75.0);  // Time to optimize
orchestrator.AddBudgetAlert("openai", 90.0);  // Urgent action needed
```

### 3. Default to Cheap Models
```csharp
// ✅ Good: Cheap by default, expensive when needed
orchestrator.SetDefaultStrategy(SelectionStrategy.LeastCost);
```

### 4. Monitor Regularly
```csharp
// ✅ Good: Regular cost checks
if (requestCount % 100 == 0)
{
    var cost = orchestrator.GetTotalCost();
    Console.WriteLine($"Cost so far: ${cost:F2}");
}
```

## Cost Optimization Strategies

### Strategy 1: Model Switching

```csharp
// Start with cheap model
var model = "gpt-4o-mini";

// If response quality is low, retry with better model
if (qualityScore < 0.7)
{
    model = "gpt-4o";
    // Retry request
}
```

### Strategy 2: Request Batching

```csharp
// ❌ Bad: Individual requests
for (int i = 0; i < 100; i++)
{
    await GetResponse($"Question {i}");  // 100 requests
}

// ✅ Good: Batch in single request
var batchedPrompt = string.Join("\n", questions);
await GetResponse(batchedPrompt);  // 1 request
```

### Strategy 3: Caching

```csharp
// Cache expensive responses
var cache = new Dictionary<string, string>();

async Task<string> GetResponseCached(string prompt)
{
    if (cache.ContainsKey(prompt))
    {
        return cache[prompt];  // Free!
    }

    var response = await orchestrator.GetResponse(...);
    cache[prompt] = response.Result;
    return response.Result;
}
```

## Troubleshooting

### Issue: Costs higher than expected

```csharp
// Check cost breakdown
var costs = orchestrator.GetCostByProvider();
foreach (var (provider, cost) in costs.OrderByDescending(c => c.Value))
{
    Console.WriteLine($"{provider}: ${cost:F4}");
}

// Switch to cheaper provider
orchestrator.SetDefaultStrategy(SelectionStrategy.LeastCost);
```

### Issue: Budget alerts not triggering

```csharp
// Alerts are checked automatically, but you can force a check
orchestrator.CheckAlerts();

// Verify budget is set
var budget = orchestrator.GetBudget("openai");
if (budget == null)
{
    Console.WriteLine("No budget set!");
}
```

## Key Takeaways

✅ **Real-time cost tracking** - Know what you're spending instantly
✅ **Budget controls** - Prevent overspending
✅ **Progressive alerts** - Get warnings before limits
✅ **LeastCost strategy** - Automatic cost optimization
✅ **Per-provider budgets** - Fine-grained control

## Next Steps

- [Tutorial 4: Health Monitoring](./04-health-monitoring.md) - Monitor provider status
- [Tutorial 5: Selection Strategies](./05-selection-strategies.md) - Advanced provider selection
- [Tutorial 6: Production Deployment](./06-production.md) - Deploy with confidence

You now have full control over your LLM costs!
