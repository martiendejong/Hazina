# Agents Domain Architecture

## Overview Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            YOUR APPLICATION                                  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          AGENT FRAMEWORK                                     │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                           Agent                                      │   │
│  │  • Name, Role, System Prompt                                        │   │
│  │  • Conversation History                                              │   │
│  │  • Tool Registry                                                     │   │
│  │  • ProviderOrchestrator (for LLM calls)                             │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                      │                                       │
│            ┌─────────────────────────┼─────────────────────────┐            │
│            ▼                         ▼                         ▼            │
│  ┌─────────────────┐      ┌─────────────────┐      ┌─────────────────┐     │
│  │   AgentTool     │      │   AgentTool     │      │   AgentTool     │     │
│  │   Calculator    │      │   WebSearch     │      │   FileReader    │     │
│  └─────────────────┘      └─────────────────┘      └─────────────────┘     │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         WORKFLOW ENGINE                                      │
│                                                                              │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                          Workflow                                    │   │
│  │  Name: "document-processing"                                         │   │
│  │  Steps: [Analyze] → [Extract] → [Summarize] → [Store]               │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                              │
│  Step Types:                                                                 │
│  ┌───────────────┐  ┌───────────────┐  ┌───────────────┐  ┌─────────────┐ │
│  │  AgentTask    │  │   Parallel    │  │  Conditional  │  │    Loop     │ │
│  │  Single agent │  │  Run N steps  │  │  If/else      │  │  Repeat N   │ │
│  │  task         │  │  concurrently │  │  branching    │  │  times      │ │
│  └───────────────┘  └───────────────┘  └───────────────┘  └─────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                      MULTI-AGENT COORDINATOR                                 │
│                                                                              │
│  Strategies:                                                                 │
│  ┌───────────────────────────────────────────────────────────────────────┐ │
│  │                                                                        │ │
│  │  SEQUENTIAL ──► Agent1 ──► Agent2 ──► Agent3 ──► Result              │ │
│  │                Pipeline processing                                     │ │
│  │                                                                        │ │
│  │  PARALLEL   ──► ┌─Agent1─┐                                            │ │
│  │                 ├─Agent2─┼──► Aggregate Results                       │ │
│  │                 └─Agent3─┘                                            │ │
│  │                                                                        │ │
│  │  DEBATE     ──► Round 1: All agents respond                          │ │
│  │                 Round 2: Agents critique each other                   │ │
│  │                 Round 3: Reach consensus                              │ │
│  │                                                                        │ │
│  │  HIERARCHICAL─► Manager decomposes task                               │ │
│  │                 Workers execute subtasks                              │ │
│  │                 Manager aggregates results                            │ │
│  └───────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Tool Calling Flow

```
User Input: "Calculate 15% of 230"
     │
     ▼
┌─────────────────────┐
│   Agent.RunAsync()  │
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Build Messages      │ ─── System prompt + user message
│ with Tool Defs      │     + tool definitions in JSON schema
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Send to LLM         │ ─── ProviderOrchestrator.GetResponse()
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Parse Response      │ ─── Check for tool calls
└─────────────────────┘
     │
     ├──► No tool call ──► Return response directly
     │
     └──► Tool call found
              │
              ▼
         ┌─────────────────────┐
         │ TOOL: Calculator    │
         │ (a=230, b=15,       │
         │  operation=percent) │
         └─────────────────────┘
              │
              ▼
         ┌─────────────────────┐
         │ Execute Tool        │ ─── CalculatorTool.ExecuteAsync()
         └─────────────────────┘
              │
              ▼
         ┌─────────────────────┐
         │ Tool Result: "34.5" │
         └─────────────────────┘
              │
              ▼
         ┌─────────────────────┐
         │ Add to Messages     │ ─── Tool result becomes context
         └─────────────────────┘
              │
              ▼
         ┌─────────────────────┐
         │ Send back to LLM    │ ─── For final response
         └─────────────────────┘
              │
              ▼
         ┌─────────────────────┐
         │ "15% of 230 is      │
         │  34.5"              │
         └─────────────────────┘
```

## Workflow Execution Flow

```
Workflow: "document-analysis"
     │
     ▼
┌───────────────────────────────────────────────────────────────┐
│  WorkflowEngine.ExecuteAsync(workflow, initialContext)        │
└───────────────────────────────────────────────────────────────┘
     │
     ▼
┌───────────────────────────────────────────────────────────────┐
│  Step 1: AgentTaskStep("analyze")                             │
│  Agent: DocumentAnalyzer                                       │
│  Prompt: "Analyze this document: {{input}}"                   │
│  Output: analyze.output = "This is a technical specification" │
└───────────────────────────────────────────────────────────────┘
     │
     ▼
┌───────────────────────────────────────────────────────────────┐
│  Step 2: ConditionalStep                                       │
│  Condition: context["analyze.output"].Contains("technical")   │
│                                                                │
│  ├── TRUE  ──► TechnicalSummaryStep                           │
│  │              Prompt: "Create technical summary..."          │
│  │                                                             │
│  └── FALSE ──► GeneralSummaryStep                             │
│                Prompt: "Create general summary..."             │
└───────────────────────────────────────────────────────────────┘
     │
     ▼
┌───────────────────────────────────────────────────────────────┐
│  Step 3: ParallelStep                                          │
│  ┌─────────────────┐  ┌─────────────────┐  ┌───────────────┐ │
│  │ ExtractKeywords │  │ ExtractEntities │  │ ExtractDates  │ │
│  │ (concurrent)    │  │ (concurrent)    │  │ (concurrent)  │ │
│  └─────────────────┘  └─────────────────┘  └───────────────┘ │
│                              │                                 │
│                    Aggregate all outputs                       │
└───────────────────────────────────────────────────────────────┘
     │
     ▼
┌───────────────────────────────────────────────────────────────┐
│  Step 4: LoopStep                                              │
│  MaxIterations: 3                                              │
│  Condition: !context["quality.score"] >= 0.9                  │
│                                                                │
│  Loop Body: RefineOutput agent                                 │
│  "Improve quality: {{previous_output}}"                        │
└───────────────────────────────────────────────────────────────┘
     │
     ▼
┌───────────────────────────────────────────────────────────────┐
│  Final Context:                                                │
│  {                                                             │
│    "input": "original document",                               │
│    "analyze.output": "technical specification",                │
│    "summary.output": "...",                                    │
│    "keywords": ["API", "REST", "authentication"],              │
│    "entities": ["OAuth", "JWT"],                               │
│    "dates": ["2024-01-15"],                                    │
│    "final_output": "refined summary..."                        │
│  }                                                             │
└───────────────────────────────────────────────────────────────┘
```

## Multi-Agent Debate Flow

```
Task: "What is the best programming language for web development?"
     │
     ▼
┌───────────────────────────────────────────────────────────────┐
│  MultiAgentCoordinator.CoordinateAsync(                       │
│    agents: [PythonExpert, JavaScriptExpert, RustExpert],      │
│    strategy: Debate,                                           │
│    maxRounds: 3                                                │
│  )                                                             │
└───────────────────────────────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────── ROUND 1 ──────────────────────┐
│                                                               │
│  PythonExpert: "Python with Django/Flask offers rapid..."    │
│  JavaScriptExpert: "JavaScript with Node.js enables..."      │
│  RustExpert: "Rust with Actix provides memory safety..."     │
│                                                               │
└───────────────────────────────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────── ROUND 2 ──────────────────────┐
│  (Each agent sees all Round 1 responses)                      │
│                                                               │
│  PythonExpert: "While JS has broader adoption, Python's..."  │
│  JavaScriptExpert: "I agree Rust is faster, but JS has..."   │
│  RustExpert: "Performance matters, but I acknowledge..."     │
│                                                               │
└───────────────────────────────────────────────────────────────┘
     │
     ▼
┌─────────────────────────────── ROUND 3 ──────────────────────┐
│  (Consensus check)                                            │
│                                                               │
│  Detector: "Agents agree on: ecosystem matters, use case     │
│            determines choice, all are valid options"          │
│                                                               │
│  Consensus: REACHED                                           │
└───────────────────────────────────────────────────────────────┘
     │
     ▼
┌───────────────────────────────────────────────────────────────┐
│  Final Result:                                                 │
│  "The best language depends on your specific needs:           │
│   - Python: Rapid development, data science integration       │
│   - JavaScript: Full-stack capability, largest ecosystem      │
│   - Rust: Performance-critical applications, safety           │
│                                                                │
│   All three agents agreed that the choice depends on          │
│   project requirements rather than absolute superiority."     │
└───────────────────────────────────────────────────────────────┘
```

## Key Files

| Component | File |
|-----------|------|
| Agent | `Hazina.AI.Agents/Core/Agent.cs` |
| Agent Tool | `Hazina.AI.Agents/Tools/AgentTool.cs` |
| Calculator Tool | `Hazina.AI.Agents/Tools/CalculatorTool.cs` |
| Workflow | `Hazina.AI.Agents/Workflows/Workflow.cs` |
| Workflow Engine | `Hazina.AI.Agents/Workflows/WorkflowEngine.cs` |
| Step Types | `Hazina.AI.Agents/Workflows/WorkflowStep.cs` |
| Coordinator | `Hazina.AI.Agents/Coordination/MultiAgentCoordinator.cs` |
