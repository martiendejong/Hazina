using System.Text;
using Hazina.Agents.Tools.Context;

// Claude Code - Autonomous coding assistant powered by OpenAI + Tools

var config = OpenAIConfig.Load();
var client = new OpenAIClientWrapper(config);

// Create tools context with file and command execution capabilities
var workingDirectory = Directory.GetCurrentDirectory();
var toolsContext = new ClaudeCodeToolsContext(workingDirectory)
{
    SendMessage = (id, toolName, message) =>
    {
        // Print tool calls to console
        Console.ForegroundColor = ConsoleColor.DarkCyan;
        Console.WriteLine($"\n[Tool: {toolName}]");
        Console.ResetColor();

        // For grep/glob, don't show full output (too verbose)
        if (toolName == "grep" || toolName == "glob")
        {
            Console.WriteLine($"(output suppressed - will be sent to LLM)");
        }
        else
        {
            // Show first 800 chars for other tools
            var preview = message.Length > 800
                ? message.Substring(0, 800) + "\n... (truncated)"
                : message;
            Console.WriteLine(preview);
        }
    }
};

var systemPreamble = @"You are Claude Code: a powerful autonomous coding assistant with full file system access and command execution capabilities.

AVAILABLE TOOLS:
- read_file: Read file contents with optional line ranges (offset, limit)
- write_file: Create or overwrite files
- edit_file: Make precise string replacements in existing files
- bash: Execute PowerShell commands (Windows) - UNRESTRICTED ACCESS
- glob: Find files by pattern (e.g., '**/*.cs', '*.json')
- grep: Search file contents with regex patterns

TOOL USAGE PHILOSOPHY:
1. ALWAYS use tools to inspect before modifying - never guess file contents
2. Use read_file FIRST to understand existing code before making changes
3. Use edit_file for surgical modifications to existing files (preserves formatting)
4. Use write_file only for creating NEW files
5. Use bash to run builds, tests, and verify changes
6. Use glob/grep to explore unfamiliar codebases
7. Work AUTONOMOUSLY - use tools proactively without asking permission

WORKFLOW FOR TASKS:
1. UNDERSTAND: Read relevant files, explore structure with glob/grep
2. PLAN: Think through changes needed
3. EXECUTE: Make precise edits or create files
4. VERIFY: Run commands to test changes (build, run tests, etc.)

SURGICAL FILE EDITS:
- When editing files, read them first to get exact strings
- Match whitespace and indentation EXACTLY in old_string
- Use read_file with line ranges to focus on specific sections

COMMAND EXECUTION:
- You have UNRESTRICTED command access
- Run any PowerShell commands needed (build, test, deploy, etc.)
- Always verify your changes by running appropriate commands

Be concise in explanations. Focus on getting work done autonomously with tools. Proactively explore, read, and modify code as needed.";

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

    // CRITICAL: Pass toolsContext to enable function calling!
    var response = await client.GetResponseStream(
        context,
        OnChunk,
        HazinaChatResponseFormat.Text,
        toolsContext,  // <-- This enables tool usage!
        images: null,
        CancellationToken.None
    );

    // Add assistant response to context
    var assistantMessage = sb.ToString();
    if (!string.IsNullOrWhiteSpace(assistantMessage))
    {
        context.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.Assistant,
            Text = assistantMessage
        });
    }

    Console.WriteLine();
}

// Single command mode
if (args.Length > 0)
{
    await RunOnce(string.Join(" ", args));
    return;
}

// Interactive mode
Console.WriteLine("Claude Code (Hazina + OpenAI) — type 'exit' to quit");
Console.WriteLine($"Working Directory: {workingDirectory}");
Console.WriteLine();

while (true)
{
    Console.ForegroundColor = ConsoleColor.Green;
    Console.Write("> ");
    Console.ResetColor();

    var line = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(line) || line.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
        break;

    await RunOnce(line!);
}
