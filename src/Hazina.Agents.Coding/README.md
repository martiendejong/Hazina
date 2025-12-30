# Hazina.Agents.Coding

GLM-based deterministic coding agent with plan → act → observe loop.

## Architecture

```
┌──────────────────────────────────────────────────────┐
│  CodingAgent (Orchestrator)                          │
│  ├─ GlmPlanner (JSON-only output)                    │
│  ├─ ToolExecutor (PowerShell on Windows)             │
│  ├─ AgentSummaryStore (File-based memory)            │
│  └─ AgentLoop (Enforces control flow)                │
└──────────────────────────────────────────────────────┘
```

## Design Principles

1. **GLM is a planner, not an executor** - All execution happens outside the model
2. **JSON-only responses** - Enforces strict schema, no free-form chat
3. **Deterministic execution** - Windows PowerShell for all commands
4. **Auditable output** - Schema-validated, stored summaries
5. **Safety first** - Hard-rejects destructive commands

## GLM Output Schema

```json
{
  "thought": "short internal reasoning",
  "plan": ["step 1", "step 2"],
  "actions": [
    {
      "tool": "read_file|apply_diff|run|git_status|git_diff",
      "path": "optional",
      "diff": "optional unified diff",
      "command": "optional PowerShell command"
    }
  ]
}
```

## Allowed Tools

- **read_file** - Read file from working directory (requires: path)
- **apply_diff** - Apply unified diff to file (requires: path, diff)
- **run** - Execute PowerShell command (requires: command)
- **git_status** - Run git status
- **git_diff** - Run git diff

Unknown tools are hard-rejected.

## Agent Loop Flow

```
1. Load task + memory summary
2. Ask GLM for plan + actions
3. Validate JSON schema
4. Execute actions sequentially
5. Capture results
6. Summarize outcome (max 500 tokens)
7. Store summary
8. Stop when:
   - No actions returned
   - Tests succeed
   - Max iterations reached (default: 5)
```

Stop conditions are enforced by the agent loop, not by GLM.

## Usage

```csharp
using Hazina.Agents.Coding;
using Hazina.AI.Providers.Core;

var orchestrator = serviceProvider.GetRequiredService<IProviderOrchestrator>();
var agent = new CodingAgent(orchestrator);

var task = CodingAgent.CreateTask(
    description: "Add null checks to Parser.cs",
    workingDirectory: @"C:\Projects\MyApp",
    maxIterations: 5,
    testCommand: "dotnet test"
);

var result = await agent.RunAsync(task);

if (result.Success)
{
    Console.WriteLine($"Task completed in {result.Iterations} iterations");
    Console.WriteLine(result.Summary);
}
else
{
    Console.WriteLine($"Task failed: {result.Error}");
}
```

## Memory Storage

Summaries stored in:
```
{WorkingDirectory}/.hazina/agent-memory/{taskId}.txt
```

Format:
```
Iteration 1:
- Modified Parser.cs (null checks added)
- dotnet test failed: 1 error in ParserTests

Iteration 2:
...
```

## Safety Constraints

❌ **Destructive commands blocked:**
- `rm -rf`, `del /s`, `del /q`
- `Remove-Item -Recurse`
- `format`, `diskpart`
- `rd /s`, `rmdir /s`

❌ **Network access blocked:**
- `Invoke-WebRequest`, `Invoke-RestMethod`
- `wget`, `curl`

❌ **Environment inspection blocked:**
- `$env:`, `Set-ExecutionPolicy`
- `net user`, `net localgroup`

## Windows Compatibility

All commands execute via:
```powershell
powershell.exe -NoProfile -Command "<command>"
```

Captures stdout and stderr separately for observation.

## Error Handling

- Invalid JSON: Retry once with error feedback
- Unknown tool: Hard reject, no execution
- Destructive command: Hard reject, no execution
- Execution failure: Capture error, continue to next iteration
- Max retries exceeded: Fail with summary

## Example Task Flow

**Task:** Add error handling to API endpoint

**Iteration 1:**
```json
{
  "thought": "Need to see current implementation",
  "plan": ["Read API file", "Identify error handling gaps"],
  "actions": [
    {"tool": "read_file", "path": "src/Api/UserController.cs"}
  ]
}
```

**Iteration 2:**
```json
{
  "thought": "Add try-catch blocks around database calls",
  "plan": ["Apply error handling patch"],
  "actions": [
    {
      "tool": "apply_diff",
      "path": "src/Api/UserController.cs",
      "diff": "--- a/src/Api/UserController.cs\n+++ b/src/Api/UserController.cs\n..."
    }
  ]
}
```

**Iteration 3:**
```json
{
  "thought": "Run tests to verify",
  "plan": ["Execute test suite"],
  "actions": [
    {"tool": "run", "command": "dotnet test"}
  ]
}
```

Tests pass → Agent stops

## Integration with Hazina

Uses existing infrastructure:
- `IProviderOrchestrator` for GLM calls
- Follows Hazina agent patterns
- Compatible with existing tools and workspaces
- File-based memory (`.hazina/agent-memory/`)

## Differences from Free-Form Agents

| Aspect | CodingAgent | Traditional Agent |
|--------|-------------|-------------------|
| Output | JSON only | Free-form chat |
| Execution | Outside model | May be ambiguous |
| Tools | 5 allowed tools | Unlimited |
| Memory | Summaries only | Full logs |
| Stop conditions | Agent decides | Model suggests |
| Safety | Hard blocks | Soft warnings |

## Future Extensions

- Support for additional tools (lint, format, etc.)
- Multi-file diff operations
- Test result parsing
- Git commit automation
- IDE integration
