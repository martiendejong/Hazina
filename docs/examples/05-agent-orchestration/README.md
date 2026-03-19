# Agent Orchestration - Multi-Agent Coordination

**Build systems where specialized AI agents collaborate to solve complex tasks**

## What You'll Learn

- How to create specialized agents with different expertise
- How to coordinate agents in sequential workflows
- How to use a supervisor agent for complex task decomposition
- How to run agents in parallel for faster execution
- Real-world multi-agent patterns

## Prerequisites

- .NET 8.0 or higher
- OpenAI API key
- Understanding of [Hello World](../01-hello-world/) basics

## What is Agent Orchestration?

Instead of one AI doing everything, **multi-agent systems**:

1. **Specialize** - Each agent has specific expertise (research, writing, coding, etc.)
2. **Collaborate** - Agents pass results to each other
3. **Coordinate** - A supervisor agent breaks down complex tasks and assigns work
4. **Parallelize** - Multiple agents work simultaneously

**Result**: Better quality, faster execution, more complex capabilities.

## Running the Example

```bash
# Set your API key
export OPENAI_API_KEY=sk-your-key-here

# Run
dotnet run
```

Expected output:
```
=== Agent Orchestration Example ===

✓ AI client initialized
✓ Created 4 specialized agents:
  - Researcher: Gathers information
  - Writer: Creates content
  - Editor: Reviews and refines
  - Supervisor: Coordinates workflow

--- Example 1: Sequential Workflow ---

Task: Create article about 'What is Retrieval-Augmented Generation (RAG)?'

[Researcher] Gathering information...
Research complete: 856 chars

[Writer] Creating article...
Draft complete: 612 chars

[Editor] Reviewing article...
Editing complete

=== Final Article ===

Retrieval-Augmented Generation (RAG) is an AI technique that combines the power of large
language models with external knowledge retrieval. Unlike traditional models that rely
solely on training data, RAG systems dynamically fetch relevant information from databases,
documents, or APIs before generating responses. This approach significantly improves
accuracy and reduces hallucinations, making AI applications more reliable for real-world
use cases like customer support, documentation search, and research assistance.

✓ Success! Multi-agent orchestration is working.
```

## Code Walkthrough

### 1. Create Specialized Agents

```csharp
var researchAgent = new Agent("Researcher", ai)
{
    SystemPrompt = "You are a research specialist. Your job is to gather comprehensive, " +
                  "accurate information about topics. Provide detailed, well-structured " +
                  "research summaries with key facts and insights."
};

var writerAgent = new Agent("Writer", ai)
{
    SystemPrompt = "You are a professional writer. Your job is to transform research into " +
                  "clear, engaging content. Write in a friendly but professional tone."
};

var editorAgent = new Agent("Editor", ai)
{
    SystemPrompt = "You are a senior editor. Your job is to review content for clarity, " +
                  "accuracy, grammar, and tone. Provide constructive feedback and suggest improvements."
};
```

**What's happening:**
- Each agent is the same underlying AI (GPT-4)
- **System prompts** give each agent different "expertise"
- Agents maintain separate conversation contexts

### 2. Sequential Workflow

```csharp
// Step 1: Research
var researchResult = await researchAgent.ExecuteAsync(
    $"Research this topic: {topic}"
);

// Step 2: Write (using research)
var writeResult = await writerAgent.ExecuteAsync(
    $"Write an article based on this research:\n\n{researchResult}"
);

// Step 3: Edit (refine writing)
var finalResult = await editorAgent.ExecuteAsync(
    $"Review and polish this article:\n\n{writeResult}"
);
```

**What's happening:**
- Agents execute sequentially (one after another)
- Each agent builds on previous agent's output
- Final result has gone through research → writing → editing pipeline

**Pipeline pattern**:
```
User Request
    ↓
[Researcher] → Detailed research
    ↓
[Writer] → Draft article
    ↓
[Editor] → Polished final version
    ↓
User receives result
```

### 3. Supervisor Coordination

```csharp
var supervisorAgent = new Agent("Supervisor", ai)
{
    SystemPrompt = "You are a project supervisor. Coordinate between research, writing, " +
                  "and editing teams. Break down complex tasks, assign work appropriately, " +
                  "synthesize results, and deliver final output."
};

var result = await supervisorAgent.ExecuteAsync(
    $"Task: {complexTask}\n\n" +
    $"You have three teams: Research, Writing, Editing.\n" +
    $"Coordinate them to complete this task."
);
```

**What's happening:**
- Supervisor agent plans the workflow
- Breaks down complex tasks into subtasks
- Coordinates execution across specialist agents
- Synthesizes final output

**Hierarchical pattern**:
```
                [Supervisor]
                     ↓
        ┌────────────┼────────────┐
        ↓            ↓            ↓
  [Researcher]   [Writer]    [Editor]
        ↓            ↓            ↓
     Research → Draft Article → Final Version
                     ↓
                 Supervisor synthesizes
                     ↓
                Final Output
```

### 4. Parallel Execution

```csharp
var topics = new[]
{
    "Benefits of RAG",
    "RAG pitfalls",
    "RAG best practices"
};

// Start all research tasks in parallel
var parallelTasks = topics.Select(async topic =>
{
    return await researchAgent.ExecuteAsync($"Research: {topic}");
}).ToArray();

// Wait for all to complete
var results = await Task.WhenAll(parallelTasks);

// Synthesize results
var synthesized = await writerAgent.ExecuteAsync(
    $"Combine these findings:\n\n{string.Join("\n\n", results)}"
);
```

**What's happening:**
- Multiple agent instances run simultaneously
- Each researches a different topic
- Results are synthesized into coherent output

**Parallel pattern**:
```
                   User Request
                        ↓
        ┌───────────────┼───────────────┐
        ↓               ↓               ↓
  [Research A]    [Research B]    [Research C]
        ↓               ↓               ↓
    Result A        Result B        Result C
        └───────────────┼───────────────┘
                        ↓
                  [Synthesizer]
                        ↓
                  Final Output
```

**Performance benefit**: 3 sequential tasks @ 5s each = 15s total, vs 3 parallel tasks @ 5s each = ~5s total

## Real-World Agent Patterns

### Software Development Team

```csharp
var architectAgent = new Agent("Architect", ai)
{
    SystemPrompt = "You are a software architect. Design system architectures, " +
                  "choose technologies, define interfaces and data models."
};

var developerAgent = new Agent("Developer", ai)
{
    SystemPrompt = "You are a senior developer. Write clean, well-tested code " +
                  "following best practices and design specifications."
};

var testerAgent = new Agent("Tester", ai)
{
    SystemPrompt = "You are a QA engineer. Review code for bugs, edge cases, " +
                  "and write comprehensive test cases."
};

// Workflow: Architecture → Development → Testing
```

### Customer Support System

```csharp
var triageAgent = new Agent("Triage", ai)
{
    SystemPrompt = "You categorize customer inquiries and route them to appropriate specialists."
};

var technicalAgent = new Agent("TechnicalSupport", ai)
{
    SystemPrompt = "You solve technical problems, debug issues, provide solutions."
};

var billingAgent = new Agent("Billing", ai)
{
    SystemPrompt = "You handle billing inquiries, refunds, subscription management."
};

// Triage routes to appropriate specialist
```

### Content Production Pipeline

```csharp
var ideaAgent = new Agent("Ideator", ai)
{
    SystemPrompt = "You brainstorm creative content ideas based on trends and audience interests."
};

var researchAgent = new Agent("Researcher", ai)
{
    SystemPrompt = "You gather facts, statistics, and supporting information."
};

var writerAgent = new Agent("Writer", ai)
{
    SystemPrompt = "You create engaging content from research and ideas."
};

var seoAgent = new Agent("SEO", ai)
{
    SystemPrompt = "You optimize content for search engines and discoverability."
};

// Pipeline: Ideas → Research → Writing → SEO Optimization
```

## Agent Communication Patterns

### 1. Sequential Hand-off

```csharp
var result1 = await agentA.ExecuteAsync(task);
var result2 = await agentB.ExecuteAsync($"Continue from: {result1}");
var result3 = await agentC.ExecuteAsync($"Finalize: {result2}");
```

**Use when**: Clear pipeline, each step builds on previous

### 2. Supervisor Delegation

```csharp
var plan = await supervisor.ExecuteAsync("Plan how to solve this task");
// Supervisor's response indicates which agents to use
var specialized = await specialistAgent.ExecuteAsync(extractedSubtask);
var final = await supervisor.ExecuteAsync($"Synthesize: {specialized}");
```

**Use when**: Complex tasks requiring planning and coordination

### 3. Parallel Scatter-Gather

```csharp
var tasks = subtasks.Select(st => agentPool.ExecuteAsync(st)).ToArray();
var results = await Task.WhenAll(tasks);
var synthesized = await synthesizerAgent.ExecuteAsync($"Combine: {results}");
```

**Use when**: Independent subtasks that can run simultaneously

### 4. Debate/Consensus

```csharp
var proposal = await agentA.ExecuteAsync(question);
var critique = await agentB.ExecuteAsync($"Critique this: {proposal}");
var revision = await agentA.ExecuteAsync($"Revise based on: {critique}");
var final = await judgeAgent.ExecuteAsync($"Choose best: {proposal} vs {revision}");
```

**Use when**: Need verification, different perspectives, quality control

## Advanced Patterns

### Agent Memory

```csharp
public class Agent
{
    private List<HazinaChatMessage> _memory = new();

    public async Task<string> ExecuteWithMemoryAsync(string task)
    {
        _memory.Add(new() { Role = HazinaMessageRole.User, Text = task });

        var response = await _llm.GetResponse(_memory, ...);

        _memory.Add(new() { Role = HazinaMessageRole.Assistant, Text = response.Result });

        return response.Result;
    }
}
```

**Benefit**: Agent remembers previous interactions in the session

### Agent Teams

```csharp
public class AgentTeam
{
    private List<Agent> _agents;

    public async Task<string> ConsensusAsync(string task)
    {
        // All agents vote
        var votes = await Task.WhenAll(_agents.Select(a => a.ExecuteAsync(task)));

        // Supervisor chooses best response
        var best = await _supervisor.ExecuteAsync($"Choose best answer: {votes}");
        return best;
    }
}
```

**Benefit**: Multiple perspectives, better quality through consensus

### Dynamic Agent Selection

```csharp
public class AgentRouter
{
    public async Task<string> RouteAsync(string task)
    {
        var classification = await _classifierAgent.ExecuteAsync(
            $"Classify this task: {task}\nCategories: technical, creative, analytical"
        );

        var selectedAgent = classification.Contains("technical") ? _technicalAgent :
                           classification.Contains("creative") ? _creativeAgent :
                           _analyticalAgent;

        return await selectedAgent.ExecuteAsync(task);
    }
}
```

**Benefit**: Automatic selection of best-suited agent

## Performance Optimization

### 1. Parallel Where Possible

**Slow**:
```csharp
var r1 = await agent.ExecuteAsync(task1);  // 5 seconds
var r2 = await agent.ExecuteAsync(task2);  // 5 seconds
var r3 = await agent.ExecuteAsync(task3);  // 5 seconds
// Total: 15 seconds
```

**Fast**:
```csharp
var tasks = new[] { task1, task2, task3 }
    .Select(t => agent.ExecuteAsync(t))
    .ToArray();
var results = await Task.WhenAll(tasks);
// Total: ~5 seconds (tasks run in parallel)
```

### 2. Reuse Agents

**Inefficient**:
```csharp
for (int i = 0; i < 100; i++)
{
    var agent = new Agent("Temp", ai); // Creates new agent each time
    await agent.ExecuteAsync(task);
}
```

**Efficient**:
```csharp
var agent = new Agent("Reusable", ai); // Create once
for (int i = 0; i < 100; i++)
{
    await agent.ExecuteAsync(task); // Reuse
}
```

### 3. Streaming for Long Outputs

```csharp
public async Task<string> ExecuteStreamingAsync(string task)
{
    var messages = new List<HazinaChatMessage> { /* ... */ };

    await _llm.GetResponseStream(
        messages,
        chunk => Console.Write(chunk), // Display as it arrives
        HazinaChatResponseFormat.Text,
        null, null,
        CancellationToken.None
    );
}
```

**Benefit**: User sees progress, perceived latency reduced

## Cost Management

Track costs across agents:

```csharp
public class CostTracker
{
    private Dictionary<string, decimal> _costByAgent = new();

    public async Task<string> ExecuteWithTrackingAsync(Agent agent, string task)
    {
        var response = await agent.ExecuteAsync(task);

        if (response.TokenUsage != null)
        {
            var cost = (response.TokenUsage.InputTokens * 0.03m / 1000) +
                      (response.TokenUsage.OutputTokens * 0.06m / 1000);

            _costByAgent[agent.Name] = _costByAgent.GetValueOrDefault(agent.Name) + cost;
        }

        return response.Result;
    }

    public void PrintCostSummary()
    {
        foreach (var (agent, cost) in _costByAgent)
        {
            Console.WriteLine($"{agent}: ${cost:F4}");
        }
    }
}
```

## Troubleshooting

### Agents Producing Inconsistent Results

**Problem**: Different agents give conflicting information.

**Solutions**:
- Add a "reviewer" agent that reconciles differences
- Use consensus pattern (multiple agents vote)
- Have supervisor agent validate consistency

### High Latency

**Problem**: Sequential workflows are too slow.

**Solutions**:
- Identify independent tasks → run in parallel
- Use cheaper/faster models for simple agents (GPT-3.5 instead of GPT-4)
- Cache common agent responses

### Expensive Multi-Agent Calls

**Problem**: Costs adding up quickly.

**Solutions**:
- Use cheaper models for simpler agents (research, triage)
- Limit conversation history (agents don't need full context every time)
- Cache repeated queries

## Next Steps

- [Multi-Agent System](../10-multi-agent/) - Advanced agent coordination
- [Hierarchical Agents](../17-hierarchical-agents/) - Supervisor patterns
- [Custom Tools](../03-custom-tools/) - Give agents tools to call
- [Agent Workflows](../../docs/AGENTS_GUIDE.md) - Complete agent guide

## Full Code

See [Program.cs](Program.cs) for the complete, runnable code.

---

**Congratulations! You've built a multi-agent system.**

Specialized agents collaborating produce better results than a single generalist AI. Use this pattern for complex workflows, quality control, and parallel execution.
