# Claude Code Implementation Plan for Hazina

## Objective
Transform `Hazina.App.ClaudeCode` from a basic chat interface into a fully autonomous coding assistant with file operations, command execution, and tool-calling capabilities similar to Claude Code.

## Current State Analysis

### What Exists
1. **Tool Infrastructure (Built-in):**
   - `HazinaChatTool` - Complete tool definition system
   - `IToolsContext` - Tool provider interface
   - `OpenAIClientWrapper.GetResponse()` - Already supports `IToolsContext` parameter
   - `SimpleOpenAIClientChatInteraction` - Automatic tool-calling loop (up to 50 calls)
   - Tool execution happens in `HandleToolCalls()` method

2. **Agent Infrastructure:**
   - `AgentTool` base class in `Hazina.AI.Agents`
   - `CalculatorTool` example

### What's Missing
1. Core file operation tools (Read, Write, Edit, Glob, Grep)
2. Command execution tool (Bash/PowerShell)
3. ToolsContext instantiation and configuration
4. System prompt instructing tool usage
5. Agentic behavior prompts

---

## Implementation Phases

### **Phase 1: Core Tool Implementations**
Create concrete tool implementations using `HazinaChatTool`:

#### 1.1 ReadFileTool
```csharp
public class ReadFileTool
{
    public static HazinaChatTool Create(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "read_file",
            description: "Read contents of a file from the file system",
            parameters: new List<ChatToolParameter>
            {
                new() { Name = "file_path", Type = "string", Required = true,
                       Description = "Absolute or relative path to file" },
                new() { Name = "offset", Type = "number", Required = false,
                       Description = "Line number to start reading from (default: 0)" },
                new() { Name = "limit", Type = "number", Required = false,
                       Description = "Number of lines to read (default: all)" }
            },
            execute: async (messages, call, cancel) =>
            {
                // Implementation: Read file with optional line range
                // Return formatted output with line numbers
            }
        );
    }
}
```

#### 1.2 WriteFileTool
```csharp
- Tool name: "write_file"
- Parameters: file_path, content
- Implementation: Write/overwrite file, create directories if needed
- Return: Success message with file path
```

#### 1.3 EditFileTool
```csharp
- Tool name: "edit_file"
- Parameters: file_path, old_string, new_string, replace_all
- Implementation: Exact string replacement in files
- Return: Preview of changes or error
```

#### 1.4 BashTool (PowerShell on Windows)
```csharp
- Tool name: "bash"
- Parameters: command, timeout, working_directory
- Implementation: Execute PowerShell commands
- Safety: Block destructive commands (rm -rf, format, etc.)
- Return: stdout, stderr, exit code
```

#### 1.5 GlobTool
```csharp
- Tool name: "glob"
- Parameters: pattern, path
- Implementation: File pattern matching (e.g., "**/*.cs")
- Return: List of matching file paths
```

#### 1.6 GrepTool
```csharp
- Tool name: "grep"
- Parameters: pattern, path, output_mode, -i, -A, -B, -C
- Implementation: Content search using regex
- Return: Matching lines or file paths
```

---

### **Phase 2: ToolsContext Setup**

#### 2.1 Create ToolsContext Class
```csharp
public class ClaudeCodeToolsContext : IToolsContext
{
    public List<HazinaChatTool> Tools { get; set; } = new();
    public Action<string, string, string>? SendMessage { get; set; }
    public string? ProjectId { get; set; }
    public Action<string, int, int, string>? OnTokensUsed { get; set; }

    public ClaudeCodeToolsContext(string workingDirectory)
    {
        // Register all tools
        Tools.Add(ReadFileTool.Create(workingDirectory));
        Tools.Add(WriteFileTool.Create(workingDirectory));
        Tools.Add(EditFileTool.Create(workingDirectory));
        Tools.Add(BashTool.Create(workingDirectory));
        Tools.Add(GlobTool.Create(workingDirectory));
        Tools.Add(GrepTool.Create(workingDirectory));
    }

    public void Add(HazinaChatTool info) => Tools.Add(info);
}
```

#### 2.2 Console Output Handler
```csharp
// Print tool calls and results to console
SendMessage = (id, toolName, message) =>
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n[{toolName}]");
    Console.ResetColor();
    Console.WriteLine(message);
};
```

---

### **Phase 3: System Prompt Engineering**

Create Claude Code-style system prompt:

```csharp
var systemPrompt = @"You are Claude Code, a powerful autonomous coding assistant with file system access and command execution capabilities.

AVAILABLE TOOLS:
- read_file: Read file contents with optional line ranges
- write_file: Create or overwrite files
- edit_file: Make precise string replacements in existing files
- bash: Execute PowerShell commands (Windows)
- glob: Find files by pattern (e.g., **/*.cs)
- grep: Search file contents with regex

TOOL USAGE PHILOSOPHY:
1. ALWAYS use tools to inspect before modifying code
2. NEVER guess file contents - read first
3. Use edit_file for existing files, write_file only for new files
4. Run bash commands to test changes
5. Use glob/grep to explore unfamiliar codebases
6. Work autonomously - don't ask permission for tool usage

WORKFLOW:
1. Understand: Read relevant files, explore structure
2. Plan: Think through changes needed
3. Execute: Make precise edits or create files
4. Verify: Run commands to test changes

When user asks to make changes:
- Read the file first
- Make the change with edit_file
- Verify by reading again or running tests

Be concise in explanations. Focus on getting work done with tools.";
```

---

### **Phase 4: Update Program.cs**

Transform the main loop to use tools:

```csharp
var config = OpenAIConfig.Load();
var client = new OpenAIClientWrapper(config);

// Create tools context
var workingDirectory = Directory.GetCurrentDirectory();
var toolsContext = new ClaudeCodeToolsContext(workingDirectory)
{
    SendMessage = (id, toolName, message) =>
    {
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"\n[Tool: {toolName}]");
        Console.ResetColor();
        if (toolName != "grep" && toolName != "glob")
            Console.WriteLine(message.Length > 500
                ? message.Substring(0, 500) + "..."
                : message);
    }
};

var systemPreamble = @"You are Claude Code: an autonomous coding assistant...";

List<HazinaChatMessage> context = new List<HazinaChatMessage>
{
    new() { Role = HazinaMessageRole.System, Text = systemPreamble }
};

async Task RunOnce(string prompt)
{
    context.Add(new HazinaChatMessage
    {
        Role = HazinaMessageRole.User,
        Text = prompt
    });

    var sb = new StringBuilder();
    void OnChunk(string chunk)
    {
        sb.Append(chunk);
        Console.Write(chunk);
    }

    Console.OutputEncoding = Encoding.UTF8;

    // Pass toolsContext to enable function calling
    var response = await client.GetResponseStream(
        context,
        OnChunk,
        HazinaChatResponseFormat.Text,
        toolsContext,  // <-- This enables tool calling!
        images: null,
        CancellationToken.None
    );

    context.Add(new HazinaChatMessage
    {
        Role = HazinaMessageRole.Assistant,
        Text = sb.ToString()
    });

    Console.WriteLine();
}

// Interactive loop
Console.WriteLine("Claude Code (Hazina + OpenAI) — type 'exit' to quit");
while (true)
{
    Console.Write("\n> ");
    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line) ||
        line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;
    await RunOnce(line!);
}
```

---

### **Phase 5: Tool Implementation Details**

#### Safety Constraints (BashTool)
```csharp
private static readonly string[] DangerousPatterns =
{
    "rm -rf", "del /s", "del /q", "remove-item -recurse",
    "format ", "diskpart", "rd /s", "rmdir /s"
};

private static bool IsDestructiveCommand(string command)
{
    return DangerousPatterns.Any(pattern =>
        command.Contains(pattern, StringComparison.OrdinalIgnoreCase));
}
```

#### File Path Resolution
```csharp
private static string ResolvePath(string workingDirectory, string path)
{
    if (Path.IsPathRooted(path))
        return path;
    return Path.GetFullPath(Path.Combine(workingDirectory, path));
}
```

---

## Project Structure

```
src/
├─ Hazina.Agents.Tools/              # New project
│  ├─ Hazina.Agents.Tools.csproj
│  ├─ FileSystem/
│  │  ├─ ReadFileTool.cs
│  │  ├─ WriteFileTool.cs
│  │  ├─ EditFileTool.cs
│  │  ├─ GlobTool.cs
│  │  └─ GrepTool.cs
│  ├─ Execution/
│  │  └─ BashTool.cs
│  ├─ Safety/
│  │  └─ CommandValidator.cs
│  └─ Context/
│     └─ ClaudeCodeToolsContext.cs
│
apps/CLI/Hazina.App.ClaudeCode/
├─ Program.cs                         # Updated with tools
├─ Prompts/
│  └─ SystemPrompts.cs                # Claude Code system prompt
└─ appsettings.json
```

---

## Testing Strategy

### Manual Tests
1. **File Reading:** "Read C:\Projects\hazina\README.md"
2. **File Search:** "Find all .csproj files in this repository"
3. **Content Search:** "Search for 'IProviderOrchestrator' in the src directory"
4. **File Editing:** "Add a comment to line 5 of Program.cs"
5. **Command Execution:** "Run `dotnet build` and show results"
6. **Multi-step Task:** "Find all TODOs in the codebase and list them"

### Verification
- Tool calls appear in console output
- File operations succeed
- Commands execute and return results
- LLM uses tools autonomously without prompting
- Multi-turn tool usage works (tool → LLM → tool → LLM)

---

## Success Criteria

✅ User can ask "what files are in this directory?" and get results via glob tool
✅ User can ask "read the README" and get file contents via read_file tool
✅ User can ask "add error handling to X" and file is edited via edit_file tool
✅ User can ask "run the tests" and command executes via bash tool
✅ User can ask "find all usages of X" and grep tool is used
✅ LLM proactively uses tools without explicit tool names in user prompt
✅ Multi-tool workflows complete successfully (read → edit → run → verify)

---

## Rollout Plan

1. **Day 1:** Create `Hazina.Agents.Tools` project with all 6 tools
2. **Day 2:** Create `ClaudeCodeToolsContext` and test individual tools
3. **Day 3:** Update `Program.cs` with tool integration
4. **Day 4:** System prompt engineering and testing
5. **Day 5:** End-to-end testing and refinement

---

## Alternative: Quick Prototype (Single File)

For rapid iteration, all tools can be defined inline in `Program.cs` as local functions:

```csharp
var readFile = new HazinaChatTool(
    "read_file",
    "Read file contents",
    new List<ChatToolParameter> { ... },
    async (msgs, call, cancel) => { /* implementation */ }
);

toolsContext.Add(readFile);
// ... add other tools
```

This allows faster testing before creating a formal project structure.

---

## Next Steps

1. Create `Hazina.Agents.Tools` project
2. Implement ReadFileTool first (simplest)
3. Test with updated Program.cs
4. Iterate on remaining tools
5. Polish system prompt based on behavior

## Expected Outcome

After implementation, this interaction should work:

```
> can you tell me what you see?

[Tool: glob]
Pattern: **/*

[Tool: read_file]
File: README.md

I can see this is the Hazina repository. The project structure includes:
- Core AI components (Hazina.AI.Providers, Hazina.AI.Agents, Hazina.AI.RAG)
- LLM provider implementations (OpenAI)
- Several CLI applications
- A coding agent implementation in Hazina.Agents.Coding

The main README describes Hazina as an AI orchestration framework...
```
