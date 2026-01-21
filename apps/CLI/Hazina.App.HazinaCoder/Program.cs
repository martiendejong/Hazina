using System.CommandLine;
using System.Text;
using Spectre.Console;
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Hazina.LLMs.Anthropic;
using Hazina.Agents.Tools.Context;

// HazinaCoder - Multi-provider coding assistant CLI
// Supports: OpenAI, Anthropic Claude, Ollama (local), and more

var rootCommand = new RootCommand("HazinaCoder - Multi-provider coding assistant powered by Hazina AI")
{
    new Option<string>("--provider", () => "auto", "LLM provider: openai, anthropic, ollama, or auto"),
    new Option<string>("--model", "Model override (provider-specific)"),
    new Option<string>("--working-dir", () => Directory.GetCurrentDirectory(), "Working directory for file operations"),
    new Option<bool>("--verbose", () => false, "Enable verbose output"),
    new Argument<string[]>("prompt", () => Array.Empty<string>(), "Direct prompt (non-interactive mode)")
};

rootCommand.SetHandler(async (string provider, string? model, string workingDir, bool verbose, string[] promptArgs) =>
{
    var cli = new HazinaCoderCLI(provider, model, workingDir, verbose);
    await cli.Run(promptArgs);
},
rootCommand.Options.OfType<Option<string>>().First(o => o.Name == "provider"),
rootCommand.Options.OfType<Option<string>>().First(o => o.Name == "model"),
rootCommand.Options.OfType<Option<string>>().First(o => o.Name == "working-dir"),
rootCommand.Options.OfType<Option<bool>>().First(o => o.Name == "verbose"),
rootCommand.Arguments.OfType<Argument<string[]>>().First());

return await rootCommand.InvokeAsync(args);

class HazinaCoderCLI
{
    private string _providerName;
    private string? _modelOverride;
    private string _workingDirectory;
    private bool _verbose;
    private ILLMClient _client = null!;
    private string _model = "";
    private IToolsContext _toolsContext = null!;
    private List<HazinaChatMessage> _context = new();
    private decimal _sessionCost = 0m;
    private int _sessionTokens = 0;
    private string? _claudeMdContent;
    private List<SkillInfo> _skills = new();
    private OutputMode _outputMode = OutputMode.Full; // Default to full output like Claude Code

    private enum OutputMode
    {
        Full,      // Show everything
        Compact,   // Show up to 2000 chars
        Minimal    // Show up to 400 chars
    }

    public HazinaCoderCLI(string provider, string? model, string workingDir, bool verbose)
    {
        _providerName = provider;
        _modelOverride = model;
        _workingDirectory = workingDir;
        _verbose = verbose;
    }

    public async Task Run(string[] promptArgs)
    {
        // Validate and normalize working directory
        if (string.IsNullOrWhiteSpace(_workingDirectory))
        {
            _workingDirectory = Directory.GetCurrentDirectory();
        }
        else
        {
            _workingDirectory = Path.GetFullPath(_workingDirectory);
        }

        if (!Directory.Exists(_workingDirectory))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] Working directory does not exist: {Markup.Escape(_workingDirectory)}");
            return;
        }

        // Load CLAUDE.md if present
        _claudeMdContent = LoadClaudeMd();

        // Load skills from .claude/skills/
        _skills = LoadSkills();

        // Detect provider from environment if auto
        if (_providerName == "auto")
        {
            _providerName = DetectProvider();
        }

        // Create LLM client based on provider
        try
        {
            (_client, _model) = CreateClient(_providerName, _modelOverride);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error creating {_providerName} client:[/] {Markup.Escape(ex.Message)}");
            AnsiConsole.MarkupLine("[yellow]Tip:[/] Set the appropriate API key environment variable:");
            AnsiConsole.MarkupLine("  - OpenAI: OPENAI_API_KEY");
            AnsiConsole.MarkupLine("  - Anthropic: ANTHROPIC_API_KEY");
            AnsiConsole.MarkupLine("  - Ollama: No key needed (local)");
            return;
        }

        // Create tools context with extended tools
        _toolsContext = new HazinaCoderToolsContext(_workingDirectory)
        {
            SendMessage = (id, toolName, message) =>
            {
                AnsiConsole.MarkupLine($"\n[cyan][[Tool: {Markup.Escape(toolName)}]][/]");
                DisplayToolOutput(toolName, message);
            }
        };

        // System prompt - Claude Code style
        var systemPreamble = BuildSystemPrompt();

        _context = new List<HazinaChatMessage>
        {
            new() { Role = HazinaMessageRole.System, Text = systemPreamble }
        };

        // Single command mode
        if (promptArgs.Length > 0)
        {
            await RunOnce(string.Join(" ", promptArgs));
            return;
        }

        // Interactive mode
        var rule = new Rule($"[green]HazinaCoder[/] [dim]({_providerName}/{_model})[/]");
        rule.Justification = Justify.Left;
        AnsiConsole.Write(rule);
        AnsiConsole.MarkupLine($"[dim]Working Directory:[/] {_workingDirectory}");
        if (_claudeMdContent != null)
        {
            AnsiConsole.MarkupLine("[green]✓[/] [dim]CLAUDE.md loaded[/]");
        }
        if (_skills.Count > 0)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] [dim]{_skills.Count} skill(s) loaded[/]");
        }
        AnsiConsole.MarkupLine($"[dim]Output: {_outputMode} (use /output to cycle)[/]");
        AnsiConsole.MarkupLine("[dim]Commands: /help, /output, /tools, /skills, /cost, /clear, /exit[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            AnsiConsole.Markup("[green]> [/]");
            var line = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Handle slash commands
            if (line.StartsWith("/"))
            {
                var result = HandleCommand(line);
                if (result == CommandResult.Exit)
                    break;
                if (result == CommandResult.Handled)
                    continue;
            }

            await RunOnce(line);
        }
    }

    private async Task RunOnce(string prompt)
    {
        _context.Add(new HazinaChatMessage
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

        try
        {
            var response = await _client.GetResponseStream(
                _context,
                OnChunk,
                HazinaChatResponseFormat.Text,
                _toolsContext,
                images: null,
                CancellationToken.None
            );

            // Track usage
            if (response.TokenUsage != null)
            {
                _sessionCost += response.TokenUsage.TotalCost;
                _sessionTokens += response.TokenUsage.TotalTokens;
            }

            // Add assistant response to context
            var assistantMessage = sb.ToString();
            if (!string.IsNullOrWhiteSpace(assistantMessage))
            {
                _context.Add(new HazinaChatMessage
                {
                    Role = HazinaMessageRole.Assistant,
                    Text = assistantMessage
                });
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}");
        }

        Console.WriteLine();
    }

    private void DisplayToolOutput(string toolName, string message)
    {
        // For search tools, always show summary in non-full modes
        var isSearchTool = toolName == "grep" || toolName == "glob";

        switch (_outputMode)
        {
            case OutputMode.Full:
                // Show everything
                AnsiConsole.WriteLine(message);
                break;

            case OutputMode.Compact:
                if (isSearchTool && message.Length > 2000)
                {
                    // For search tools, show count and preview
                    var lines = message.Split('\n');
                    AnsiConsole.MarkupLine($"[dim]({lines.Length} lines, showing first 2000 chars)[/]");
                    AnsiConsole.WriteLine(message.Substring(0, 2000) + "\n...");
                }
                else if (message.Length > 2000)
                {
                    AnsiConsole.WriteLine(message.Substring(0, 2000) + "\n... (truncated, use /full for complete output)");
                }
                else
                {
                    AnsiConsole.WriteLine(message);
                }
                break;

            case OutputMode.Minimal:
                if (isSearchTool)
                {
                    var lines = message.Split('\n');
                    AnsiConsole.MarkupLine($"[dim]({lines.Length} results - use /output to see more)[/]");
                }
                else if (message.Length > 400)
                {
                    AnsiConsole.WriteLine(message.Substring(0, 400) + "\n... (truncated, use /output to cycle modes)");
                }
                else
                {
                    AnsiConsole.WriteLine(message);
                }
                break;
        }
    }

    private string DetectProvider()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")))
            return "anthropic";
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
            return "openai";
        // Default to ollama (local) if no API keys found
        return "ollama";
    }

    private (ILLMClient client, string model) CreateClient(string provider, string? modelOverride)
    {
        switch (provider.ToLower())
        {
            case "openai":
                var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                    ?? throw new Exception("OPENAI_API_KEY environment variable not set");
                var openAiModel = modelOverride ?? "gpt-4o";
                var openAiConfig = new OpenAIConfig
                {
                    ApiKey = openAiKey,
                    Model = openAiModel
                };
                return (new OpenAIClientWrapper(openAiConfig), openAiModel);

            case "anthropic":
            case "claude":
                var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY")
                    ?? throw new Exception("ANTHROPIC_API_KEY environment variable not set");
                var anthropicModel = modelOverride ?? "claude-sonnet-4-20250514";
                var anthropicConfig = new AnthropicConfig
                {
                    ApiKey = anthropicKey,
                    Model = anthropicModel,
                    Endpoint = "https://api.anthropic.com",
                    ApiVersion = "2023-06-01"
                };
                return (new ClaudeClientWrapper(anthropicConfig), anthropicModel);

            case "ollama":
            case "local":
                var ollamaEndpoint = Environment.GetEnvironmentVariable("OLLAMA_ENDPOINT") ?? "http://localhost:11434";
                var ollamaModel = modelOverride ?? "llama3.1";
                var ollamaConfig = new OllamaConfig
                {
                    Endpoint = ollamaEndpoint,
                    Model = ollamaModel
                };
                return (new OllamaClientWrapper(ollamaConfig), ollamaModel);

            default:
                throw new Exception($"Unknown provider: {provider}. Supported: openai, anthropic, ollama");
        }
    }

    private CommandResult HandleCommand(string line)
    {
        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();
        var arg = parts.Length > 1 ? parts[1] : null;

        switch (command)
        {
            case "/exit":
            case "/quit":
            case "/q":
                AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                return CommandResult.Exit;

            case "/help":
            case "/?":
                ShowHelp();
                return CommandResult.Handled;

            case "/clear":
                _context.RemoveRange(1, _context.Count - 1); // Keep system prompt
                AnsiConsole.MarkupLine("[green]Conversation cleared.[/]");
                return CommandResult.Handled;

            case "/cost":
                AnsiConsole.MarkupLine($"[cyan]Session Stats:[/]");
                AnsiConsole.MarkupLine($"  Tokens: {_sessionTokens:N0}");
                AnsiConsole.MarkupLine($"  Cost: ${_sessionCost:F4}");
                return CommandResult.Handled;

            case "/provider":
                if (string.IsNullOrEmpty(arg))
                {
                    AnsiConsole.MarkupLine($"[cyan]Current provider:[/] {_providerName}");
                    AnsiConsole.MarkupLine("[dim]Usage: /provider <openai|anthropic|ollama>[/]");
                }
                else
                {
                    try
                    {
                        (_client, _model) = CreateClient(arg, _modelOverride);
                        _providerName = arg;
                        AnsiConsole.MarkupLine($"[green]Switched to {_providerName}/{_model}[/]");
                        // Update system prompt with new provider info
                        if (_context.Count > 0 && _context[0].Role == HazinaMessageRole.System)
                        {
                            _context[0].Text = _context[0].Text.Replace(
                                _context[0].Text.Split('\n')[1],
                                $"Provider: {_providerName} | Model: {_model}");
                        }
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to switch provider:[/] {Markup.Escape(ex.Message)}");
                    }
                }
                return CommandResult.Handled;

            case "/model":
                if (string.IsNullOrEmpty(arg))
                {
                    AnsiConsole.MarkupLine($"[cyan]Current model:[/] {_model}");
                    AnsiConsole.MarkupLine("[dim]Usage: /model <model-name>[/]");
                }
                else
                {
                    try
                    {
                        (_client, _model) = CreateClient(_providerName, arg);
                        AnsiConsole.MarkupLine($"[green]Switched to {_providerName}/{_model}[/]");
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[red]Failed to switch model:[/] {Markup.Escape(ex.Message)}");
                    }
                }
                return CommandResult.Handled;

            case "/tools":
                AnsiConsole.MarkupLine("[cyan]Available Tools:[/]");
                AnsiConsole.MarkupLine("  [yellow]File System:[/]");
                AnsiConsole.MarkupLine("    read_file      - Read file contents with line ranges");
                AnsiConsole.MarkupLine("    write_file     - Create/overwrite files");
                AnsiConsole.MarkupLine("    edit_file      - Surgical string replacement");
                AnsiConsole.MarkupLine("    glob           - Find files by pattern");
                AnsiConsole.MarkupLine("    grep           - Search file contents with regex");
                AnsiConsole.MarkupLine("    list_directory - List directory contents with details");
                AnsiConsole.MarkupLine("  [yellow]Execution:[/]");
                AnsiConsole.MarkupLine("    bash           - Execute shell commands");
                AnsiConsole.MarkupLine("  [yellow]Git:[/]");
                AnsiConsole.MarkupLine("    git_status     - Get repository status and commits");
                AnsiConsole.MarkupLine("  [yellow]Web:[/]");
                AnsiConsole.MarkupLine("    web_fetch      - Fetch and parse web content");
                AnsiConsole.MarkupLine("  [yellow]Task Management:[/]");
                AnsiConsole.MarkupLine("    todo_write     - Track tasks during coding session");
                return CommandResult.Handled;

            case "/skills":
                if (_skills.Count == 0)
                {
                    AnsiConsole.MarkupLine("[dim]No skills loaded. Add skills to .claude/skills/<name>/SKILL.md[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[cyan]Loaded Skills ({_skills.Count}):[/]");
                    foreach (var skill in _skills)
                    {
                        AnsiConsole.MarkupLine($"  [yellow]{skill.Name}[/]");
                        AnsiConsole.MarkupLine($"    [dim]{skill.Description}[/]");
                    }
                }
                return CommandResult.Handled;

            case "/context":
                AnsiConsole.MarkupLine($"[cyan]Context:[/] {_context.Count} messages");
                var totalChars = _context.Sum(m => m.Text?.Length ?? 0);
                AnsiConsole.MarkupLine($"[dim]~{totalChars:N0} characters[/]");
                return CommandResult.Handled;

            case "/output":
            case "/o":
                // Cycle through output modes: Full -> Compact -> Minimal -> Full
                _outputMode = _outputMode switch
                {
                    OutputMode.Full => OutputMode.Compact,
                    OutputMode.Compact => OutputMode.Minimal,
                    OutputMode.Minimal => OutputMode.Full,
                    _ => OutputMode.Full
                };
                var modeDesc = _outputMode switch
                {
                    OutputMode.Full => "Full (show all tool output)",
                    OutputMode.Compact => "Compact (up to 2000 chars)",
                    OutputMode.Minimal => "Minimal (up to 400 chars)",
                    _ => _outputMode.ToString()
                };
                AnsiConsole.MarkupLine($"[green]Output mode:[/] {modeDesc}");
                return CommandResult.Handled;

            case "/full":
                _outputMode = OutputMode.Full;
                AnsiConsole.MarkupLine("[green]Output mode:[/] Full (show all tool output)");
                return CommandResult.Handled;

            case "/compact":
                _outputMode = OutputMode.Compact;
                AnsiConsole.MarkupLine("[green]Output mode:[/] Compact (up to 2000 chars)");
                return CommandResult.Handled;

            case "/minimal":
                _outputMode = OutputMode.Minimal;
                AnsiConsole.MarkupLine("[green]Output mode:[/] Minimal (up to 400 chars)");
                return CommandResult.Handled;

            default:
                // Not a recognized command, treat as regular input
                return CommandResult.NotHandled;
        }
    }

    private void ShowHelp()
    {
        var table = new Table();
        table.AddColumn("Command");
        table.AddColumn("Description");
        table.Border = TableBorder.Rounded;

        table.AddRow("/help", "Show this help");
        table.AddRow("/output, /o", "Cycle output mode: Full → Compact → Minimal");
        table.AddRow("/full", "Show full tool output (no truncation)");
        table.AddRow("/compact", "Show up to 2000 chars per tool");
        table.AddRow("/minimal", "Show up to 400 chars per tool");
        table.AddRow("/provider <name>", "Switch provider (openai, anthropic, ollama)");
        table.AddRow("/model <name>", "Switch model");
        table.AddRow("/tools", "List available tools");
        table.AddRow("/skills", "List loaded skills from .claude/skills/");
        table.AddRow("/cost", "Show session cost and token usage");
        table.AddRow("/context", "Show context size");
        table.AddRow("/clear", "Clear conversation history");
        table.AddRow("/exit", "Exit HazinaCoder");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]HazinaCoder automatically loads:[/]");
        AnsiConsole.MarkupLine("[dim]  - CLAUDE.md from working directory (project instructions)[/]");
        AnsiConsole.MarkupLine("[dim]  - Skills from .claude/skills/<name>/SKILL.md[/]");
    }

    private enum CommandResult { Handled, NotHandled, Exit }

    private string? LoadClaudeMd()
    {
        // Search for CLAUDE.md in working directory and parent directories
        var searchPaths = new[]
        {
            Path.Combine(_workingDirectory, "CLAUDE.md"),
            Path.Combine(_workingDirectory, "claude.md"),
            Path.Combine(_workingDirectory, ".claude", "CLAUDE.md"),
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var content = File.ReadAllText(path);
                    if (_verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]Loaded: {path}[/]");
                    }
                    return content;
                }
                catch
                {
                    // Ignore read errors
                }
            }
        }

        return null;
    }

    private List<SkillInfo> LoadSkills()
    {
        var skills = new List<SkillInfo>();
        var skillsDir = Path.Combine(_workingDirectory, ".claude", "skills");

        if (!Directory.Exists(skillsDir))
            return skills;

        try
        {
            foreach (var dir in Directory.GetDirectories(skillsDir))
            {
                var skillFile = Path.Combine(dir, "SKILL.md");
                if (File.Exists(skillFile))
                {
                    try
                    {
                        var content = File.ReadAllText(skillFile);
                        var name = Path.GetFileName(dir);
                        var description = ExtractSkillDescription(content);

                        skills.Add(new SkillInfo
                        {
                            Name = name,
                            Description = description,
                            Content = content,
                            Path = skillFile
                        });

                        if (_verbose)
                        {
                            AnsiConsole.MarkupLine($"[dim]Loaded skill: {name}[/]");
                        }
                    }
                    catch
                    {
                        // Ignore individual skill load errors
                    }
                }
            }
        }
        catch
        {
            // Ignore directory enumeration errors
        }

        return skills;
    }

    private static string ExtractSkillDescription(string content)
    {
        // Try to extract description from YAML frontmatter or first paragraph
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
            {
                return line.Substring("description:".Length).Trim().Trim('"', '\'');
            }
        }

        // Fall back to first non-empty, non-header line
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("#") && !trimmed.StartsWith("---"))
            {
                return trimmed.Length > 100 ? trimmed.Substring(0, 100) + "..." : trimmed;
            }
        }

        return "No description available";
    }

    private string BuildSystemPrompt()
    {
        var sb = new StringBuilder();

        sb.AppendLine($@"You are HazinaCoder, an autonomous coding assistant powered by the Hazina AI framework.
Provider: {_providerName} | Model: {_model}
Working Directory: {_workingDirectory}

You are an interactive CLI tool that helps users with software engineering tasks. Use the tools available to you to assist the user.

# Core Principles

1. **Read Before Edit**: NEVER propose changes to code you haven't read. Always use read_file first.
2. **Autonomous Execution**: Execute tasks without asking for permission unless destructive/irreversible.
3. **Verify Changes**: Always run builds, tests, or other verification after making changes.
4. **Be Concise**: Focus on getting work done. Explanations should be brief and actionable.

# Available Tools

## File Operations
- **read_file**: Read file contents with optional line ranges (offset, limit)
- **write_file**: Create or overwrite files (use only for NEW files)
- **edit_file**: Make precise string replacements in existing files
- **glob**: Find files by pattern (e.g., '**/*.cs', '*.json')
- **grep**: Search file contents with regex patterns
- **list_directory**: List directory contents with details

## Execution
- **bash**: Execute shell commands (PowerShell on Windows, bash on Unix)

## Git
- **git_status**: Get structured git status (branch, changes, recent commits)

## Web
- **web_fetch**: Fetch content from URLs (strips HTML for readability)

## Task Management
- **todo_write**: Track tasks during your coding session. Use this for complex multi-step tasks.

# Task Management Guidelines

Use the todo_write tool when:
- Task requires 3+ distinct steps
- User provides multiple tasks
- You need to track progress on complex work

Mark todos as completed IMMEDIATELY after finishing each task.

# Workflow

1. **UNDERSTAND**: Read relevant files, explore with glob/grep
2. **PLAN**: Use todo_write to break down complex tasks
3. **EXECUTE**: Make precise edits, create files as needed
4. **VERIFY**: Run builds, tests, verify changes work

# File Editing

- Read the file FIRST to get exact strings
- Match whitespace and indentation EXACTLY
- The edit will FAIL if old_string is not unique - provide more context if needed
- Prefer editing existing files over creating new ones");

        // Add CLAUDE.md content if present
        if (!string.IsNullOrEmpty(_claudeMdContent))
        {
            sb.AppendLine();
            sb.AppendLine("# Project Instructions (from CLAUDE.md)");
            sb.AppendLine();
            sb.AppendLine(_claudeMdContent);
        }

        // Add skills if present
        if (_skills.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("# Available Skills");
            sb.AppendLine();
            sb.AppendLine("The following skills are available. When a task matches a skill's description, follow its instructions:");
            sb.AppendLine();

            foreach (var skill in _skills)
            {
                sb.AppendLine($"## Skill: {skill.Name}");
                sb.AppendLine($"Description: {skill.Description}");
                sb.AppendLine();
                sb.AppendLine("```markdown");
                // Truncate very long skills
                var content = skill.Content.Length > 3000
                    ? skill.Content.Substring(0, 3000) + "\n... (truncated)"
                    : skill.Content;
                sb.AppendLine(content);
                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        // Add environment info
        sb.AppendLine();
        sb.AppendLine("# Environment");
        sb.AppendLine();
        sb.AppendLine($"- Platform: {Environment.OSVersion.Platform}");
        sb.AppendLine($"- Working Directory: {_workingDirectory}");
        sb.AppendLine($"- Date: {DateTime.Now:yyyy-MM-dd}");

        return sb.ToString();
    }
}

class SkillInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    public string Path { get; set; } = "";
}
