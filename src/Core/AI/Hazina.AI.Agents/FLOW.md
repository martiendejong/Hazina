# Agent Framework Flow

## Happy Path: Simple Agent Task

```
1. User calls agent.RunAsync(input)
2. Build messages: system prompt + user input + tool definitions
3. Send to LLM via ProviderOrchestrator
4. Parse response for tool calls
5. If no tool calls: return response directly
6. If tool calls: execute tools, add results, re-send to LLM
7. Return final response
```

## Happy Path: Workflow Execution

```
1. User calls engine.ExecuteAsync(workflow, context)
2. For each step in workflow:
   a. Check step type (AgentTask, Parallel, Conditional, Loop)
   b. Execute step with current context
   c. Add step output to context
   d. Move to next step
3. Return final context with all outputs
```

## Happy Path: Multi-Agent Debate

```
1. User calls coordinator.CoordinateAsync(agents, task, Debate)
2. Round 1: Each agent responds independently
3. Round 2+: Each agent sees all responses, critiques others
4. After each round: check for consensus
5. If consensus reached: return agreed answer
6. If max rounds: return majority/aggregated answer
```

## Sequence Diagram: Tool Calling

```
User          Agent          LLM           Tool
  │              │             │             │
  │ RunAsync()   │             │             │
  │─────────────►│             │             │
  │              │             │             │
  │              │ GetResponse()             │
  │              │────────────►│             │
  │              │◄────────────│             │
  │              │ "TOOL: calc(2,3,add)"     │
  │              │             │             │
  │              │ Execute()   │             │
  │              │────────────────────────── │
  │              │◄────────────────────────► │
  │              │ result: "5" │             │
  │              │             │             │
  │              │ GetResponse()             │
  │              │────────────►│             │
  │              │◄────────────│             │
  │              │ "2 + 3 = 5" │             │
  │              │             │             │
  │◄─────────────│             │             │
  │ "2 + 3 = 5"  │             │             │
```

## Sequence Diagram: Workflow

```
User          Engine         Step1         Step2         Step3
  │              │             │             │             │
  │ Execute()    │             │             │             │
  │─────────────►│             │             │             │
  │              │             │             │             │
  │              │ Execute()   │             │             │
  │              │────────────►│             │             │
  │              │◄────────────│             │             │
  │              │ ctx[step1]  │             │             │
  │              │             │             │             │
  │              │ Execute()   │             │             │
  │              │────────────────────────── │             │
  │              │◄────────────────────────► │             │
  │              │ ctx[step2]  │             │             │
  │              │             │             │             │
  │              │ Execute()   │             │             │
  │              │────────────────────────────────────────►│
  │              │◄────────────────────────────────────────│
  │              │ ctx[step3]  │             │             │
  │              │             │             │             │
  │◄─────────────│             │             │             │
  │ final ctx    │             │             │             │
```

## Error Paths

### Tool Execution Fails
```
1. LLM requests tool call
2. Tool execution throws exception
3. Exception caught, error message created
4. Error added to messages as tool result
5. LLM re-sent with error context
6. LLM adjusts approach or reports failure
```

### Tool Not Found
```
1. LLM requests unknown tool
2. Agent checks tool registry
3. Tool not found
4. Error message: "Tool 'X' not available"
5. LLM re-sent with available tools list
```

### Infinite Tool Loop
```
1. LLM keeps calling tools repeatedly
2. Iteration counter increments
3. If iterations > maxIterations (default: 10)
4. Force return last response
5. Log warning about possible loop
```

### Workflow Step Fails
```
1. Step execution throws exception
2. Step marked as Failed
3. Workflow stops (unless continueOnError: true)
4. Return partial context with error info
```

### Debate No Consensus
```
1. Agents debate for maxRounds
2. Still no consensus
3. Aggregation strategy applies:
   - Majority vote
   - OR merge all viewpoints
   - OR return "no consensus" with all opinions
```

## Step Types

| Type | Description | Context Flow |
|------|-------------|--------------|
| AgentTask | Single agent executes task | input → agent → output |
| Parallel | Multiple steps run concurrently | inputs → [agents...] → merged outputs |
| Conditional | Branch based on condition | input → condition → branch A or B |
| Loop | Repeat until condition | input → loop body → condition check → repeat or exit |

## Tool Call Format

```
Input: "Calculate 15% of 230"

LLM Response:
TOOL: Calculator(a=230, b=15, operation=percent)

Parsed:
{
  "tool": "Calculator",
  "parameters": {
    "a": 230,
    "b": 15,
    "operation": "percent"
  }
}

Tool Execution:
Calculator.ExecuteAsync(230, 15, "percent") → "34.5"

Final Response:
"15% of 230 is 34.5"
```

## Multi-Agent Strategies

```
SEQUENTIAL
──────────
Agent1 ──► output1 ──► Agent2 ──► output2 ──► Agent3 ──► final
(Pipeline: each agent builds on previous)

PARALLEL
────────
         ┌─► Agent1 ──► output1 ─┐
input ───┼─► Agent2 ──► output2 ─┼──► Aggregate ──► final
         └─► Agent3 ──► output3 ─┘
(All work independently, results merged)

DEBATE
──────
Round 1: All respond independently
Round 2: All see others' responses, critique
Round 3: Seek consensus
(Iterative refinement toward agreement)

HIERARCHICAL
────────────
         Manager (decomposes task)
              │
    ┌─────────┼─────────┐
    ▼         ▼         ▼
 Worker1   Worker2   Worker3
    │         │         │
    └─────────┼─────────┘
              ▼
         Manager (aggregates)
              │
              ▼
           Final
(Boss/worker pattern)
```

## Key Decision Points

```
                    Agent receives input
                          │
                          ▼
                 ┌─────────────────┐
                 │ Has tools?      │
                 └─────────────────┘
                    │         │
                  Yes        No
                    │         │
                    ▼         │
             Include tool     │
             definitions      │
                    │         │
                    └────┬────┘
                         │
                         ▼
                    Send to LLM
                         │
                         ▼
                 ┌─────────────────┐
                 │ Tool call in    │
                 │ response?       │
                 └─────────────────┘
                    │         │
                  Yes        No
                    │         │
                    ▼         ▼
             Execute tool    Return
             Add result      response
             Loop back
```
