# DevGPT LLM Client Tools

Tool calling extensions for DevGPT LLM clients. This package provides reusable tools that LLMs can invoke for various tasks including AI coding assistants, CLI execution, and web scraping.

## Available Tools

### 1. Claude CLI Tool (`ClaudeCliTool`)
Calls the Anthropic Claude CLI for conversational tasks.

**Tool name:** `claude_cli`

**Usage:**
```csharp
// As a tool for LLM function calling
var tool = ClaudeCliTool.Create();
tools.Add(tool);

// Direct programmatic usage
var result = await ClaudeRunner.RunClaudeAsync(
    prompt: "Explain quantum computing in simple terms",
    model: "claude-3-5-sonnet-latest",
    timeoutSeconds: 120
);
```

**Requirements:**
- The `claude` CLI must be installed and available on your PATH, and you must be logged in
- On Windows, the tool invokes `cmd.exe /c claude ...` so npm global shims (claude.cmd) work
- Optional: Set `CLAUDE_CLI_PATH` to the full path of the CLI if it is not on PATH

**Environment Variables:**
- `CLAUDE_CLI_PATH`: Full path to the Claude CLI executable. Examples:
  - Windows (npm global shim): `C:\Users\you\AppData\Roaming\npm\claude.cmd`
  - Windows (exe): `C:\Tools\claude\claude.exe`
  - macOS/Linux: `/usr/local/bin/claude`

**Default Timeout:** 120 seconds

### 2. Claude Code Tool (`ClaudeCodeTool`)
Calls the Claude Code CLI for AI-assisted coding tasks in a specific directory.

**Tool name:** `claude_code`

**Usage:**
```csharp
// As a tool for LLM function calling
var tool = ClaudeCodeTool.Create();
tools.Add(tool);

// Direct programmatic usage
var result = await ClaudeCodeRunner.RunClaudeCodeAsync(
    prompt: "Refactor the authentication module to use JWT",
    workingDirectory: @"C:\Projects\MyApp",
    model: "claude-sonnet-4-5-20250929",
    timeoutSeconds: 300
);
```

**Environment Variables:**
- `CLAUDE_CODE_CLI_PATH`: Optional path to Claude Code CLI executable

**Default Timeout:** 300 seconds (5 minutes)

### 3. Codex Tool (`CodexTool`)
Calls a Codex CLI (e.g., GPT-based coding assistant) for AI-assisted coding tasks.

**Tool name:** `codex`

**Usage:**
```csharp
// As a tool for LLM function calling
var tool = CodexTool.Create();
tools.Add(tool);

// Direct programmatic usage
var result = await CodexRunner.RunCodexAsync(
    prompt: "Write unit tests for the UserService class",
    workingDirectory: @"C:\Projects\MyApp\tests",
    model: "gpt-4",
    timeoutSeconds: 300
);
```

**Environment Variables:**
- `CODEX_CLI_PATH`: Optional path to Codex CLI executable

**Default Timeout:** 300 seconds (5 minutes)

### 4. Web Page Scraper (`WebPageScraper`)
Scrapes and cleans HTML content from web pages.

**Usage:**
```csharp
var tool = WebPageScraper.Create();
tools.Add(tool);
```

## Multi-Agent Coding Orchestration

The package includes a powerful `MultiAgentCodingOrchestrator` that enables coordinating multiple AI coding agents (Claude Code, Codex, etc.) to work on complex tasks.

### Example: Orchestrated Task Breakdown

```csharp
using DevGPT.LLMClientTools.Examples;

// Setup orchestrator with GPT-4
var openAIClient = new OpenAIClientWrapper(config);
var orchestrator = new MultiAgentCodingOrchestrator(openAIClient);

// Run a complex task - the orchestrator will break it down and delegate
var result = await orchestrator.OrchestrateCodingTaskAsync(
    mainTask: @"Refactor the user authentication module:
                1. Replace sessions with JWT tokens
                2. Update all API endpoints
                3. Write comprehensive unit tests
                4. Update API documentation",
    projectDirectory: @"C:\Projects\MyApp",
    cancel: cancellationToken
);

Console.WriteLine(result);
```

The orchestrator will:
1. Analyze the complex task
2. Break it into smaller subtasks
3. Delegate each subtask to the most appropriate agent (Claude Code for refactoring, Codex for tests, etc.)
4. Coordinate dependencies between tasks
5. Monitor progress and handle errors
6. Report final results

### Example: Parallel Independent Tasks

```csharp
// Run multiple coding agents in parallel on independent tasks
var tasks = new Dictionary<string, (string task, string workingDir)>
{
    ["claude-frontend"] = (
        "Implement new user profile page with React hooks",
        @"C:\Projects\MyApp\frontend"
    ),
    ["claude-backend"] = (
        "Add database migration for user preferences table",
        @"C:\Projects\MyApp\backend"
    ),
    ["codex-tests"] = (
        "Write integration tests for authentication flow",
        @"C:\Projects\MyApp\backend\tests"
    ),
    ["codex-docs"] = (
        "Generate API documentation from OpenAPI spec",
        @"C:\Projects\MyApp\docs"
    )
};

var results = await orchestrator.RunParallelCodingTasksAsync(
    tasks,
    cancellationToken
);

foreach (var (agent, result) in results)
{
    Console.WriteLine($"\n=== {agent} ===");
    Console.WriteLine(result);
}
```

## Prerequisites

For the coding tools to work, you need:

1. **Claude CLI**: Install from https://claude.ai/download
   - Login: `claude login`
   - Verify: `claude "hello"`

2. **Claude Code CLI**: Install from https://claude.com/claude-code
   - Login via the CLI
   - Verify: `claude-code --version`

3. **Codex CLI** (optional): Any GPT-based coding CLI tool
   - Set up according to your tool's documentation

## Architecture

The tools follow a consistent pattern:

```
Tool Class (e.g., ClaudeCodeTool)
    ↓
Creates DevGPTChatTool with:
    - Name
    - Description
    - Parameters
    - Execute function
        ↓
    Calls Runner (e.g., ClaudeCodeRunner)
        ↓
    Executes CLI process
        ↓
    Returns result
```

## Adding Custom Tools

You can create your own tools following the same pattern:

```csharp
public static class MyCustomTool
{
    public static DevGPTChatTool Create()
    {
        return new DevGPTChatTool(
            name: "my_tool",
            description: "What this tool does",
            parameters: new List<ChatToolParameter> { /* ... */ },
            execute: async (messages, toolCall, cancel) =>
            {
                // Your tool logic here
                return "result";
            }
        );
    }

    public static void Register(IToolsContext tools)
    {
        tools.Add(Create());
    }
}
```

## Tool Context

Use `ToolsContextBase` to manage collections of tools:

```csharp
public class MyToolsContext : ToolsContextBase
{
    public MyToolsContext()
    {
        ClaudeCodeTool.Register(this);
        CodexTool.Register(this);
        ClaudeCliTool.Register(this);
        WebPageScraper.Register(this);
    }
}
```

## Best Practices

1. **Timeouts**: Set appropriate timeouts for coding tasks (300-600 seconds)
2. **Working Directories**: Always specify working directories for code tools
3. **Error Handling**: Tool executions can fail - handle errors gracefully
4. **Parallel Execution**: Use parallel tasks for independent work
5. **Orchestration**: Use an orchestrator (GPT-4) to coordinate complex multi-step tasks
6. **Monitoring**: Log tool calls and results for debugging

## License

MIT License - see LICENSE file for details.

