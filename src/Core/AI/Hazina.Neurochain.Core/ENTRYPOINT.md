# Neurochain Domain Entry Point

## Start Here
- **Orchestrator**: `Core/NeuroChainOrchestrator.cs` - Multi-layer coordination
- **Layers**: `Layers/FastReasoningLayer.cs`, `DeepReasoningLayer.cs`, `VerificationLayer.cs`
- **Configuration**: `Core/NeuroChainConfig.cs`

## Key Flows

### 1. Multi-Layer Reasoning
```csharp
var neurochain = new NeuroChainOrchestrator(config);
neurochain.AddLayer(new FastReasoningLayer(orchestrator));
neurochain.AddLayer(new DeepReasoningLayer(orchestrator));
neurochain.AddLayer(new VerificationLayer(orchestrator));

var result = await neurochain.ReasonAsync("Complex question", context);
// Returns: confidence 95-99%, cross-validated answer
```

### 2. With Ground Truth Validation
```csharp
var context = new ReasoningContext {
    GroundTruth = new Dictionary<string, string> {
        ["capital_france"] = "Paris"
    }
};
var result = await neurochain.ReasonAsync("What is the capital of France?", context);
```

### 3. Adaptive Behavior
```csharp
var engine = new AdaptiveBehaviorEngine();
var config = await engine.AnalyzeAndConfigureAsync(task, neurochain);
// Automatically selects layers based on task complexity
```

## Projects in This Domain
| Project | Purpose | Criticality |
|---------|---------|-------------|
| `Hazina.Neurochain.Core` | Multi-layer reasoning | CRITICAL |

## Sub-Components
- `Core/` - Orchestrator, results, config
- `Layers/` - Fast, Deep, Verification layers
- `Learning/` - Failure analysis, pattern learning
- `Analysis/` - Adaptive behavior

## Performance Modes
| Mode | Latency | Cost | Confidence |
|------|---------|------|------------|
| Fast only | <1s | $0.0001 | 70-80% |
| Fast + Deep | 3-7s | $0.005 | 90-95% |
| All layers | 5-15s | $0.01 | 95-99% |

## Dependencies
- Requires: `Hazina.AI.Providers` (for LLM calls)
- Optional: `Hazina.AI.FaultDetection` (per-layer validation)
