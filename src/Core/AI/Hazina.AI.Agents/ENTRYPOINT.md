# Agents Domain Entry Point

## Start Here
- **Agent Creation**: `Core/Agent.cs` - Base agent class
- **Tool Framework**: `Tools/AgentTool.cs` - Tool definitions
- **Workflows**: `Workflows/WorkflowEngine.cs` - Multi-step orchestration
- **Multi-Agent**: `Coordination/MultiAgentCoordinator.cs`

## Key Flows

### 1. Simple Agent
```csharp
var agent = new Agent("assistant", orchestrator);
agent.AddTool(new CalculatorTool());
var response = await agent.RunAsync("Calculate 15% of 230");
```

### 2. Workflow Execution
```csharp
var workflow = new Workflow("process-document")
    .AddStep(new AgentTaskStep("analyze", agent, "Analyze: {{input}}"))
    .AddStep(new AgentTaskStep("summarize", agent, "Summarize: {{analyze.output}}"));
var result = await engine.ExecuteAsync(workflow, context);
```

### 3. Multi-Agent Debate
```csharp
var coordinator = new MultiAgentCoordinator();
var result = await coordinator.CoordinateAsync(
    agents, task, MultiAgentStrategy.Debate, maxRounds: 3);
```

## Projects in This Domain
| Project | Purpose | Criticality |
|---------|---------|-------------|
| `Hazina.AI.Agents` | Agent framework | CRITICAL |

## Sub-Components
- `Core/` - Agent base class, context
- `Tools/` - Tool framework, built-in tools
- `Workflows/` - Step types, engine
- `Coordination/` - Multi-agent strategies

## Dependencies
- Requires: `Hazina.AI.Providers` (for LLM calls)
- Optional: `Hazina.Neurochain.Core` (for high-confidence reasoning)
