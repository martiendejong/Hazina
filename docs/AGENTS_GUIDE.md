# Agentic Workflows Guide

## Overview

Hazina's agent system provides autonomous AI agents with tool-calling capabilities, workflow orchestration, and multi-agent coordination.

## Core Concepts

### Agent
An autonomous AI entity that can:
- Execute tasks
- Use tools
- Maintain conversation history
- Integrate with NeuroChain for reasoning

### Tool
A capability an agent can use to interact with external systems or perform specific operations.

### Workflow
A multi-step process coordinating multiple agents or operations.

### Coordination
Strategies for multiple agents to work together.

## Quick Start

```csharp
var orchestrator = new ProviderOrchestrator();

// Create an agent
var agent = new Agent(
    "ResearchAssistant",
    "An agent that helps with research tasks",
    orchestrator
);

// Execute a task
var response = await agent.ExecuteAsync("Research the history of AI");

Console.WriteLine(response.Result);
```

## Creating Agents

### Basic Agent

```csharp
var agent = new Agent(
    name: "DataAnalyst",
    description: "Analyzes data and provides insights",
    orchestrator: orchestrator,
    config: new AgentConfig
    {
        UseNeurochain = false,
        MinConfidence = 0.8
    }
);
```

### Agent with NeuroChain

```csharp
var neurochain = new NeuroChainOrchestrator(orchestrator);

var agent = new Agent(
    "ExpertAnalyst",
    "Expert-level analysis with high confidence",
    orchestrator,
    neurochain,
    new AgentConfig
    {
        UseNeurochain = true,
        MinConfidence = 0.95
    }
);
```

## Tool Calling

### Built-in Tools

```csharp
var agent = new Agent("Assistant", "Helpful assistant", orchestrator);

// Register calculator tool
agent.RegisterTool(new CalculatorTool());

// Agent can now use calculator
var response = await agent.ExecuteAsync("What is 123 * 456?");
// Agent will call: TOOL: Calculator(expression=123 * 456)
```

### Custom Tools

```csharp
public class WeatherTool : AgentTool
{
    public WeatherTool()
    {
        Name = "GetWeather";
        Description = "Gets current weather for a location";
        Parameters = new Dictionary<string, ToolParameter>
        {
            ["location"] = new ToolParameter
            {
                Type = "string",
                Description = "City name or coordinates",
                Required = true
            }
        };
    }

    public override async Task<ToolResult> ExecuteAsync(
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        if (!ValidateArguments(arguments, out var error))
        {
            return new ToolResult { Success = false, Error = error };
        }

        var location = arguments["location"].ToString();
        var weather = await FetchWeatherAsync(location);

        return new ToolResult
        {
            Success = true,
            Output = $"Weather in {location}: {weather}"
        };
    }

    private async Task<string> FetchWeatherAsync(string location)
    {
        // Your weather API call here
        return "Sunny, 72°F";
    }
}

// Use the tool
agent.RegisterTool(new WeatherTool());
var response = await agent.ExecuteAsync("What's the weather in New York?");
```

### Multiple Tools

```csharp
var agent = new Agent("Assistant", "Multi-purpose assistant", orchestrator);

agent.RegisterTool(new CalculatorTool());
agent.RegisterTool(new WeatherTool());
agent.RegisterTool(new SearchTool());
agent.RegisterTool(new FileReaderTool());

// Agent will select appropriate tools based on task
var response = await agent.ExecuteAsync(
    "Calculate 50 * 20, then check weather in London"
);
```

## Workflows

### Sequential Workflow

```csharp
var engine = new WorkflowEngine();

// Register agents
engine.RegisterAgent(new Agent("Researcher", "Researches topics", orchestrator));
engine.RegisterAgent(new Agent("Writer", "Writes content", orchestrator));
engine.RegisterAgent(new Agent("Editor", "Edits and polishes", orchestrator));

// Define workflow
var workflow = new Workflow
{
    Name = "ContentCreation",
    Description = "Research, write, and edit content",
    Steps = new List<WorkflowStep>
    {
        new WorkflowStep
        {
            Name = "Research",
            Type = StepType.AgentTask,
            AgentName = "Researcher",
            Task = "Research {topic}",
            OutputKey = "research"
        },
        new WorkflowStep
        {
            Name = "Write",
            Type = StepType.AgentTask,
            AgentName = "Writer",
            Task = "Write article based on: {research}",
            OutputKey = "draft"
        },
        new WorkflowStep
        {
            Name = "Edit",
            Type = StepType.AgentTask,
            AgentName = "Editor",
            Task = "Edit and polish: {draft}",
            OutputKey = "final"
        }
    }
};

// Execute workflow
var result = await engine.ExecuteWorkflowAsync(
    workflow,
    new Dictionary<string, object> { ["topic"] = "AI Safety" }
);

Console.WriteLine($"Final result: {result.FinalContext["final"]}");
```

### Parallel Workflow

```csharp
var workflow = new Workflow
{
    Name = "ParallelResearch",
    Steps = new List<WorkflowStep>
    {
        new WorkflowStep
        {
            Name = "ParallelResearch",
            Type = StepType.Parallel,
            ParallelSteps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Name = "TechnicalResearch",
                    Type = StepType.AgentTask,
                    AgentName = "TechResearcher",
                    Task = "Research technical aspects of {topic}"
                },
                new WorkflowStep
                {
                    Name = "BusinessResearch",
                    Type = StepType.AgentTask,
                    AgentName = "BusinessAnalyst",
                    Task = "Research business implications of {topic}"
                },
                new WorkflowStep
                {
                    Name = "EthicalResearch",
                    Type = StepType.AgentTask,
                    AgentName = "EthicsExpert",
                    Task = "Research ethical considerations of {topic}"
                }
            }
        }
    }
};
```

### Conditional Workflow

```csharp
var workflow = new Workflow
{
    Steps = new List<WorkflowStep>
    {
        new WorkflowStep
        {
            Name = "InitialAnalysis",
            Type = StepType.AgentTask,
            AgentName = "Analyst",
            Task = "Analyze: {data}",
            OutputKey = "analysis"
        },
        new WorkflowStep
        {
            Name = "ConditionalAction",
            Type = StepType.Conditional,
            Condition = new WorkflowCondition
            {
                Variable = "analysis",
                Operator = ConditionOperator.Contains,
                Value = "urgent"
            },
            ThenStep = new WorkflowStep
            {
                Name = "UrgentResponse",
                Type = StepType.AgentTask,
                AgentName = "UrgentHandler",
                Task = "Handle urgent issue: {analysis}"
            },
            ElseStep = new WorkflowStep
            {
                Name = "NormalResponse",
                Type = StepType.AgentTask,
                AgentName = "NormalHandler",
                Task = "Process normally: {analysis}"
            }
        }
    }
};
```

### Loop Workflow

```csharp
var workflow = new Workflow
{
    Steps = new List<WorkflowStep>
    {
        new WorkflowStep
        {
            Name = "IterativeRefinement",
            Type = StepType.Loop,
            MaxIterations = 5,
            LoopCondition = new WorkflowCondition
            {
                Variable = "quality_score",
                Operator = ConditionOperator.Equals,
                Value = "low"
            },
            LoopStep = new WorkflowStep
            {
                Name = "Refine",
                Type = StepType.AgentTask,
                AgentName = "Refiner",
                Task = "Improve the output",
                OutputKey = "quality_score"
            }
        }
    }
};
```

## Multi-Agent Coordination

### Sequential Coordination

Agents work in pipeline - output of one becomes input to next:

```csharp
var coordinator = new MultiAgentCoordinator(
    orchestrator,
    CoordinationStrategy.Sequential
);

coordinator.RegisterAgent(new Agent("Agent1", "First processor", orchestrator));
coordinator.RegisterAgent(new Agent("Agent2", "Second processor", orchestrator));
coordinator.RegisterAgent(new Agent("Agent3", "Final processor", orchestrator));

var result = await coordinator.ExecuteAsync("Process this data");
// Agent1 → Agent2 → Agent3
```

### Parallel Coordination

Agents work independently, results aggregated:

```csharp
var coordinator = new MultiAgentCoordinator(
    orchestrator,
    CoordinationStrategy.Parallel
);

coordinator.RegisterAgent(new Agent("Specialist1", "Domain expert 1", orchestrator));
coordinator.RegisterAgent(new Agent("Specialist2", "Domain expert 2", orchestrator));
coordinator.RegisterAgent(new Agent("Specialist3", "Domain expert 3", orchestrator));

var result = await coordinator.ExecuteAsync("Analyze this problem");
// All agents work simultaneously, answers combined
```

### Debate Coordination

Agents discuss and reach consensus:

```csharp
var coordinator = new MultiAgentCoordinator(
    orchestrator,
    CoordinationStrategy.Debate
);

coordinator.RegisterAgent(new Agent("Optimist", "Optimistic viewpoint", orchestrator));
coordinator.RegisterAgent(new Agent("Pessimist", "Critical viewpoint", orchestrator));
coordinator.RegisterAgent(new Agent("Realist", "Balanced viewpoint", orchestrator));

var result = await coordinator.ExecuteAsync("Should we adopt this technology?");
// Agents debate over 3 rounds, final answer synthesized
```

### Hierarchical Coordination

One agent coordinates others:

```csharp
var coordinator = new MultiAgentCoordinator(
    orchestrator,
    CoordinationStrategy.Hierarchical
);

// First agent is the coordinator
coordinator.RegisterAgent(new Agent("Manager", "Coordinates work", orchestrator));
coordinator.RegisterAgent(new Agent("Worker1", "Executes tasks", orchestrator));
coordinator.RegisterAgent(new Agent("Worker2", "Executes tasks", orchestrator));
coordinator.RegisterAgent(new Agent("Worker3", "Executes tasks", orchestrator));

var result = await coordinator.ExecuteAsync("Complete this project");
// Manager breaks down task, assigns to workers, synthesizes results
```

## Advanced Features

### Conversation History

```csharp
var agent = new Agent("Assistant", "Remembers context", orchestrator);

await agent.ExecuteAsync("My name is John");
await agent.ExecuteAsync("What is my name?");
// Agent remembers previous conversation

// Access history
var history = agent.GetConversationHistory();
foreach (var message in history)
{
    Console.WriteLine($"[{message.Role}] {message.Content}");
}

// Clear history when needed
agent.ClearHistory();
```

### Context Passing

```csharp
var context = new Dictionary<string, object>
{
    ["user_id"] = "12345",
    ["preferences"] = new[] { "detailed", "technical" },
    ["previous_interactions"] = 42
};

var response = await agent.ExecuteAsync("Provide analysis", context);
// Agent has access to context during execution
```

### Error Handling in Workflows

```csharp
var workflow = new Workflow
{
    Steps = new List<WorkflowStep>
    {
        new WorkflowStep
        {
            Name = "RiskyOperation",
            Type = StepType.AgentTask,
            AgentName = "Worker",
            Task = "Do risky operation",
            ContinueOnFailure = true  // Don't stop workflow on failure
        },
        new WorkflowStep
        {
            Name = "CleanupIfNeeded",
            Type = StepType.AgentTask,
            AgentName = "Cleanup",
            Task = "Cleanup if previous failed"
        }
    }
};

var result = await engine.ExecuteWorkflowAsync(workflow);

if (!result.Success)
{
    Console.WriteLine($"Workflow error: {result.Error}");
    foreach (var step in result.StepResults.Where(s => !s.Success))
    {
        Console.WriteLine($"  Failed step: {step.StepName} - {step.Error}");
    }
}
```

## Best Practices

1. **Single Responsibility**: Each agent should have a clear, focused purpose
2. **Tool Granularity**: Create focused tools rather than monolithic ones
3. **Error Handling**: Always handle tool execution failures gracefully
4. **Context Management**: Clear history when starting new conversations
5. **Workflow Design**: Break complex tasks into manageable steps
6. **Coordination Strategy**: Choose based on task nature:
   - Sequential: When order matters
   - Parallel: For independent analyses
   - Debate: For controversial or uncertain decisions
   - Hierarchical: For complex, decomposable tasks

## Example: Complete Agent System

```csharp
// Setup
var orchestrator = new ProviderOrchestrator();
var neurochain = new NeuroChainOrchestrator(orchestrator);

// Create specialized agents
var researcher = new Agent("Researcher", "Researches topics thoroughly", orchestrator);
researcher.RegisterTool(new WebSearchTool());
researcher.RegisterTool(new DatabaseQueryTool());

var analyst = new Agent("Analyst", "Analyzes data", orchestrator, neurochain,
    new AgentConfig { UseNeurochain = true, MinConfidence = 0.9 });
analyst.RegisterTool(new CalculatorTool());
analyst.RegisterTool(new StatisticsTool());

var writer = new Agent("Writer", "Creates polished content", orchestrator);

// Create workflow
var engine = new WorkflowEngine();
engine.RegisterAgent(researcher);
engine.RegisterAgent(analyst);
engine.RegisterAgent(writer);

var workflow = new Workflow
{
    Name = "ResearchReport",
    Steps = new List<WorkflowStep>
    {
        // Parallel research
        new WorkflowStep
        {
            Name = "GatherData",
            Type = StepType.Parallel,
            ParallelSteps = new List<WorkflowStep>
            {
                new WorkflowStep
                {
                    Name = "WebResearch",
                    Type = StepType.AgentTask,
                    AgentName = "Researcher",
                    Task = "Research {topic} from web sources",
                    OutputKey = "web_data"
                },
                new WorkflowStep
                {
                    Name = "DatabaseResearch",
                    Type = StepType.AgentTask,
                    AgentName = "Researcher",
                    Task = "Query database for {topic}",
                    OutputKey = "db_data"
                }
            }
        },
        // Analyze
        new WorkflowStep
        {
            Name = "Analyze",
            Type = StepType.AgentTask,
            AgentName = "Analyst",
            Task = "Analyze: Web: {web_data}, DB: {db_data}",
            OutputKey = "analysis"
        },
        // Write report
        new WorkflowStep
        {
            Name = "WriteReport",
            Type = StepType.AgentTask,
            AgentName = "Writer",
            Task = "Write comprehensive report on {analysis}",
            OutputKey = "report"
        }
    }
};

// Execute
var result = await engine.ExecuteWorkflowAsync(
    workflow,
    new Dictionary<string, object> { ["topic"] = "Climate Change Impact" }
);

Console.WriteLine($"Report: {result.FinalContext["report"]}");
```
