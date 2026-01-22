using System.CommandLine;
using System.Text;
using Spectre.Console;
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Hazina.LLMs.Anthropic;
using Hazina.Agents.Tools.Context;
using Hazina.Agents.Tools.Mcp;

// HazinaCoder - Multi-provider coding assistant CLI
// Supports: OpenAI, Anthropic Claude, Ollama (local), and more

var providerOpt = new Option<string>("--provider", () => "auto", "LLM provider: openai, anthropic, ollama, or auto");
var modelOpt = new Option<string?>("--model", "Model override (provider-specific)");
var workingDirOpt = new Option<string>("--working-dir", () => Directory.GetCurrentDirectory(), "Working directory for file operations");
var verboseOpt = new Option<bool>("--verbose", () => false, "Enable verbose output");
var maxTurnsOpt = new Option<int>("--max-turns", () => 50, "Maximum tool calls per conversation turn (default: 50)");
var machineContextOpt = new Option<string?>("--machine-context", "Path to machine context directory (e.g., C:\\scripts\\_machine)");
var reflectionLogOpt = new Option<string?>("--reflection-log", "Path to reflection log file for learned patterns");
var loadGitOpt = new Option<bool>("--load-git", () => true, "Load git status at startup (default: true)");
var loadMcpOpt = new Option<bool>("--load-mcp", () => true, "Load MCP servers from settings (default: true)");
var mcpSettingsOpt = new Option<string?>("--mcp-settings", "Path to MCP settings file (default: auto-discover)");
var promptArg = new Argument<string[]>("prompt", () => Array.Empty<string>(), "Direct prompt (non-interactive mode)");

var rootCommand = new RootCommand("HazinaCoder - Multi-provider coding assistant powered by Hazina AI")
{
    providerOpt, modelOpt, workingDirOpt, verboseOpt, maxTurnsOpt,
    machineContextOpt, reflectionLogOpt, loadGitOpt, loadMcpOpt, mcpSettingsOpt, promptArg
};

rootCommand.SetHandler(async (context) =>
{
    var provider = context.ParseResult.GetValueForOption(providerOpt)!;
    var model = context.ParseResult.GetValueForOption(modelOpt);
    var workingDir = context.ParseResult.GetValueForOption(workingDirOpt)!;
    var verbose = context.ParseResult.GetValueForOption(verboseOpt);
    var maxTurns = context.ParseResult.GetValueForOption(maxTurnsOpt);
    var machineContext = context.ParseResult.GetValueForOption(machineContextOpt);
    var reflectionLog = context.ParseResult.GetValueForOption(reflectionLogOpt);
    var loadGit = context.ParseResult.GetValueForOption(loadGitOpt);
    var loadMcp = context.ParseResult.GetValueForOption(loadMcpOpt);
    var mcpSettings = context.ParseResult.GetValueForOption(mcpSettingsOpt);
    var promptArgs = context.ParseResult.GetValueForArgument(promptArg);

    var cli = new HazinaCoderCLI(provider, model, workingDir, verbose, maxTurns, machineContext, reflectionLog, loadGit, loadMcp, mcpSettings);
    await cli.Run(promptArgs);
});

return await rootCommand.InvokeAsync(args);

class HazinaCoderCLI : IDisposable
{
    private string _providerName;
    private string? _modelOverride;
    private string _workingDirectory;
    private bool _verbose;
    private int _maxTurns;
    private int _currentTurnCount = 0;
    private string? _machineContextPath;
    private string? _reflectionLogPath;
    private bool _loadGit;
    private bool _loadMcp;
    private string? _mcpSettingsPath;
    private string? _gitStatusInfo;
    private string? _machineContextContent;
    private string? _reflectionLogContent;
    private ILLMClient _client = null!;
    private string _model = "";
    private HazinaCoderToolsContext _toolsContext = null!;
    private McpManager? _mcpManager;
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

    public HazinaCoderCLI(string provider, string? model, string workingDir, bool verbose, int maxTurns = 50,
        string? machineContext = null, string? reflectionLog = null, bool loadGit = true,
        bool loadMcp = true, string? mcpSettings = null)
    {
        _providerName = provider;
        _modelOverride = model;
        _workingDirectory = workingDir;
        _verbose = verbose;
        _maxTurns = maxTurns;
        _machineContextPath = machineContext;
        _reflectionLogPath = reflectionLog;
        _loadGit = loadGit;
        _loadMcp = loadMcp;
        _mcpSettingsPath = mcpSettings;
    }

    public void Dispose()
    {
        _mcpManager?.Dispose();
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

        // Load enhanced context (Phase 4B)
        _gitStatusInfo = LoadGitStatus();
        _machineContextContent = LoadMachineContext();
        _reflectionLogContent = LoadReflectionLog();

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
                _currentTurnCount++;
                var turnDisplay = _verbose ? $" (turn {_currentTurnCount}/{_maxTurns})" : "";
                AnsiConsole.MarkupLine($"\n[cyan][[Tool: {Markup.Escape(toolName)}{turnDisplay}]][/]");
                DisplayToolOutput(toolName, message);
            }
        };

        // Load MCP servers (Phase 4C)
        if (_loadMcp)
        {
            await LoadMcpServersAsync();
        }

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
        if (_gitStatusInfo != null)
        {
            AnsiConsole.MarkupLine("[green]✓[/] [dim]Git status loaded[/]");
        }
        if (_machineContextContent != null)
        {
            AnsiConsole.MarkupLine("[green]✓[/] [dim]Machine context loaded[/]");
        }
        if (_reflectionLogContent != null)
        {
            AnsiConsole.MarkupLine("[green]✓[/] [dim]Reflection log loaded[/]");
        }
        if (_mcpManager != null && _mcpManager.ConnectedServers > 0)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] [dim]{_mcpManager.ConnectedServers} MCP server(s), {_mcpManager.Tools.Count} tools[/]");
        }
        AnsiConsole.MarkupLine($"[dim]Output: {_outputMode} | Permissions: {(_toolsContext.EnablePermissions ? "ON" : "OFF")}[/]");
        AnsiConsole.MarkupLine("[dim]Commands: /help, /output, /permissions, /tools, /skills, /clear, /exit[/]");
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
        // Reset turn counter for this conversation turn
        var startTurnCount = _currentTurnCount;

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

            // Show summary of tool calls made
            var toolCallsThisTurn = _currentTurnCount - startTurnCount;
            if (toolCallsThisTurn > 0 && _verbose)
            {
                AnsiConsole.MarkupLine($"\n[dim]({toolCallsThisTurn} tool call(s) | ${_sessionCost:F4} | {_sessionTokens:N0} tokens)[/]");
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
            case "/status":
                AnsiConsole.MarkupLine($"[cyan]Session Stats:[/]");
                AnsiConsole.MarkupLine($"  Provider: {_providerName}/{_model}");
                AnsiConsole.MarkupLine($"  Tool Calls: {_currentTurnCount} (max: {_maxTurns})");
                AnsiConsole.MarkupLine($"  Tokens: {_sessionTokens:N0}");
                AnsiConsole.MarkupLine($"  Cost: ${_sessionCost:F4}");
                AnsiConsole.MarkupLine($"  Context: {_context.Count} messages");
                AnsiConsole.MarkupLine($"  Output Mode: {_outputMode}");
                AnsiConsole.MarkupLine($"  Permissions: {(_toolsContext.EnablePermissions ? "ON" : "OFF")}");
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
                AnsiConsole.MarkupLine("    notebook_edit  - Edit Jupyter notebook cells");
                AnsiConsole.MarkupLine("  [yellow]Execution:[/]");
                AnsiConsole.MarkupLine("    bash           - Execute shell commands (with permission checks)");
                AnsiConsole.MarkupLine("    bash_background- Run commands in background");
                AnsiConsole.MarkupLine("    task_output    - Get background task status/output");
                AnsiConsole.MarkupLine("  [yellow]Git:[/]");
                AnsiConsole.MarkupLine("    git_status     - Get repository status and commits");
                AnsiConsole.MarkupLine("  [yellow]Web:[/]");
                AnsiConsole.MarkupLine("    web_fetch      - Fetch and parse web content");
                AnsiConsole.MarkupLine("    web_search     - Search the web (DuckDuckGo)");
                AnsiConsole.MarkupLine("  [yellow]Task Management:[/]");
                AnsiConsole.MarkupLine("    todo_write     - Track tasks during coding session");
                AnsiConsole.MarkupLine("  [yellow]User Interaction:[/]");
                AnsiConsole.MarkupLine("    ask_user       - Ask user for clarification or decisions");
                AnsiConsole.MarkupLine("  [yellow]Plan Mode:[/]");
                AnsiConsole.MarkupLine("    enter_plan_mode- Start structured planning before implementation");
                AnsiConsole.MarkupLine("    exit_plan_mode - Present plan for user approval");
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

            case "/mcp":
                if (_mcpManager == null || _mcpManager.ConnectedServers == 0)
                {
                    AnsiConsole.MarkupLine("[dim]No MCP servers connected.[/]");
                    AnsiConsole.MarkupLine("[dim]Add MCP servers to .claude/settings.json or mcp-settings.json[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[cyan]MCP Servers ({_mcpManager.ConnectedServers}):[/]");
                    foreach (var (serverName, toolName, description) in _mcpManager.ListAllTools())
                    {
                        AnsiConsole.MarkupLine($"  [yellow]{serverName}[/] → {toolName}");
                        if (!string.IsNullOrEmpty(description))
                        {
                            var descPreview = description.Length > 80 ? description.Substring(0, 80) + "..." : description;
                            AnsiConsole.MarkupLine($"    [dim]{Markup.Escape(descPreview)}[/]");
                        }
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

            case "/permissions":
            case "/perm":
                _toolsContext.EnablePermissions = !_toolsContext.EnablePermissions;
                if (_toolsContext.EnablePermissions)
                {
                    AnsiConsole.MarkupLine("[green]Permissions:[/] ON - Dangerous commands require approval");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]Permissions:[/] OFF - All commands run without approval");
                }
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
        table.AddRow("/permissions, /perm", "Toggle permission checks for dangerous commands");
        table.AddRow("/provider <name>", "Switch provider (openai, anthropic, ollama)");
        table.AddRow("/model <name>", "Switch model");
        table.AddRow("/tools", "List available tools (13 total)");
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

    private string? LoadGitStatus()
    {
        if (!_loadGit)
            return null;

        try
        {
            var sb = new StringBuilder();

            // Check if we're in a git repo
            var gitDir = Path.Combine(_workingDirectory, ".git");
            if (!Directory.Exists(gitDir) && !File.Exists(gitDir)) // .git can be a file for worktrees
            {
                return null;
            }

            // Get current branch
            var branchResult = RunGitCommand("rev-parse --abbrev-ref HEAD");
            if (branchResult != null)
            {
                sb.AppendLine($"Current Branch: {branchResult.Trim()}");
            }

            // Get status
            var statusResult = RunGitCommand("status --porcelain");
            if (!string.IsNullOrWhiteSpace(statusResult))
            {
                var lines = statusResult.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                sb.AppendLine($"Changed Files: {lines.Length}");
                if (lines.Length <= 10)
                {
                    foreach (var line in lines)
                    {
                        sb.AppendLine($"  {line.Trim()}");
                    }
                }
                else
                {
                    foreach (var line in lines.Take(10))
                    {
                        sb.AppendLine($"  {line.Trim()}");
                    }
                    sb.AppendLine($"  ... and {lines.Length - 10} more");
                }
            }
            else
            {
                sb.AppendLine("Working tree clean");
            }

            // Get recent commits
            var logResult = RunGitCommand("log --oneline -5");
            if (!string.IsNullOrWhiteSpace(logResult))
            {
                sb.AppendLine("\nRecent Commits:");
                foreach (var line in logResult.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(5))
                {
                    sb.AppendLine($"  {line.Trim()}");
                }
            }

            if (_verbose)
            {
                AnsiConsole.MarkupLine("[dim]Loaded: git status[/]");
            }

            return sb.ToString();
        }
        catch
        {
            return null;
        }
    }

    private string? RunGitCommand(string args)
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = _workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null) return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return process.ExitCode == 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    private string? LoadMachineContext()
    {
        if (string.IsNullOrWhiteSpace(_machineContextPath) || !Directory.Exists(_machineContextPath))
            return null;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Machine Context");
            sb.AppendLine();

            // Load key files from machine context
            var importantFiles = new[]
            {
                "worktrees.pool.md",
                "pr-dependencies.md",
                "DEFINITION_OF_DONE.md",
                "SOFTWARE_DEVELOPMENT_PRINCIPLES.md",
                "PERSONAL_INSIGHTS.md"
            };

            foreach (var fileName in importantFiles)
            {
                var filePath = Path.Combine(_machineContextPath, fileName);
                if (File.Exists(filePath))
                {
                    try
                    {
                        var content = File.ReadAllText(filePath);
                        // Truncate large files
                        if (content.Length > 5000)
                        {
                            content = content.Substring(0, 5000) + "\n... (truncated)";
                        }
                        sb.AppendLine($"## {fileName}");
                        sb.AppendLine(content);
                        sb.AppendLine();
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }

            if (_verbose)
            {
                AnsiConsole.MarkupLine($"[dim]Loaded: machine context from {_machineContextPath}[/]");
            }

            return sb.Length > 20 ? sb.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    private string? LoadReflectionLog()
    {
        var path = _reflectionLogPath;

        // Try default location if not specified
        if (string.IsNullOrWhiteSpace(path) && !string.IsNullOrWhiteSpace(_machineContextPath))
        {
            path = Path.Combine(_machineContextPath, "reflection.log.md");
        }

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var content = File.ReadAllText(path);

            // Get last ~50 entries or 10KB, whichever is smaller
            if (content.Length > 10000)
            {
                // Find a good cutoff point
                var cutoff = content.Length - 10000;
                var newlinePos = content.IndexOf('\n', cutoff);
                if (newlinePos > 0)
                {
                    content = "... (earlier entries truncated)\n\n" + content.Substring(newlinePos + 1);
                }
                else
                {
                    content = content.Substring(cutoff);
                }
            }

            if (_verbose)
            {
                AnsiConsole.MarkupLine($"[dim]Loaded: reflection log[/]");
            }

            return content;
        }
        catch
        {
            return null;
        }
    }

    private async Task LoadMcpServersAsync()
    {
        try
        {
            // Find MCP settings
            McpSettings? settings = null;

            if (!string.IsNullOrWhiteSpace(_mcpSettingsPath))
            {
                settings = McpSettings.LoadFromFile(_mcpSettingsPath);
            }
            else
            {
                settings = McpSettings.FindSettings(_workingDirectory);
            }

            if (settings == null || settings.McpServers.Count == 0)
            {
                if (_verbose)
                {
                    AnsiConsole.MarkupLine("[dim]No MCP servers configured[/]");
                }
                return;
            }

            // Create MCP manager and connect to servers
            _mcpManager = new McpManager(_verbose);
            await _mcpManager.LoadServersAsync(settings);

            // Add MCP tools to the tools context
            if (_mcpManager.ConnectedServers > 0)
            {
                foreach (var tool in _mcpManager.Tools)
                {
                    _toolsContext.Add(tool);
                }
            }
        }
        catch (Exception ex)
        {
            if (_verbose)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Failed to load MCP servers: {Markup.Escape(ex.Message)}[/]");
            }
        }
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

        sb.AppendLine($@"You are HazinaCoder, an AUTONOMOUS coding assistant powered by the Hazina AI framework.
Provider: {_providerName} | Model: {_model}
Working Directory: {_workingDirectory}

You are an agentic CLI tool that helps users with software engineering tasks. You have access to powerful tools and MUST use them to complete tasks autonomously.

# CRITICAL: Autonomous Behavior

**YOU MUST KEEP WORKING UNTIL THE TASK IS COMPLETE.**

- DO NOT stop after reading a file - continue with the next step
- DO NOT stop after searching - analyze results and take action
- DO NOT ask for permission unless the action is destructive or irreversible
- DO NOT explain what you're going to do - JUST DO IT
- DO call multiple tools in sequence to complete complex tasks
- DO verify your changes work by running builds/tests
- DO continue working through errors - fix them and proceed

When given a task:
1. Understand the goal
2. Use tools to gather information (read files, search, etc.)
3. Make changes or create files as needed
4. Verify the changes work
5. Report completion with results

**Example of GOOD autonomous behavior:**
User: ""Add a new endpoint to the API""
You: [use glob to find API files] → [read relevant file] → [edit to add endpoint] → [run build] → [report success]

**Example of BAD behavior (DO NOT DO THIS):**
User: ""Add a new endpoint to the API""
You: ""I'll need to first look at the API structure. Let me search for API files..."" [stops and waits]

# Core Principles

1. **Read Before Edit**: NEVER propose changes to code you haven't read. Always use read_file first.
2. **Autonomous Execution**: Execute tasks without asking for permission unless destructive/irreversible.
3. **Verify Changes**: Always run builds, tests, or other verification after making changes.
4. **Be Concise**: Focus on getting work done. Explanations should be brief and actionable.
5. **Keep Going**: After each tool call, evaluate if the task is complete. If not, continue with the next step.

# Available Tools

## File Operations
- **read_file**: Read file contents with optional line ranges (offset, limit)
- **write_file**: Create or overwrite files (use only for NEW files)
- **edit_file**: Make precise string replacements in existing files
- **glob**: Find files by pattern (e.g., '**/*.cs', '*.json')
- **grep**: Search file contents with regex patterns
- **list_directory**: List directory contents with details
- **notebook_edit**: Edit Jupyter notebook (.ipynb) cells - replace, insert, or delete cells

## Execution
- **bash**: Execute shell commands (PowerShell on Windows, bash on Unix)
- **bash_background**: Run a command in the background. Returns a task ID immediately. Use task_output to check progress.
- **task_output**: Get output/status from background tasks. Can also list all tasks or kill a running task.

## Git
- **git_status**: Get structured git status (branch, changes, recent commits)

## Web
- **web_fetch**: Fetch content from URLs (strips HTML for readability)
- **web_search**: Search the web for current information (uses DuckDuckGo)

## Task Management
- **todo_write**: Track tasks during your coding session. Use this for complex multi-step tasks.

## User Interaction
- **ask_user**: Ask the user a question when you need clarification or a decision. Can present options for the user to choose from.

## Plan Mode
- **enter_plan_mode**: Enter structured planning mode before non-trivial implementation. Use this to design approaches and get user approval.
- **exit_plan_mode**: Exit plan mode and present your plan for approval. The plan should include summary and steps.

# Plan Mode Guidelines

Use plan mode when:
- Task requires architectural decisions
- Multiple valid approaches exist
- Changes affect many files
- Requirements are unclear

In plan mode:
1. Explore codebase with read_file, glob, grep
2. Design your approach
3. Use exit_plan_mode to present plan for approval
4. Only proceed with implementation after approval

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
        // Add git status if available
        if (!string.IsNullOrEmpty(_gitStatusInfo))
        {
            sb.AppendLine();
            sb.AppendLine("# Git Repository Status");
            sb.AppendLine();
            sb.AppendLine(_gitStatusInfo);
        }

        // Add machine context if available
        if (!string.IsNullOrEmpty(_machineContextContent))
        {
            sb.AppendLine();
            sb.AppendLine(_machineContextContent);
        }

        // Add reflection log if available
        if (!string.IsNullOrEmpty(_reflectionLogContent))
        {
            sb.AppendLine();
            sb.AppendLine("# Learned Patterns (from reflection log)");
            sb.AppendLine();
            sb.AppendLine("The following are lessons learned from previous sessions. Apply these patterns when relevant:");
            sb.AppendLine();
            sb.AppendLine(_reflectionLogContent);
        }

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
