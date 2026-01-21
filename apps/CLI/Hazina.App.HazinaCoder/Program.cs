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

                if (toolName == "grep" || toolName == "glob")
                {
                    AnsiConsole.MarkupLine("[dim](output sent to LLM)[/]");
                }
                else if (_verbose)
                {
                    var preview = message.Length > 1000
                        ? message.Substring(0, 1000) + "\n... (truncated)"
                        : message;
                    AnsiConsole.WriteLine(preview);
                }
                else
                {
                    var preview = message.Length > 400
                        ? message.Substring(0, 400) + "\n... (truncated)"
                        : message;
                    AnsiConsole.WriteLine(preview);
                }
            }
        };

        // System prompt
        var systemPreamble = $@"You are HazinaCoder: a powerful autonomous coding assistant with full file system access and command execution capabilities.
Provider: {_providerName} | Model: {_model}

AVAILABLE TOOLS:
- read_file: Read file contents with optional line ranges (offset, limit)
- write_file: Create or overwrite files
- edit_file: Make precise string replacements in existing files
- bash: Execute PowerShell/bash commands - UNRESTRICTED ACCESS
- glob: Find files by pattern (e.g., '**/*.cs', '*.json')
- grep: Search file contents with regex patterns
- list_directory: List directory contents with details (files, sizes, dates)
- git_status: Get structured git status (branch, changes, recent commits)
- web_fetch: Fetch content from URLs (strips HTML for readability)

TOOL USAGE PHILOSOPHY:
1. ALWAYS use tools to inspect before modifying - never guess file contents
2. Use read_file FIRST to understand existing code before making changes
3. Use edit_file for surgical modifications to existing files (preserves formatting)
4. Use write_file only for creating NEW files
5. Use bash to run builds, tests, and verify changes
6. Use glob/grep to explore unfamiliar codebases
7. Use list_directory for structured directory exploration
8. Use git_status to understand repository state
9. Work AUTONOMOUSLY - use tools proactively without asking permission

WORKFLOW FOR TASKS:
1. UNDERSTAND: Read relevant files, explore structure with glob/grep/list_directory
2. PLAN: Think through changes needed
3. EXECUTE: Make precise edits or create files
4. VERIFY: Run commands to test changes (build, run tests, etc.)

SURGICAL FILE EDITS:
- When editing files, read them first to get exact strings
- Match whitespace and indentation EXACTLY in old_string
- Use read_file with line ranges to focus on specific sections

COMMAND EXECUTION:
- You have UNRESTRICTED command access
- Run any commands needed (build, test, deploy, etc.)
- Always verify your changes by running appropriate commands

Be concise in explanations. Focus on getting work done autonomously with tools.";

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
        AnsiConsole.MarkupLine("[dim]Commands: /help, /provider, /model, /cost, /clear, /exit[/]");
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
                return CommandResult.Handled;

            case "/context":
                AnsiConsole.MarkupLine($"[cyan]Context:[/] {_context.Count} messages");
                var totalChars = _context.Sum(m => m.Text?.Length ?? 0);
                AnsiConsole.MarkupLine($"[dim]~{totalChars:N0} characters[/]");
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
        table.AddRow("/provider <name>", "Switch provider (openai, anthropic, ollama)");
        table.AddRow("/model <name>", "Switch model");
        table.AddRow("/tools", "List available tools");
        table.AddRow("/cost", "Show session cost and token usage");
        table.AddRow("/context", "Show context size");
        table.AddRow("/clear", "Clear conversation history");
        table.AddRow("/exit", "Exit HazinaCoder");

        AnsiConsole.Write(table);
    }

    private enum CommandResult { Handled, NotHandled, Exit }
}
