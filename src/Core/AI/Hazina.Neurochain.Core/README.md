# Hazina.Neurochain.Core

**Multi-Layer Reasoning System (Neurochain/SCP Architecture)**

Implements a sophisticated multi-layer reasoning system that validates results across independent AI layers, achieving production-grade reliability through consensus, cross-validation, and adaptive behavior.

## Overview

The Neurochain architecture uses multiple independent reasoning layers working in parallel to:
- **Reduce hallucinations** through cross-validation
- **Increase confidence** through consensus
- **Detect logical flaws** through independent verification
- **Adapt behavior** based on task complexity

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                  NeuroChain Orchestrator                     │
│                                                              │
│  ┌────────────┐  ┌──────────────┐  ┌─────────────────────┐ │
│  │   Layer 1  │  │   Layer 2    │  │      Layer 3        │ │
│  │    Fast    │  │     Deep     │  │    Verification     │ │
│  │  Reasoning │  │  Reasoning   │  │  Cross-Validation   │ │
│  └────────────┘  └──────────────┘  └─────────────────────┘ │
│         │               │                     │             │
│         └───────────────┴─────────────────────┘             │
│                         │                                   │
│                    ┌────▼────┐                              │
│                    │Consensus│                              │
│                    │ Engine  │                              │
│                    └─────────┘                              │
└─────────────────────────────────────────────────────────────┘
                         │
                    ┌────▼────────┐
                    │Final Answer │
                    │+ Confidence │
                    └─────────────┘
```

## Three-Layer System

### Layer 1: Fast Reasoning
- **Purpose**: Quick initial analysis
- **Model**: GPT-4o-mini, Claude Haiku
- **Speed**: < 2 seconds
- **Cost**: Low ($0.0001-0.001 per request)
- **Features**:
  - Rapid step-by-step reasoning
  - Confidence estimation
  - Basic validation

### Layer 2: Deep Reasoning
- **Purpose**: Thorough, detailed analysis
- **Model**: GPT-4, Claude Opus/Sonnet
- **Speed**: 2-10 seconds
- **Cost**: Medium ($0.001-0.01 per request)
- **Features**:
  - Detailed reasoning chain
  - Explicit assumptions
  - Supporting evidence
  - Weakness identification
  - Contradiction detection

### Layer 3: Verification
- **Purpose**: Cross-validation and consensus
- **Model**: Different from Layers 1&2 for independence
- **Speed**: 2-10 seconds
- **Cost**: Medium ($0.001-0.01 per request)
- **Features**:
  - Cross-layer validation
  - Consensus determination
  - Disagreement analysis
  - Meta-validation

## Quick Start

### Basic Usage

```csharp
using Hazina.Neurochain.Core;
using Hazina.Neurochain.Core.Layers;
using Hazina.AI.Providers.Core;

// Setup provider orchestrator (from Phase 1)
var orchestrator = QuickSetup.SetupWithFailover("sk-...", "sk-ant-...");

// Create Neurochain orchestrator
var neurochain = new NeuroChainOrchestrator();

// Add layers
neurochain.AddLayer(new FastReasoningLayer(orchestrator));
neurochain.AddLayer(new DeepReasoningLayer(orchestrator));
neurochain.AddLayer(new VerificationLayer(orchestrator));

// Execute multi-layer reasoning
var result = await neurochain.ReasonAsync(
    "What is the square root of 256?",
    new ReasoningContext
    {
        MinConfidence = 0.9,
        IncludeReasoning = true
    }
);

Console.WriteLine($"Answer: {result.FinalAnswer}");
Console.WriteLine($"Confidence: {result.FinalConfidence:P0}");
Console.WriteLine($"Layers executed: {result.LayerResults.Count}");
Console.WriteLine($"Total cost: ${result.TotalCost:F6}");
Console.WriteLine($"Total time: {result.TotalDurationMs}ms");
```

### Advanced Configuration

```csharp
var config = new NeuroChainConfig
{
    ParallelExecution = true,           // Run layers in parallel
    EnableCrossValidation = true,       // Validate results across layers
    EnableEarlyStop = true,             // Stop early if high confidence
    EarlyStopConfidenceThreshold = 0.95 // 95% confidence threshold
};

var neurochain = new NeuroChainOrchestrator(config);
```

## Features

### 1. Multi-Layer Reasoning

Execute the same query across multiple independent layers:

```csharp
var result = await neurochain.ReasonAsync("Complex question", context);

// Each layer provides independent reasoning
foreach (var layerResult in result.LayerResults)
{
    Console.WriteLine($"Layer: {layerResult.Provider}");
    Console.WriteLine($"Answer: {layerResult.Response}");
    Console.WriteLine($"Confidence: {layerResult.Confidence:P0}");
    Console.WriteLine($"Steps: {string.Join(" → ", layerResult.ReasoningChain)}");
    Console.WriteLine();
}
```

### 2. Cross-Validation

Automatic validation across layers:

```csharp
if (result.CrossValidation != null)
{
    Console.WriteLine($"Valid: {result.CrossValidation.IsValid}");
    Console.WriteLine($"Consensus: {result.CrossValidation.ConsensusAnswer}");

    Console.WriteLine("\nAgreements:");
    foreach (var agreement in result.CrossValidation.Agreements)
    {
        Console.WriteLine($"  - {agreement}");
    }

    if (result.CrossValidation.Issues.Count > 0)
    {
        Console.WriteLine("\nIssues:");
        foreach (var issue in result.CrossValidation.Issues)
        {
            Console.WriteLine($"  - {issue.Type}: {issue.Description} (Severity: {issue.Severity:P0})");
        }
    }
}
```

### 3. Detailed Reasoning Breakdown

```csharp
var breakdown = result.GetDetailedBreakdown();
Console.WriteLine(breakdown);

// Output:
// Prompt: What is the square root of 256?
// Final Answer: 16
// Confidence: 95%
// Duration: 3420ms
// Cost: $0.002150
//
// Layer 1: auto
//   Answer: 16
//   Confidence: 90%
//   Steps: 3
//   Duration: 1250ms
//   Cost: $0.000450
//
// Layer 2: auto
//   Answer: 16
//   Confidence: 95%
//   Steps: 5
//   Duration: 2100ms
//   Cost: $0.001500
//   Assumptions: The question asks for positive square root
//
// Cross-Validation:
//   Valid: True
//   Confidence: 95%
//   Agreements: Perfect consensus - all layers agree
```

### 4. Ground Truth Validation

```csharp
var context = new ReasoningContext
{
    GroundTruth = new Dictionary<string, string>
    {
        ["capital_france"] = "Paris",
        ["year_wwii_ended"] = "1945"
    },
    MinConfidence = 0.9
};

var result = await neurochain.ReasonAsync("What is the capital of France?", context);

// Automatically validates against ground truth
// Invalid results trigger re-reasoning or lower confidence
```

### 5. Parallel Execution

```csharp
var config = new NeuroChainConfig
{
    ParallelExecution = true  // All layers run simultaneously
};

// Reduces latency from 5s (sequential) to 2s (parallel)
// Useful when layers are independent
```

### 6. Early Stopping

```csharp
var config = new NeuroChainConfig
{
    EnableEarlyStop = true,
    EarlyStopConfidenceThreshold = 0.95,
    MinLayersBeforeEarlyStop = 1
};

// If Fast layer achieves 95% confidence, skip Deep and Verification layers
// Saves cost and time for simple queries
```

## Examples

### Example 1: Mathematical Reasoning

```csharp
var result = await neurochain.ReasonAsync(
    "If a train travels 120 miles in 2 hours, how far will it travel in 5 hours at the same speed?",
    new ReasoningContext { MinConfidence = 0.95 }
);

Console.WriteLine(result.FinalAnswer);  // "300 miles"
Console.WriteLine($"Confidence: {result.FinalConfidence:P0}");  // "Confidence: 100%"

// All layers show reasoning steps:
// Layer 1: Speed = 120/2 = 60 mph → Distance = 60 * 5 = 300 miles
// Layer 2: Detailed calculation with unit analysis
// Layer 3: Cross-validates calculations
```

### Example 2: Factual Question with Validation

```csharp
var context = new ReasoningContext
{
    GroundTruth = new Dictionary<string, string>
    {
        ["president_2021"] = "Joe Biden"
    },
    MinConfidence = 0.9
};

var result = await neurochain.ReasonAsync(
    "Who became US President in 2021?",
    context
);

// If any layer contradicts ground truth, validation fails
// Cross-validation identifies the discrepancy
```

### Example 3: Complex Analysis

```csharp
var result = await neurochain.ReasonAsync(@"
    Analyze the following code and identify potential issues:

    public void ProcessOrder(Order order)
    {
        if (order.Total > 0)
        {
            order.Status = ""Processed"";
            database.Save(order);
        }
    }
", new ReasoningContext
{
    Domain = "Software Engineering - Code Review",
    MaxSteps = 10
});

// Layer 1: Quick scan for obvious issues
// Layer 2: Deep analysis of edge cases, null checks, error handling
// Layer 3: Cross-validates identified issues across layers

Console.WriteLine("Issues identified:");
foreach (var layer in result.LayerResults)
{
    Console.WriteLine($"\n{layer.Provider}:");
    foreach (var step in layer.ReasoningChain)
    {
        Console.WriteLine($"  - {step}");
    }
}
```

### Example 4: Consensus Building

```csharp
var result = await neurochain.ReasonAsync(
    "What is the best programming language for web development in 2025?",
    new ReasoningContext()
);

// Subjective questions show layer disagreement
if (result.CrossValidation != null)
{
    Console.WriteLine($"Consensus: {result.CrossValidation.ConsensusAnswer}");

    Console.WriteLine("\nDisagreements:");
    foreach (var disagreement in result.CrossValidation.Disagreements)
    {
        Console.WriteLine($"  - {disagreement}");
    }

    // Example output:
    // Consensus: "JavaScript/TypeScript"
    // Disagreements:
    //   - Layer 1 suggested Python
    //   - Layer 2 suggested TypeScript
    //   - Layer 3 noted multiple valid options
}
```

### Example 5: Cost Optimization

```csharp
// Option 1: Fast-only for simple queries
var fastOnly = new NeuroChainOrchestrator();
fastOnly.AddLayer(new FastReasoningLayer(orchestrator));

var result1 = await fastOnly.ReasonAsync("What is 2+2?");
// Cost: ~$0.0001, Time: <1s

// Option 2: Early stopping for moderate queries
var earlyStop = new NeuroChainOrchestrator(new NeuroChainConfig
{
    EnableEarlyStop = true,
    EarlyStopConfidenceThreshold = 0.95
});
earlyStop.AddLayer(new FastReasoningLayer(orchestrator));
earlyStop.AddLayer(new DeepReasoningLayer(orchestrator));

var result2 = await earlyStop.ReasonAsync("Simple factual question");
// If fast layer is confident, stops early: Cost: ~$0.0005, Time: <2s
// Otherwise continues to deep layer: Cost: ~$0.005, Time: ~5s

// Option 3: Full validation for critical queries
var fullValidation = new NeuroChainOrchestrator(new NeuroChainConfig
{
    EnableCrossValidation = true,
    ParallelExecution = true
});
fullValidation.AddLayer(new FastReasoningLayer(orchestrator));
fullValidation.AddLayer(new DeepReasoningLayer(orchestrator));
fullValidation.AddLayer(new VerificationLayer(orchestrator));

var result3 = await fullValidation.ReasonAsync("Critical business decision");
// Cost: ~$0.01, Time: ~3s (parallel), Confidence: >95%
```

## API Reference

### NeuroChainOrchestrator

**Methods:**
- `AddLayer(IReasoningLayer)` - Add a reasoning layer
- `ClearLayers()` - Remove all layers
- `ReasonAsync(prompt, context?)` - Execute multi-layer reasoning

### ReasoningContext

**Properties:**
- `History` - Conversation history
- `GroundTruth` - Known facts for validation
- `MinConfidence` - Required confidence threshold (0-1)
- `MaxSteps` - Maximum reasoning steps
- `IncludeReasoning` - Include reasoning chain in output
- `Domain` - Domain-specific context

### NeuroChainConfig

**Properties:**
- `ParallelExecution` - Run layers in parallel (default: false)
- `EnableCrossValidation` - Validate across layers (default: true)
- `EnableEarlyStop` - Stop early if confident (default: false)
- `EarlyStopConfidenceThreshold` - Threshold for early stop (default: 0.95)

### NeuroChainResult

**Properties:**
- `FinalAnswer` - Consensus answer
- `FinalConfidence` - Overall confidence (0-1)
- `LayerResults` - Results from each layer
- `CrossValidation` - Cross-validation results
- `TotalCost` - Total cost across layers
- `TotalDurationMs` - Total execution time
- `EarlyStopped` - Whether early stopping triggered

**Methods:**
- `GetDetailedBreakdown()` - Get formatted breakdown of all reasoning

## Performance Characteristics

| Configuration | Latency | Cost | Confidence | Use Case |
|--------------|---------|------|------------|----------|
| Fast only | <1s | $0.0001 | 70-80% | Simple queries |
| Fast + Early stop | 1-5s | $0.0001-0.005 | 80-95% | Most queries |
| Fast + Deep | 3-7s | $0.005 | 90-95% | Complex queries |
| All layers (sequential) | 5-15s | $0.01 | 95-99% | Critical queries |
| All layers (parallel) | 2-10s | $0.01 | 95-99% | Critical queries (faster) |

## Best Practices

1. **Choose layers based on task criticality**
   - Simple: Fast only
   - Moderate: Fast + Early stop
   - Critical: All layers with cross-validation

2. **Use parallel execution for latency-sensitive tasks**
   - Reduces wall-clock time by ~60%
   - Same cost and quality as sequential

3. **Provide ground truth when available**
   - Significantly improves validation accuracy
   - Detects hallucinations early

4. **Set appropriate confidence thresholds**
   - 0.7-0.8: General use
   - 0.9+: Critical decisions
   - 0.95+: Maximum reliability

5. **Monitor costs**
   - Track `result.TotalCost` per query
   - Use early stopping to optimize
   - Consider fast-only for high-volume simple queries

## Integration with Phase 1

Neurochain builds on Phase 1 components:

```csharp
// Use Phase 1 provider orchestrator
var orchestrator = QuickSetup.SetupWithFailover("sk-...", "sk-ant-...");

// Pass to Neurochain layers
var neurochain = new NeuroChainOrchestrator();
neurochain.AddLayer(new FastReasoningLayer(orchestrator));
neurochain.AddLayer(new DeepReasoningLayer(orchestrator));

// Neurochain automatically uses:
// - Multi-provider abstraction (Phase 1.1)
// - Cost tracking (Phase 1.1)
// - Health monitoring (Phase 1.1)
// - Fault detection (Phase 1.2) - optional per-layer
// - Context management (Phase 1.3) - via ReasoningContext
```

## Error Handling

```csharp
var result = await neurochain.ReasonAsync("Question", context);

if (!result.IsSuccessful)
{
    Console.WriteLine($"Error: {result.Error}");

    // Check individual layer failures
    foreach (var layer in result.LayerResults)
    {
        if (!layer.IsValid)
        {
            Console.WriteLine($"Layer failed: {layer.Provider}");
            foreach (var issue in layer.ValidationIssues)
            {
                Console.WriteLine($"  - {issue}");
            }
        }
    }
}
```

## Changelog

### Version 1.0.0 (2025-12-29)
- Initial release
- Three-layer reasoning system (Fast, Deep, Verification)
- Cross-validation and consensus engine
- Parallel and sequential execution modes
- Early stopping optimization
- Ground truth validation
- Detailed reasoning breakdown

## Related Documentation

- [Phase 1 Implementation Plan](../../../../HAZINA_CV_IMPLEMENTATION_PLAN.md)
- [Hazina.AI.Providers](../Hazina.AI.Providers/README.md) - Multi-provider abstraction
- [Hazina.AI.FaultDetection](../Hazina.AI.FaultDetection/README.md) - Adaptive fault detection
- [Hazina.AI.FluentAPI](../Hazina.AI.FluentAPI/README.md) - Developer-friendly API

## License

See main Hazina project license.
