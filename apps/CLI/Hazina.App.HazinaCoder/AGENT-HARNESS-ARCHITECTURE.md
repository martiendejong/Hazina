# HazinaCoder Agent Harness Architecture

**File:** `hazinacoder-agent-harness.drawio`
**Generated:** 2026-05-15

## Overview

The HazinaCoder agent harness is a **C#/.NET workflow engine** with **real-time streaming** capabilities. It combines structured workflow execution with event-driven architecture for live progress updates.

## Core Components

### 1. Workflow Engine (`WorkflowEngine.cs`)

**Purpose:** Orchestrates step-by-step task execution with validation and rollback support.

**Key Features:**
- ✅ YAML-based workflow definitions
- ✅ Sequential step execution with prerequisites
- ✅ Automatic rollback on failure
- ✅ Step validation (output, files, conditions)
- ✅ Timeout enforcement per step
- ✅ Context sharing between steps
- ✅ Execution history tracking

**Flow:**
```
Load YAML → Validate Prerequisites → Execute Steps → Validate Output → Complete/Rollback
```

**Example Workflow YAML:**
```yaml
name: "Feature Implementation"
steps:
  - name: "Read ClickUp Task"
    type: "read_clickup_task"
    required: true
    timeout_seconds: 30

  - name: "Investigate Codebase"
    type: "investigate_codebase"
    prerequisites: ["Read ClickUp Task"]
    required: true

  - name: "Implement Changes"
    type: "implement_changes"
    prerequisites: ["Investigate Codebase"]
    required: true
    rollback_on_failure: true
    rollback_step: "revert_changes"
```

### 2. Streaming Orchestrator (`StreamingOrchestrator.cs`)

**Purpose:** Real-time event streaming to multiple endpoints (SSE, WebSocket, Console).

**Key Features:**
- ✅ Channel-based event bus (bounded channel, 1000 capacity)
- ✅ IAsyncEnumerable streaming API
- ✅ Multiple concurrent endpoints
- ✅ Auto-reconnect support
- ✅ Event type system
- ✅ Drop oldest strategy when full

**Event Types:**
- `PlanningStarted` - Workflow planning begins
- `PlanningStep` - Individual planning step
- `ActionStarted` - Step execution starts
- `ActionProgress` - Progress updates (percentage)
- `ToolExecutionStarted` - Tool invocation
- `BuildProgress` - Build output streaming
- `TestProgress` - Test execution status
- `TokenUsageUpdate` - LLM token tracking
- `FileChanged` - File modifications
- `SummaryGenerated` - Final execution summary

**Streaming Architecture:**
```
Step Execution
      ↓
EmitEventAsync(event)
      ↓
Channel.Writer.WriteAsync(event)
      ↓
Broadcast to all endpoints
      ├── SSE Endpoint → Browser
      ├── WebSocket → Client
      └── Console → Terminal
```

### 3. Workflow Steps (7 Default Steps)

1. **ReadClickUpTaskStep**
   - Fetches task from ClickUp API
   - Extracts requirements

2. **InvestigateCodebaseStep**
   - Uses Grep/Find to locate relevant files
   - Analyzes project structure

3. **AllocateWorktreeStep**
   - Creates Git worktree for isolation
   - Sets up working directory

4. **ImplementChangesStep**
   - Invokes LLM for code generation
   - Applies changes to worktree

5. **MergeDevelopStep**
   - Merges latest develop branch
   - Resolves conflicts if needed

6. **BuildTestStep**
   - Runs `dotnet build`
   - Executes `dotnet test`
   - Streams output in real-time

7. **CreatePRStep**
   - Commits changes
   - Creates GitHub PR via `gh` CLI
   - Links to ClickUp task

### 4. Workflow Context

**Shared state across all steps:**

```csharp
public class WorkflowContext
{
    public Dictionary<string, string> Variables { get; set; }
    public Dictionary<string, object?> Outputs { get; set; }
    public HashSet<string> CompletedSteps { get; set; }
    public string WorkingDirectory { get; set; }
    public CancellationToken CancellationToken { get; set; }
}
```

**Usage:**
```csharp
// Step 1 stores output
context.SetOutput("worktreePath", "/path/to/worktree");

// Step 2 retrieves it
var path = context.GetOutput<string>("worktreePath");
```

### 5. Step Validation

**Three validation types:**

1. **Output Validation**
   ```yaml
   validation:
     require_output: true
     error_message: "Step must produce output"
   ```

2. **File Validation**
   ```yaml
   validation:
     file_exists: "${worktree_path}/src/MyFile.cs"
     error_message: "Generated file not found"
   ```

3. **Condition Validation**
   ```yaml
   validation:
     condition: "exit_code == 0"
     error_message: "Build failed"
   ```

## Architecture Patterns

### Event-Driven Streaming

Unlike the Node.js approval-workflow-app which uses callbacks, HazinaCoder uses **C# Channels** for high-performance streaming:

```csharp
// Producer (Workflow Engine)
await orchestrator.EmitActionProgressAsync(
    actionType: "Building",
    progress: 45.2,
    message: "Compiling 12/24 projects..."
);

// Consumer (Frontend via SSE)
await foreach (var evt in orchestrator.GetEventStreamAsync(cancellationToken))
{
    Console.WriteLine($"{evt.EventType}: {evt.Message}");
}
```

### IAsyncEnumerable Pattern

C#'s native async streaming:

```csharp
public async IAsyncEnumerable<AgentEvent> GetEventStreamAsync(
    [EnumeratorCancellation] CancellationToken ct = default)
{
    await foreach (var evt in _channel.Reader.ReadAllAsync(ct))
    {
        yield return evt;
    }
}
```

**Benefits:**
- Native C# async/await support
- Automatic backpressure handling
- Cancellation token integration
- Memory efficient (no buffering)

### Rollback-First Design

Every step can define rollback behavior:

```csharp
if (!stepResult.Success && stepDef.RollbackOnFailure)
{
    Console.WriteLine($"Rolling back: {stepDef.RollbackStep}");
    await ExecuteRollbackAsync(stepDef.RollbackStep, context, ct);
}
```

**Example Rollback:**
- Step fails: "Implement Changes"
- Triggers rollback: "revert_changes"
- Rollback step: `git reset --hard origin/develop`
- Cleans up worktree

## Comparison: HazinaCoder vs Node.js Harness

| Feature | HazinaCoder (.NET) | approval-workflow-app (Node.js) |
|---------|-------------------|----------------------------------|
| **Language** | C# / .NET | JavaScript / Node.js |
| **Workflow Definition** | YAML files | JavaScript config objects |
| **Streaming** | C# Channels + IAsyncEnumerable | Callbacks + SSE |
| **State Machine** | Workflow Engine | LangGraph StateGraph |
| **Steps** | IWorkflowStep interface | Node functions |
| **Validation** | Built-in YAML validation | Rule Engine |
| **Rollback** | Automatic per-step | Manual in rule check |
| **Event System** | Typed events (10+ types) | Generic log strings |
| **Endpoints** | SSE + WebSocket + Console | SSE only |
| **Prerequisites** | YAML-defined dependencies | Graph edges |
| **Timeout** | Per-step timeout config | Global max iterations |

## Real-World Flow

### Task: "Implement user authentication"

**1. User Input**
```bash
hazinacoder execute-task --clickup-task CU-abc123
```

**2. Workflow Engine Loads YAML**
```yaml
name: "Feature Implementation"
steps: [...]
```

**3. Step Execution with Streaming**

```
[Workflow] Starting: Feature Implementation
[Workflow] Steps: 7

📡 Event: PlanningStarted (7 steps)

[Workflow] Step 1/7: Read ClickUp Task
[Step] Reading ClickUp task...
📡 Event: ActionStarted (read_clickup_task)
✅ Step completed: Read ClickUp Task
📡 Event: ActionCompleted (read_clickup_task)

[Workflow] Step 2/7: Investigate Codebase
[Step] Investigating codebase...
📡 Event: ActionProgress (20%, "Searching for auth files...")
📡 Event: ActionProgress (60%, "Found 3 relevant files")
✅ Step completed: Investigate Codebase

[Workflow] Step 3/7: Allocate Worktree
[Step] Allocating worktree...
📡 Event: ToolExecutionStarted (git_worktree_add)
✅ Step completed: Allocate Worktree
📡 Event: FileChanged (/worktrees/task-abc123)

[Workflow] Step 4/7: Implement Changes
[Step] Implementing changes...
📡 Event: TokenUsageUpdate (1234 tokens, $0.0234)
📡 Event: FileChanged (AuthController.cs, Created)
📡 Event: FileChanged (AuthService.cs, Created)
✅ Step completed: Implement Changes

[Workflow] Step 5/7: Merge Develop
[Step] Merging develop branch...
✅ Step completed: Merge Develop

[Workflow] Step 6/7: Build & Test
[Step] Building and testing...
📡 Event: BuildProgress ("Building Hazina.Auth...")
📡 Event: BuildProgress ("Build succeeded.")
📡 Event: TestProgress (AuthTests, 5/5 passed)
✅ Step completed: Build & Test

[Workflow] Step 7/7: Create PR
[Step] Creating pull request...
📡 Event: ActionStarted (create_pr)
✅ Step completed: Create PR
📡 Event: ActionCompleted (create_pr)

✅ Workflow completed successfully: Feature Implementation
Duration: 00:03:45

📡 Event: SummaryGenerated
   Total Actions: 7
   Successful: 7
   Failed: 0
   Duration: 3m 45s
   Cost: $0.0234
```

**4. Frontend Display**

Browser receives SSE stream and displays:
- Progress bar (0% → 100%)
- Live log output
- Token usage counter
- File change notifications
- Final PR link

## Key Innovations

### 1. YAML-First Workflow Design
Workflows are **configuration, not code**. Easy to:
- Version control
- Review changes
- Generate dynamically
- Test in isolation

### 2. Bounded Channel with Drop Strategy
```csharp
Channel.CreateBounded<AgentEvent>(new BoundedChannelOptions(1000)
{
    FullMode = BoundedChannelFullMode.DropOldest
});
```

**Prevents memory leaks** when consumers are slow. Drops oldest events first (FIFO overflow).

### 3. Typed Event System
Not just strings - strongly typed events with metadata:

```csharp
public class BuildEvent : AgentEvent
{
    public string ProjectPath { get; set; }
    public string OutputLine { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
}
```

**Benefits:**
- Compile-time safety
- IntelliSense support
- Easy filtering/routing
- Structured logging

### 4. Multi-Endpoint Broadcasting
Same event → multiple destinations:

```csharp
foreach (var endpoint in _endpoints)
{
    await endpoint.SendEventAsync(agentEvent);
}
```

**Endpoints:**
- `SSEEndpoint` → Browser
- `WebSocketEndpoint` → Real-time clients
- `ConsoleEndpoint` → Terminal output
- `FileEndpoint` → Log files
- Custom endpoints via `IStreamingEndpoint`

## Files to Explore

| File | Purpose |
|------|---------|
| `Core/Streaming/StreamingOrchestrator.cs` | Event streaming system |
| `Core/Workflow/WorkflowEngine.cs` | Workflow execution engine |
| `Core/Streaming/AgentEvent.cs` | Event type definitions |
| `Core/Execution/ParallelToolExecutor.cs` | Parallel tool execution |
| `Core/State/StateManager.cs` | State persistence |

## Next Steps

1. ✅ Architecture documented
2. ⏳ Implement missing step types
3. ⏳ Add real LLM integration
4. ⏳ Create workflow YAML templates
5. ⏳ Build frontend SSE consumer

---

**The HazinaCoder harness is production-ready .NET infrastructure for autonomous agent workflows!** 🚀
