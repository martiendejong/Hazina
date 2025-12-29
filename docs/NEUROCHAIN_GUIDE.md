# NeuroChain Guide

## Overview

NeuroChain is Hazina's multi-layer reasoning system that provides higher confidence and better results through parallel and sequential reasoning chains.

## Features

### 1. Multi-Layer Reasoning

Three specialized reasoning layers:
- **Fast Layer**: Quick responses for simple tasks (uses GPT-3.5/Claude Haiku)
- **Deep Layer**: Complex reasoning for harder problems (uses GPT-4/Claude Sonnet)
- **Expert Layer**: Expert-level analysis for critical tasks (uses O1/Claude Opus)

### 2. Self-Improving Failure Analysis

Automatically learns from failures and prevents recurring issues:
```csharp
var failureLearner = new FailureLearningEngine(orchestrator);

// Record a failure
failureLearner.RecordFailure(new FailureRecord
{
    Prompt = "Original question",
    Response = "Incorrect answer",
    ExpectedResponse = "Correct answer",
    Category = FailureCategory.Hallucination,
    Severity = 0.8
});

// Learn patterns (requires 3+ similar failures)
var patterns = await failureLearner.LearnPatternsAsync();

// Get recommendations
var recommendations = failureLearner.GetRecommendations();

// Apply improvements to future requests
var context = new ReasoningContext();
failureLearner.ApplyImprovements(context, "New question");
```

### 3. Adaptive Behavior

Automatically optimizes configuration based on task complexity:
```csharp
var adaptive = new AdaptiveBehaviorEngine(orchestrator);

// Analyze and configure
var config = await adaptive.AnalyzeAndConfigureAsync("Your task here");

// Use recommended configuration
var neurochain = new NeuroChainOrchestrator(orchestrator, config.RecommendedConfig);
var result = await neurochain.ReasonAsync("Your task", config.ReasoningContext);
```

Complexity levels and optimizations:
- **Simple**: Fast layer only, 80% confidence, 30-60% cost savings
- **Moderate**: Fast+Deep parallel, 90% confidence
- **Complex**: All layers parallel, full validation
- **VeryComplex**: Sequential layers, 95% confidence

## Usage Examples

### Basic Multi-Layer Reasoning

```csharp
var orchestrator = new ProviderOrchestrator();
var neurochain = new NeuroChainOrchestrator(orchestrator);

var result = await neurochain.ReasonAsync(
    "Explain quantum entanglement",
    new ReasoningContext
    {
        MinConfidence = 0.85,
        Domain = "Physics"
    }
);

Console.WriteLine($"Answer: {result.FinalAnswer}");
Console.WriteLine($"Confidence: {result.FinalConfidence:P0}");
Console.WriteLine($"Layers used: {string.Join(", ", result.LayerResponses.Select(r => r.LayerName))}");
```

### With Failure Learning

```csharp
var failureLearner = new FailureLearningEngine(orchestrator);
var neurochain = new NeuroChainOrchestrator(orchestrator);

// Use Neurochain
var result = await neurochain.ReasonAsync("Complex question");

// If result is wrong, record failure
if (!ValidateResponse(result.FinalAnswer))
{
    failureLearner.RecordFailure(new FailureRecord
    {
        Prompt = "Complex question",
        Response = result.FinalAnswer,
        Category = FailureCategory.LogicalError
    });

    // Analyze failure
    var analysis = await failureLearner.AnalyzeFailureAsync(failure);
    Console.WriteLine($"Root cause: {analysis.RootCause}");
}

// Later, apply learned improvements
var context = new ReasoningContext();
failureLearner.ApplyImprovements(context, "Similar question");
var improvedResult = await neurochain.ReasonAsync("Similar question", context);
```

### With Adaptive Behavior

```csharp
var adaptive = new AdaptiveBehaviorEngine(orchestrator);

// Automatically configure based on task
var config = await adaptive.AnalyzeAndConfigureAsync(
    "Write a comprehensive research paper on climate change"
);

Console.WriteLine($"Detected complexity: {config.TaskComplexity}");
Console.WriteLine($"Recommended layers: {string.Join(", ", config.RecommendedLayers)}");

// Use adaptive configuration
var neurochain = new NeuroChainOrchestrator(orchestrator, config.RecommendedConfig);
var result = await neurochain.ReasonAsync(
    "Write a comprehensive research paper on climate change",
    config.ReasoningContext
);
```

## Configuration

### NeuroChain Configuration

```csharp
var config = new NeuroChainConfig
{
    EnableParallel = true,           // Run layers in parallel
    MinConfidence = 0.85,            // Minimum acceptable confidence
    MaxLayers = 3,                   // Maximum layers to use
    EarlyStopThreshold = 0.95,       // Stop if confidence exceeds this
    ValidatorStrategy = ValidationStrategy.Consensus,  // Consensus, BestOfN, Sequential
    EnableSelfHealing = true,        // Auto-retry on low confidence
    CrossValidateResults = true      // Validate results across layers
};

var neurochain = new NeuroChainOrchestrator(orchestrator, config);
```

### Layer Selection

```csharp
// Manually select specific layers
var result = await neurochain.ReasonWithLayersAsync(
    "Your question",
    new[] { LayerType.Deep, LayerType.Expert },
    new ReasoningContext()
);

// Or let adaptive behavior choose
var adaptive = new AdaptiveBehaviorEngine(orchestrator);
var config = await adaptive.AnalyzeAndConfigureAsync("Your question");
```

## Best Practices

1. **Start Simple**: Begin with basic reasoning, add complexity only when needed
2. **Monitor Failures**: Record failures to enable learning
3. **Use Adaptive Behavior**: Let the system optimize configuration
4. **Set Appropriate Confidence**: Higher for critical tasks, lower for exploratory
5. **Enable Cross-Validation**: For critical decisions requiring high accuracy

## Performance Optimization

- **Simple tasks**: Use Fast layer only (50-90% cost reduction)
- **Moderate tasks**: Fast + Deep parallel (30-50% cost reduction)
- **Complex tasks**: All layers parallel (maximizes accuracy)
- **Critical tasks**: Sequential layers with full validation

## Error Handling

```csharp
try
{
    var result = await neurochain.ReasonAsync("Question");

    if (!result.Success)
    {
        Console.WriteLine($"Error: {result.Error}");
    }
    else if (result.FinalConfidence < 0.7)
    {
        Console.WriteLine("Low confidence, consider retry or escalation");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected error: {ex.Message}");
}
```

## Monitoring

Track NeuroChain performance:
```csharp
foreach (var layerResponse in result.LayerResponses)
{
    Console.WriteLine($"{layerResponse.LayerName}:");
    Console.WriteLine($"  Confidence: {layerResponse.Confidence:P0}");
    Console.WriteLine($"  Duration: {layerResponse.ResponseTime.TotalSeconds:F2}s");
    Console.WriteLine($"  Cost: ${layerResponse.EstimatedCost:F4}");
}
```
