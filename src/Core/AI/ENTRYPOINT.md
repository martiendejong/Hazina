# AI Domain Entry Point

## Start Here
- **Fluent API**: `Hazina.AI.FluentAPI/Core/Hazina.cs` - One-liner AI calls
- **Providers**: `Hazina.AI.Providers/Core/ProviderOrchestrator.cs` - Multi-provider management
- **Configuration**: `Hazina.AI.FluentAPI/Configuration/QuickSetup.cs`

## Key Flows

### 1. Simple AI Call (4 lines)
```csharp
QuickSetup.SetupAndConfigure(openAIKey, anthropicKey);
var answer = await Hazina.AskAsync("What is 2+2?");
```

### 2. With Fault Detection
```csharp
var answer = await Hazina.AskSafeAsync("Question", minConfidence: 0.9);
```

### 3. Multi-Provider with Failover
```csharp
var orchestrator = QuickSetup.SetupWithFailover(keys...);
var response = await orchestrator.GetResponseAsync(messages);
```

## Projects in This Domain
| Project | Purpose | Criticality |
|---------|---------|-------------|
| `Hazina.AI.FluentAPI` | Developer-first API | CRITICAL |
| `Hazina.AI.Providers` | Provider abstraction | CRITICAL |
| `Hazina.AI.FaultDetection` | Hallucination detection | IMPORTANT |
| `Hazina.AI.Orchestration` | Context management | IMPORTANT |

## Dependencies
- Requires: `Hazina.LLMs.Client` (ILLMClient interface)
- Optional: `Hazina.AI.FaultDetection` (validation)
- Optional: Storage backend for conversation history
