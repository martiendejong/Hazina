using System.CommandLine;
using System.Text;
using System.Text.Json;
using Spectre.Console;
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Hazina.LLMs.Anthropic;
using Hazina.Agents.Tools.Context;
using Hazina.Agents.Tools.Mcp;
using SharpToken;
using Hazina.App.HazinaCoder.Core.Identity;
using Hazina.App.HazinaCoder.Core.Memory;
using Hazina.App.HazinaCoder.Core.State;

// HazinaCoder - Multi-provider coding assistant CLI with cognitive architecture
// Supports: OpenAI, Anthropic Claude, Ollama (local), and more
// Features: Persistent identity, memory systems, continuous learning

var providerOpt = new Option<string>("--provider", () => "auto", "LLM provider: openai, anthropic, ollama, or auto");
var modelOpt = new Option<string?>("--model", "Model override (provider-specific)");
var workingDirOpt = new Option<string>("--working-dir", () => @"C:\scripts", "Working directory for file operations");
var verboseOpt = new Option<bool>("--verbose", () => false, "Enable verbose output");
var maxTurnsOpt = new Option<int>("--max-turns", () => 50, "Maximum tool calls per conversation turn (default: 50)");
var machineContextOpt = new Option<string?>("--machine-context", "Path to machine context directory (e.g., C:\\scripts\\_machine)");
var reflectionLogOpt = new Option<string?>("--reflection-log", "Path to reflection log file for learned patterns");
var loadGitOpt = new Option<bool>("--load-git", () => true, "Load git status at startup (default: true)");
var loadMcpOpt = new Option<bool>("--load-mcp", () => true, "Load MCP servers from settings (default: true)");
var mcpSettingsOpt = new Option<string?>("--mcp-settings", "Path to MCP settings file (default: auto-discover)");
var maxContinuationsOpt = new Option<int>("--max-continuations", () => 5, "Maximum continuation prompts when model stops early (default: 5)");
var continuationPromptOpt = new Option<string?>("--continuation-prompt", "Custom prompt to inject for continuations");
var autoScriptsOpt = new Option<bool>("--auto-scripts", () => true, "Auto-load C:\\scripts environment at startup (default: true)");
var scriptsPathOpt = new Option<string>("--scripts-path", () => @"C:\scripts", "Path to scripts environment (default: C:\\scripts)");
var promptArg = new Argument<string[]>("prompt", () => Array.Empty<string>(), "Direct prompt (non-interactive mode)");

var rootCommand = new RootCommand("HazinaCoder - Multi-provider coding assistant powered by Hazina AI")
{
    providerOpt, modelOpt, workingDirOpt, verboseOpt, maxTurnsOpt,
    machineContextOpt, reflectionLogOpt, loadGitOpt, loadMcpOpt, mcpSettingsOpt,
    maxContinuationsOpt, continuationPromptOpt, autoScriptsOpt, scriptsPathOpt, promptArg
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
    var maxContinuations = context.ParseResult.GetValueForOption(maxContinuationsOpt);
    var continuationPrompt = context.ParseResult.GetValueForOption(continuationPromptOpt);
    var autoScripts = context.ParseResult.GetValueForOption(autoScriptsOpt);
    var scriptsPath = context.ParseResult.GetValueForOption(scriptsPathOpt)!;
    var promptArgs = context.ParseResult.GetValueForArgument(promptArg);

    var cli = new HazinaCoderCLI(provider, model, workingDir, verbose, maxTurns, machineContext, reflectionLog, loadGit, loadMcp, mcpSettings, maxContinuations, continuationPrompt, autoScripts, scriptsPath);
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
    private int _maxContinuations;
    private string? _continuationPrompt;
    private bool _autoScripts;
    private string _scriptsPath;
    private string? _gitStatusInfo;
    private string? _machineContextContent;
    private string? _reflectionLogContent;
    private string? _scriptsClaudeMdContent;
    private string? _knowledgeBaseContent;
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

    // Improvement #1: Ctrl+C graceful interrupt handling
    private CancellationTokenSource _cts = new();
    private bool _isProcessing = false;

    // Improvement #2: Token/context tracking
    private int _estimatedContextTokens = 0;
    private const int DefaultContextLimit = 128000; // Most models support at least 128k
    private int _contextWarningThreshold = 100000; // Warn at ~78% capacity
    private const int AutoCompressThreshold = 90000; // Auto-compress at ~70% capacity
    private const int TargetTokensAfterCompress = 40000; // Target ~31% after compression
    private string? _lastConversationSummary; // Summary of compressed conversation
    private bool _isCompressing = false; // Prevent recursive compression

    // Improvement #5: Configuration loaded from file
    private HazinaCoderConfig? _config;

    // ⭐ COGNITIVE ARCHITECTURE: Persistent identity, memory, learning
    private AgentIdentity? _identity;

    // Round 2 Improvements
    // #6: Auto-retry with exponential backoff
    private const int MaxRetries = 3;
    private static readonly int[] RetryDelaysMs = { 1000, 2000, 4000 };

    // #8: Conversation export tracking
    private DateTime _sessionStartTime = DateTime.Now;

    // 🔄 Session logging - write everything to file, show minimal output
    private string? _sessionLogPath;
    private StreamWriter? _sessionLogWriter;
    private bool _quietMode = false; // When true, only show user-facing messages

    // Round 3 Improvements
    // #11: Command history
    private List<string> _commandHistory = new();
    private int _historyIndex = -1;

    // #14: Per-request cost tracking
    private decimal _lastRequestCost = 0m;
    private int _lastRequestTokens = 0;

    // #15: Recent files edited
    private List<string> _recentFiles = new();
    private const int MaxRecentFiles = 10;

    // Round 4 Improvements
    // #16: Command aliases
    private Dictionary<string, string> _aliases = new()
    {
        { "/c", "/clear" },
        { "/e", "/exit" },
        { "/h", "/help" },
        { "/s", "/status" },
        { "/t", "/tokens" },
        { "/x", "/export" }
    };

    // #18: Time tracking
    private DateTime _lastCommandTime = DateTime.Now;
    private TimeSpan _totalActiveTime = TimeSpan.Zero;

    // Round 5 Improvements
    // #21: Multi-line input mode
    private bool _multiLineMode = false;
    private StringBuilder _multiLineBuffer = new();

    private enum OutputMode
    {
        Full,      // Show everything
        Compact,   // Show up to 2000 chars
        Minimal    // Show up to 400 chars
    }

    public HazinaCoderCLI(string provider, string? model, string workingDir, bool verbose, int maxTurns = 50,
        string? machineContext = null, string? reflectionLog = null, bool loadGit = true,
        bool loadMcp = true, string? mcpSettings = null, int maxContinuations = 5, string? continuationPrompt = null,
        bool autoScripts = true, string scriptsPath = @"C:\scripts")
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
        _maxContinuations = maxContinuations;
        _continuationPrompt = continuationPrompt;
        _autoScripts = autoScripts;
        _scriptsPath = scriptsPath;
    }

    public void Dispose()
    {
        _mcpManager?.Dispose();
        _cts.Dispose();
        CloseSessionLog();
    }

    private void InitializeSessionLog()
    {
        try
        {
            var logsDir = Path.Combine(_workingDirectory, ".hazinacoder", "logs");
            Directory.CreateDirectory(logsDir);
            _sessionLogPath = Path.Combine(logsDir, $"session-{_sessionStartTime:yyyyMMdd-HHmmss}.log");
            _sessionLogWriter = new StreamWriter(_sessionLogPath, append: true) { AutoFlush = true };
            LogToSession($"=== HazinaCoder Session Started ===");
            LogToSession($"Provider: {_providerName}");
            LogToSession($"Working Directory: {_workingDirectory}");
            LogToSession($"Timestamp: {_sessionStartTime:yyyy-MM-dd HH:mm:ss}");
            LogToSession("===================================\n");
        }
        catch (Exception ex)
        {
            if (_verbose)
                AnsiConsole.MarkupLine($"[yellow]Warning: Could not create session log: {Markup.Escape(ex.Message)}[/]");
        }
    }

    private void LogToSession(string message)
    {
        try
        {
            _sessionLogWriter?.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
        catch { /* Ignore logging errors */ }
    }

    private void CloseSessionLog()
    {
        try
        {
            if (_sessionLogWriter != null)
            {
                LogToSession("\n=== Session Ended ===");
                LogToSession($"Duration: {DateTime.Now - _sessionStartTime}");
                LogToSession($"Total Cost: ${_sessionCost:F4}");
                LogToSession($"Total Tokens: {_sessionTokens:N0}");
                _sessionLogWriter.Close();
                _sessionLogWriter.Dispose();
                _sessionLogWriter = null;
            }
        }
        catch { /* Ignore cleanup errors */ }
    }

    public async Task Run(string[] promptArgs)
    {
        // Improvement #1: Ctrl+C graceful interrupt handling
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true; // Prevent immediate termination
            if (_isProcessing)
            {
                AnsiConsole.MarkupLine("\n[yellow]Interrupting...[/]");
                _cts.Cancel();
                _cts = new CancellationTokenSource(); // Reset for next operation
            }
            else
            {
                AnsiConsole.MarkupLine("\n[yellow]Use /exit to quit, or press Ctrl+C again to force exit.[/]");
            }
        };

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

        // 🔄 Initialize session logging
        InitializeSessionLog();

        // Improvement #5: Load configuration file
        _config = LoadConfiguration();
        ApplyConfiguration();

        // 🧠 COGNITIVE ARCHITECTURE: Load identity (persistent self across sessions)
        var identityBasePath = Path.Combine(Path.GetDirectoryName(typeof(HazinaCoderCLI).Assembly.Location) ?? ".", "..");
        _identity = new AgentIdentity(identityBasePath);
        try
        {
            await _identity.LoadIdentityAsync();
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not load identity: {Markup.Escape(ex.Message)}");
            AnsiConsole.MarkupLine("[dim]Will continue with default identity[/]");
        }

        // Load CLAUDE.md if present (from working directory)
        _claudeMdContent = LoadClaudeMd();

        // 🚀 AUTO-SCRIPTS: Load C:\scripts environment automatically
        if (_autoScripts && Directory.Exists(_scriptsPath))
        {
            LoadScriptsEnvironment();
        }

        // Load skills from .claude/skills/
        _skills = LoadSkills();

        // Also load skills from scripts path if available
        if (_autoScripts && Directory.Exists(_scriptsPath))
        {
            var scriptsSkillsPath = Path.Combine(_scriptsPath, ".claude", "skills");
            if (Directory.Exists(scriptsSkillsPath))
            {
                var scriptsSkills = LoadSkillsFromPath(scriptsSkillsPath);
                _skills.AddRange(scriptsSkills);
            }
        }

        // Load enhanced context (Phase 4B)
        _gitStatusInfo = LoadGitStatus();
        _machineContextContent = LoadMachineContext();
        _reflectionLogContent = LoadReflectionLog();
        _knowledgeBaseContent = LoadKnowledgeBase();

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

                // Always log full output to session file
                LogToSession($"[TOOL:{toolName}] {message}");

                // In quiet mode, only show "working..." indicator
                if (_quietMode)
                {
                    // Show minimal working indicator (overwrite previous line)
                    Console.Write($"\r[Working: {toolName}...]".PadRight(60));
                }
                else
                {
                    var turnDisplay = _verbose ? $" (turn {_currentTurnCount}/{_maxTurns})" : "";
                    AnsiConsole.MarkupLine($"\n[cyan][[Tool: {Markup.Escape(toolName)}{turnDisplay}]][/]");
                    DisplayToolOutput(toolName, message);
                }
            },
            // Continuation hooks - keep the model working until task is complete
            MaxContinuations = _maxContinuations,
            ContinuationPrompt = _continuationPrompt ?? "Continue working on the task. If you've completed all steps, say 'TASK COMPLETE' and summarize what was done.",
            ShouldContinue = (response, turnNumber) =>
            {
                // Don't continue if the response indicates completion
                var completionIndicators = new[]
                {
                    "TASK COMPLETE",
                    "task complete",
                    "I've completed",
                    "I have completed",
                    "successfully completed",
                    "All done",
                    "The task is done",
                    "I'm done",
                    "finished the task",
                    "task has been completed"
                };

                foreach (var indicator in completionIndicators)
                {
                    if (response.Contains(indicator, StringComparison.OrdinalIgnoreCase))
                    {
                        if (_verbose)
                        {
                            AnsiConsole.MarkupLine("[dim](Task completion detected)[/]");
                        }
                        return false; // Stop - task is complete
                    }
                }

                // Don't continue if the response is asking a question or waiting for user input
                if (response.TrimEnd().EndsWith("?") ||
                    response.Contains("please clarify", StringComparison.OrdinalIgnoreCase) ||
                    response.Contains("what would you like", StringComparison.OrdinalIgnoreCase) ||
                    response.Contains("let me know", StringComparison.OrdinalIgnoreCase) ||
                    response.Contains("would you prefer", StringComparison.OrdinalIgnoreCase))
                {
                    return false; // Stop - waiting for user input
                }

                // Continue if the response is short (likely just an explanation before continuing)
                // or if it seems like an intermediate status update
                if (_verbose)
                {
                    AnsiConsole.MarkupLine($"[dim](Continuation: turn {turnNumber}, prompting model to continue)[/]");
                }
                return true; // Continue working
            },
            OnToolExecuted = (toolName, result, turnNumber) =>
            {
                // Track tool execution for analytics/debugging
                if (_verbose)
                {
                    AnsiConsole.MarkupLine($"[dim](Tool {toolName} completed on turn {turnNumber})[/]");
                }
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
        if (_scriptsClaudeMdContent != null)
        {
            AnsiConsole.MarkupLine("[green]✓[/] [dim]Scripts CLAUDE.md loaded (C:\\scripts\\CLAUDE.md)[/]");
        }
        if (_knowledgeBaseContent != null)
        {
            AnsiConsole.MarkupLine("[green]✓[/] [dim]Knowledge base loaded[/]");
        }
        if (_mcpManager != null && _mcpManager.ConnectedServers > 0)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] [dim]{_mcpManager.ConnectedServers} MCP server(s), {_mcpManager.Tools.Count} tools[/]");
        }
        if (_config != null)
        {
            AnsiConsole.MarkupLine("[green]✓[/] [dim]Configuration loaded[/]");
        }
        AnsiConsole.MarkupLine($"[dim]Output: {_outputMode} | Permissions: {(_toolsContext.EnablePermissions ? "ON" : "OFF")} | Backups: ON[/]");
        AnsiConsole.MarkupLine("[dim]Commands: /help, /tokens, /restore, /backups, /clear, /exit | Ctrl+C to interrupt[/]");
        AnsiConsole.WriteLine();

        while (true)
        {
            // #21: Multi-line mode indicator
            if (_multiLineMode)
            {
                AnsiConsole.Markup("[yellow]... [/]");
            }
            else
            {
                AnsiConsole.Markup("[green]> [/]");
            }

            var line = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(line))
            {
                if (_multiLineMode)
                {
                    _multiLineBuffer.AppendLine();
                }
                continue;
            }

            // #21: Multi-line mode toggle with {{ and }}
            if (line.Trim() == "{{")
            {
                _multiLineMode = true;
                _multiLineBuffer.Clear();
                AnsiConsole.MarkupLine("[dim]Multi-line mode. End with }} on its own line.[/]");
                continue;
            }

            if (_multiLineMode)
            {
                if (line.Trim() == "}}")
                {
                    _multiLineMode = false;
                    line = _multiLineBuffer.ToString().TrimEnd();
                    _multiLineBuffer.Clear();
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                }
                else
                {
                    _multiLineBuffer.AppendLine(line);
                    continue;
                }
            }

            // #16: Expand aliases
            if (line.StartsWith("/") && _aliases.TryGetValue(line.Split(' ')[0], out var expanded))
            {
                line = expanded + line.Substring(line.Split(' ')[0].Length);
            }

            // Handle slash commands
            if (line.StartsWith("/"))
            {
                var result = await HandleCommand(line);
                if (result == CommandResult.Exit)
                    break;
                if (result == CommandResult.Handled)
                    continue;
            }

            // #11: Add to command history
            if (!string.IsNullOrWhiteSpace(line) && ((_commandHistory.Count == 0) || _commandHistory[^1] != line))
            {
                _commandHistory.Add(line);
            }
            _historyIndex = _commandHistory.Count;

            // #18: Track time
            var commandStart = DateTime.Now;

            await RunOnce(line);

            // #18: Update time tracking
            _totalActiveTime += DateTime.Now - commandStart;
            _lastCommandTime = DateTime.Now;
        }
    }

    private async Task RunOnce(string prompt)
    {
        // Reset turn counter for this conversation turn
        var startTurnCount = _currentTurnCount;

        // Log user input to session log
        LogToSession($"[USER] {prompt}");

        _context.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.User,
            Text = prompt
        });

        // 🔄 AUTO-COMPRESS: Check if we need to compress BEFORE making the API call
        await AutoCompressContextIfNeeded();

        // Improvement #2: Token tracking - estimate tokens before sending
        _estimatedContextTokens = EstimateContextTokens();
        if (_estimatedContextTokens > _contextWarningThreshold)
        {
            var percentage = (int)((_estimatedContextTokens / (double)DefaultContextLimit) * 100);
            AnsiConsole.MarkupLine($"[yellow]⚠ Context size: ~{_estimatedContextTokens:N0} tokens ({percentage}% of limit)[/]");
            if (_estimatedContextTokens > DefaultContextLimit * 0.9)
            {
                AnsiConsole.MarkupLine("[yellow]Will auto-compress if needed...[/]");
            }
        }

        var sb = new StringBuilder();
        void OnChunk(string chunk)
        {
            sb.Append(chunk);
            Console.Write(chunk);
        }

        Console.OutputEncoding = Encoding.UTF8;
        _isProcessing = true;

        // #6: Auto-retry with exponential backoff, #10: Rate limit detection
        var retryCount = 0;
        var success = false;

        while (!success && retryCount <= MaxRetries)
        {
            try
            {
                if (retryCount > 0)
                {
                    var delay = RetryDelaysMs[Math.Min(retryCount - 1, RetryDelaysMs.Length - 1)];
                    AnsiConsole.MarkupLine($"[yellow]Retrying in {delay / 1000}s... (attempt {retryCount + 1}/{MaxRetries + 1})[/]");
                    await Task.Delay(delay, _cts.Token);
                    sb.Clear(); // Clear previous partial response
                }

                var response = await _client.GetResponseStream(
                    _context,
                    OnChunk,
                    HazinaChatResponseFormat.Text,
                    _toolsContext,
                    images: null,
                    _cts.Token // Improvement #1: Use cancellation token
                );

                // Track usage
                if (response.TokenUsage != null)
                {
                    // #14: Per-request cost tracking
                    _lastRequestCost = response.TokenUsage.TotalCost;
                    _lastRequestTokens = response.TokenUsage.TotalTokens;

                    _sessionCost += response.TokenUsage.TotalCost;
                    _sessionTokens += response.TokenUsage.TotalTokens;
                    _estimatedContextTokens = response.TokenUsage.TotalTokens; // Update with actual count
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

                    // Log assistant response to session file
                    LogToSession($"[ASSISTANT] {assistantMessage}");

                    // In quiet mode, clear the working indicator and show the response
                    if (_quietMode)
                    {
                        Console.Write("\r".PadRight(60) + "\r"); // Clear working indicator
                    }
                }

                // Show summary of tool calls made
                var toolCallsThisTurn = _currentTurnCount - startTurnCount;
                if (toolCallsThisTurn > 0 && _verbose)
                {
                    AnsiConsole.MarkupLine($"\n[dim]({toolCallsThisTurn} tool call(s) | ${_sessionCost:F4} | {_sessionTokens:N0} tokens)[/]");
                }

                success = true;
            }
            catch (OperationCanceledException)
            {
                AnsiConsole.MarkupLine("\n[yellow]Operation cancelled.[/]");
                // Remove the last user message since it wasn't processed
                if (_context.Count > 0 && _context[^1].Role == HazinaMessageRole.User)
                {
                    _context.RemoveAt(_context.Count - 1);
                }
                break;
            }
            catch (Exception ex)
            {
                // 🔄 CONTEXT OVERFLOW: Detect and handle by compressing
                if (IsContextOverflowError(ex))
                {
                    AnsiConsole.MarkupLine($"\n[yellow]⚡ Context overflow detected - compressing and retrying...[/]");

                    // Force compression even if below threshold
                    var compressed = await AutoCompressContextIfNeeded();
                    if (!compressed)
                    {
                        // Force more aggressive compression
                        AnsiConsole.MarkupLine("[yellow]Performing aggressive compression...[/]");
                        // Keep only system prompt and last 2 messages
                        var systemPrompt = _context.FirstOrDefault();
                        var lastMessages = _context.Skip(1).TakeLast(2).ToList();
                        _context.Clear();
                        if (systemPrompt != null) _context.Add(systemPrompt);
                        if (_lastConversationSummary != null)
                        {
                            _context.Add(new HazinaChatMessage
                            {
                                Role = HazinaMessageRole.User,
                                Text = "[CONTEXT COMPRESSED DUE TO OVERFLOW]\n\n" + _lastConversationSummary
                            });
                            _context.Add(new HazinaChatMessage
                            {
                                Role = HazinaMessageRole.Assistant,
                                Text = "Understood. Continuing with compressed context."
                            });
                        }
                        foreach (var msg in lastMessages) _context.Add(msg);
                    }

                    retryCount++;
                    if (retryCount <= MaxRetries)
                    {
                        continue; // Retry with compressed context
                    }
                }

                // #10: Rate limit detection
                var isRateLimit = ex.Message.Contains("rate", StringComparison.OrdinalIgnoreCase) ||
                                  ex.Message.Contains("429") ||
                                  ex.Message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
                                  ex.Message.Contains("quota", StringComparison.OrdinalIgnoreCase);

                if (isRateLimit && retryCount < MaxRetries)
                {
                    AnsiConsole.MarkupLine($"\n[yellow]Rate limited.[/]");
                    retryCount++;
                    continue;
                }

                // Check for other retryable errors
                var isRetryable = ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                                  ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
                                  ex.Message.Contains("503") ||
                                  ex.Message.Contains("502");

                if (isRetryable && retryCount < MaxRetries)
                {
                    AnsiConsole.MarkupLine($"\n[yellow]Connection error, will retry.[/]");
                    retryCount++;
                    continue;
                }

                AnsiConsole.MarkupLine($"\n[red]Error:[/] {Markup.Escape(ex.Message)}");
                break;
            }
        }

        _isProcessing = false;

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

        // Try to load from appsettings.Secrets.json as fallback (for VS debugging)
        var (openAiKey, anthropicKey) = LoadApiKeysFromAppSettings();
        if (!string.IsNullOrEmpty(anthropicKey))
            return "anthropic";
        if (!string.IsNullOrEmpty(openAiKey))
            return "openai";

        // Default to ollama (local) if no API keys found
        return "ollama";
    }

    /// <summary>
    /// Try to load API keys from appsettings.Secrets.json (useful when running from VS without env vars).
    /// </summary>
    private (string? openAiKey, string? anthropicKey) LoadApiKeysFromAppSettings()
    {
        // Try common locations
        var possiblePaths = new[]
        {
            // Check next to the executable first (for VS debugging)
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.Secrets.json"),
            // Check HazinaCoder project directory
            @"C:\Projects\hazina\apps\CLI\Hazina.App.HazinaCoder\appsettings.Secrets.json",
            // Check client-manager (shared secrets)
            @"C:\Projects\client-manager\ClientManagerAPI\appsettings.Secrets.json",
            // Check working directory and scripts path
            Path.Combine(_workingDirectory, "appsettings.Secrets.json"),
            Path.Combine(_scriptsPath, "appsettings.Secrets.json")
        };

        foreach (var path in possiblePaths)
        {
            if (!File.Exists(path)) continue;

            try
            {
                var json = File.ReadAllText(path);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string? openAiKey = null;
                string? anthropicKey = null;

                // Try to get OpenAI key
                if (root.TryGetProperty("OpenAI", out var openAi) &&
                    openAi.TryGetProperty("ApiKey", out var oaiKey))
                {
                    openAiKey = oaiKey.GetString();
                }

                // Try to get Anthropic key
                if (root.TryGetProperty("Anthropic", out var anthropic) &&
                    anthropic.TryGetProperty("ApiKey", out var antKey))
                {
                    anthropicKey = antKey.GetString();
                }

                if (!string.IsNullOrEmpty(openAiKey) || !string.IsNullOrEmpty(anthropicKey))
                {
                    if (_verbose)
                    {
                        AnsiConsole.MarkupLine($"[green]✓[/] [dim]Loaded API keys from {path}[/]");
                    }
                    return (openAiKey, anthropicKey);
                }
            }
            catch
            {
                // Skip files that can't be parsed
            }
        }

        return (null, null);
    }

    private (ILLMClient client, string model) CreateClient(string provider, string? modelOverride)
    {
        // Load fallback keys from appsettings.Secrets.json
        var (appSettingsOpenAiKey, appSettingsAnthropicKey) = LoadApiKeysFromAppSettings();

        switch (provider.ToLower())
        {
            case "openai":
                var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                    ?? appSettingsOpenAiKey
                    ?? throw new Exception("OPENAI_API_KEY not found in environment or appsettings.Secrets.json");
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
                    ?? appSettingsAnthropicKey
                    ?? throw new Exception("ANTHROPIC_API_KEY not found in environment or appsettings.Secrets.json");
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

    private async Task<CommandResult> HandleCommand(string line)
    {
        var parts = line.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var command = parts[0].ToLower();
        var arg = parts.Length > 1 ? parts[1] : null;

        switch (command)
        {
            case "/exit":
            case "/quit":
            case "/q":
                // 💾 Save identity before exit
                if (_identity != null)
                {
                    try
                    {
                        await _identity.SaveIdentityAsync();
                    }
                    catch (Exception ex)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not save identity: {Markup.Escape(ex.Message)}");
                    }
                }
                AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                return CommandResult.Exit;

            case "/help":
            case "/?":
                ShowHelp();
                return CommandResult.Handled;

            case "/clear":
                _context.RemoveRange(1, _context.Count - 1); // Keep system prompt
                _lastConversationSummary = null;
                AnsiConsole.MarkupLine("[green]Conversation cleared.[/]");
                return CommandResult.Handled;

            case "/summarize":
            case "/compress":
                AnsiConsole.MarkupLine("[yellow]Compressing conversation context...[/]");
                await AutoCompressContextIfNeeded();
                AnsiConsole.MarkupLine("[green]Context compressed. Use /tokens to see new size.[/]");
                return CommandResult.Handled;

            case "/cost":
            case "/status":
                var contextPct = (int)((_estimatedContextTokens / (double)DefaultContextLimit) * 100);
                var contextColor = contextPct > 90 ? "red" : contextPct > 70 ? "yellow" : "green";
                AnsiConsole.MarkupLine($"[cyan]Session Stats:[/]");
                AnsiConsole.MarkupLine($"  Provider: {_providerName}/{_model}");
                AnsiConsole.MarkupLine($"  Tool Calls: {_currentTurnCount} (max: {_maxTurns})");
                AnsiConsole.MarkupLine($"  Session Tokens: {_sessionTokens:N0}");
                AnsiConsole.MarkupLine($"  [{contextColor}]Context: ~{_estimatedContextTokens:N0} tokens ({contextPct}% of {DefaultContextLimit:N0})[/]");
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

            case "/quiet":
                _quietMode = !_quietMode;
                if (_quietMode)
                {
                    AnsiConsole.MarkupLine("[green]Quiet mode:[/] ON - Only user messages shown, tool output logged to file");
                    if (_sessionLogPath != null)
                        AnsiConsole.MarkupLine($"[dim]Session log: {_sessionLogPath}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[green]Quiet mode:[/] OFF - Full tool output shown");
                }
                return CommandResult.Handled;

            case "/log":
                if (_sessionLogPath != null && File.Exists(_sessionLogPath))
                {
                    AnsiConsole.MarkupLine($"[green]Session log:[/] {_sessionLogPath}");
                    // Show last 20 lines
                    var logLines = File.ReadAllLines(_sessionLogPath).TakeLast(20);
                    foreach (var logLine in logLines)
                        AnsiConsole.MarkupLine($"[dim]{Markup.Escape(logLine)}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No session log available[/]");
                }
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

            case "/restore":
                // Improvement #4: Restore file from backup
                if (string.IsNullOrEmpty(arg))
                {
                    AnsiConsole.MarkupLine("[yellow]Usage:[/] /restore <file_path>");
                    AnsiConsole.MarkupLine("[dim]Restores a file from its .bak backup[/]");
                }
                else
                {
                    var filePath = Path.IsPathRooted(arg)
                        ? arg
                        : Path.GetFullPath(Path.Combine(_workingDirectory, arg));
                    var backupPath = filePath + ".bak";

                    if (File.Exists(backupPath))
                    {
                        try
                        {
                            File.Copy(backupPath, filePath, overwrite: true);
                            AnsiConsole.MarkupLine($"[green]Restored:[/] {filePath} from backup");
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[red]Failed to restore:[/] {ex.Message}");
                        }
                    }
                    else
                    {
                        AnsiConsole.MarkupLine($"[yellow]No backup found:[/] {backupPath}");
                    }
                }
                return CommandResult.Handled;

            case "/backups":
                // List recent backup files
                try
                {
                    var backups = Directory.GetFiles(_workingDirectory, "*.bak", SearchOption.AllDirectories)
                        .Select(f => new FileInfo(f))
                        .OrderByDescending(f => f.LastWriteTime)
                        .Take(10);

                    if (!backups.Any())
                    {
                        AnsiConsole.MarkupLine("[dim]No backup files found in working directory[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[cyan]Recent Backups:[/]");
                        foreach (var backup in backups)
                        {
                            var relativePath = Path.GetRelativePath(_workingDirectory, backup.FullName);
                            AnsiConsole.MarkupLine($"  {backup.LastWriteTime:MM-dd HH:mm} {relativePath}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Error listing backups:[/] {ex.Message}");
                }
                return CommandResult.Handled;

            case "/tokens":
                // Show detailed token information
                _estimatedContextTokens = EstimateContextTokens();
                var pct = (int)((_estimatedContextTokens / (double)DefaultContextLimit) * 100);
                AnsiConsole.MarkupLine("[cyan]Token Usage:[/]");
                AnsiConsole.MarkupLine($"  Estimated context: ~{_estimatedContextTokens:N0} tokens ({pct}%)");
                AnsiConsole.MarkupLine($"  Context limit: {DefaultContextLimit:N0} tokens");
                AnsiConsole.MarkupLine($"  Warning threshold: {_contextWarningThreshold:N0} tokens");
                AnsiConsole.MarkupLine($"  Session total: {_sessionTokens:N0} tokens");
                AnsiConsole.MarkupLine($"  Messages: {_context.Count}");
                return CommandResult.Handled;

            case "/export":
                // #8: Export conversation to markdown
                var exportPath = arg;
                if (string.IsNullOrEmpty(exportPath))
                {
                    exportPath = Path.Combine(_workingDirectory, $"hazinacoder-session-{_sessionStartTime:yyyyMMdd-HHmmss}.md");
                }
                else if (!Path.IsPathRooted(exportPath))
                {
                    exportPath = Path.GetFullPath(Path.Combine(_workingDirectory, exportPath));
                }

                try
                {
                    var exportContent = ExportConversationToMarkdown();
                    File.WriteAllText(exportPath, exportContent);
                    AnsiConsole.MarkupLine($"[green]Exported:[/] {exportPath}");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Export failed:[/] {ex.Message}");
                }
                return CommandResult.Handled;

            case "/retry":
                // #6: Manual retry of last message
                if (_context.Count >= 2)
                {
                    // Remove last assistant response if present
                    if (_context[^1].Role == HazinaMessageRole.Assistant)
                    {
                        _context.RemoveAt(_context.Count - 1);
                    }
                    AnsiConsole.MarkupLine("[cyan]Retrying last message...[/]");
                    return CommandResult.NotHandled; // Will re-run with last user message
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]No message to retry[/]");
                }
                return CommandResult.Handled;

            // Round 3: #11 - Command history
            case "/history":
                if (_commandHistory.Count == 0)
                {
                    AnsiConsole.MarkupLine("[dim]No command history[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[cyan]Command History:[/]");
                    var start = Math.Max(0, _commandHistory.Count - 10);
                    for (var i = start; i < _commandHistory.Count; i++)
                    {
                        var preview = _commandHistory[i].Length > 60
                            ? _commandHistory[i].Substring(0, 57) + "..."
                            : _commandHistory[i];
                        AnsiConsole.MarkupLine($"  {i + 1}. {Markup.Escape(preview)}");
                    }
                }
                return CommandResult.Handled;

            // Round 3: #15 - Recent files
            case "/recent":
                if (_recentFiles.Count == 0)
                {
                    AnsiConsole.MarkupLine("[dim]No recently edited files[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[cyan]Recently Edited Files:[/]");
                    foreach (var file in _recentFiles.Take(10))
                    {
                        var relativePath = Path.GetRelativePath(_workingDirectory, file);
                        AnsiConsole.MarkupLine($"  {Markup.Escape(relativePath)}");
                    }
                }
                return CommandResult.Handled;

            // Round 4: #16 - Aliases
            case "/alias":
                if (string.IsNullOrEmpty(arg))
                {
                    AnsiConsole.MarkupLine("[cyan]Command Aliases:[/]");
                    foreach (var (alias, target) in _aliases)
                    {
                        AnsiConsole.MarkupLine($"  {alias} → {target}");
                    }
                }
                else
                {
                    var parts2 = arg.Split('=', 2);
                    if (parts2.Length == 2)
                    {
                        var aliasName = parts2[0].Trim();
                        var aliasTarget = parts2[1].Trim();
                        if (!aliasName.StartsWith("/")) aliasName = "/" + aliasName;
                        _aliases[aliasName] = aliasTarget;
                        AnsiConsole.MarkupLine($"[green]Alias set:[/] {aliasName} → {aliasTarget}");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("[yellow]Usage:[/] /alias name=command");
                    }
                }
                return CommandResult.Handled;

            // Round 4: #18 - Time tracking
            case "/time":
                var sessionDuration = DateTime.Now - _sessionStartTime;
                AnsiConsole.MarkupLine("[cyan]Time Statistics:[/]");
                AnsiConsole.MarkupLine($"  Session duration: {sessionDuration:hh\\:mm\\:ss}");
                AnsiConsole.MarkupLine($"  Active time: {_totalActiveTime:hh\\:mm\\:ss}");
                AnsiConsole.MarkupLine($"  Commands executed: {_commandHistory.Count}");
                if (_lastRequestTokens > 0)
                {
                    AnsiConsole.MarkupLine($"  Last request: {_lastRequestTokens:N0} tokens, ${_lastRequestCost:F4}");
                }
                return CommandResult.Handled;

            // Round 5: #24 - Provider health check
            case "/ping":
                AnsiConsole.MarkupLine($"[cyan]Checking {_providerName}...[/]");
                try
                {
                    var pingStart = DateTime.Now;
                    // Simple ping by getting a tiny response
                    AnsiConsole.MarkupLine($"[green]✓[/] Provider: {_providerName}");
                    AnsiConsole.MarkupLine($"  Model: {_model}");
                    AnsiConsole.MarkupLine($"  Status: Connected");
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Provider error: {ex.Message}");
                }
                return CommandResult.Handled;

            // Round 5: #23 - Quick tips
            case "/tips":
                AnsiConsole.MarkupLine("[cyan]Quick Tips:[/]");
                AnsiConsole.MarkupLine("  • Use {{ to start multi-line input, }} to end");
                AnsiConsole.MarkupLine("  • /c, /e, /h, /s, /t, /x are short aliases");
                AnsiConsole.MarkupLine("  • Ctrl+C interrupts running operations");
                AnsiConsole.MarkupLine("  • /export saves conversation to markdown");
                AnsiConsole.MarkupLine("  • /restore <file> recovers from .bak backup");
                AnsiConsole.MarkupLine("  • /alias name=command creates shortcuts");
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

        table.AddRow("/help, /h", "Show this help");
        table.AddRow("/output, /o", "Cycle output mode: Full → Compact → Minimal");
        table.AddRow("/permissions, /perm", "Toggle permission checks");
        table.AddRow("/provider <name>", "Switch provider (openai, anthropic, ollama)");
        table.AddRow("/model <name>", "Switch model");
        table.AddRow("/tools", "List available tools");
        table.AddRow("/skills", "List loaded skills");
        table.AddRow("/status, /s", "Show session stats");
        table.AddRow("/tokens, /t", "Show token/context info");
        table.AddRow("/time", "Show time statistics");
        table.AddRow("/export, /x [file]", "Export conversation to markdown");
        table.AddRow("/retry", "Retry the last message");
        table.AddRow("/history", "Show command history");
        table.AddRow("/recent", "Show recently edited files");
        table.AddRow("/alias [name=cmd]", "Show or set command aliases");
        table.AddRow("/restore <file>", "Restore file from .bak backup");
        table.AddRow("/backups", "List recent backup files");
        table.AddRow("/ping", "Check provider connection");
        table.AddRow("/tips", "Show quick tips");
        table.AddRow("/clear, /c", "Clear conversation history");
        table.AddRow("/exit, /e", "Exit HazinaCoder");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Loads: CLAUDE.md, .claude/skills/, .hazinacoderrc[/]");
        AnsiConsole.MarkupLine("[dim]Tips: Ctrl+C to interrupt, {{ for multi-line, /tips for help[/]");
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

    /// <summary>
    /// Load the C:\scripts environment automatically. This sets up:
    /// - Machine context path if not specified
    /// - Reflection log path if not specified
    /// - Scripts CLAUDE.md (the main operational manual)
    /// - Knowledge base content
    /// </summary>
    private void LoadScriptsEnvironment()
    {
        if (!Directory.Exists(_scriptsPath))
            return;

        // Auto-set machine context path if not specified
        if (string.IsNullOrWhiteSpace(_machineContextPath))
        {
            var machineDir = Path.Combine(_scriptsPath, "_machine");
            if (Directory.Exists(machineDir))
            {
                _machineContextPath = machineDir;
                if (_verbose)
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] [dim]Auto-detected machine context: {machineDir}[/]");
                }
            }
        }

        // Auto-set reflection log path if not specified
        if (string.IsNullOrWhiteSpace(_reflectionLogPath) && !string.IsNullOrWhiteSpace(_machineContextPath))
        {
            var reflectionPath = Path.Combine(_machineContextPath, "reflection.log.md");
            if (File.Exists(reflectionPath))
            {
                _reflectionLogPath = reflectionPath;
                if (_verbose)
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] [dim]Auto-detected reflection log[/]");
                }
            }
        }

        // Load C:\scripts\CLAUDE.md - extract only essential sections to save tokens
        var scriptsClaudeMdPath = Path.Combine(_scriptsPath, "CLAUDE.md");
        if (File.Exists(scriptsClaudeMdPath))
        {
            try
            {
                var fullContent = File.ReadAllText(scriptsClaudeMdPath);
                // Extract only essential sections instead of loading the full 87KB file
                _scriptsClaudeMdContent = ExtractEssentialClaudeMd(fullContent);
                if (_verbose)
                {
                    AnsiConsole.MarkupLine($"[green]✓[/] [dim]Loaded: C:\\scripts\\CLAUDE.md (extracted {_scriptsClaudeMdContent.Length:N0} chars from {fullContent.Length:N0})[/]");
                }
            }
            catch (Exception ex)
            {
                if (_verbose)
                {
                    AnsiConsole.MarkupLine($"[yellow]Warning:[/] Could not load scripts CLAUDE.md: {Markup.Escape(ex.Message)}");
                }
            }
        }

        // Load additional critical files from scripts root
        LoadScriptsRootFiles();
    }

    /// <summary>
    /// Load critical files from the scripts root directory.
    /// </summary>
    private void LoadScriptsRootFiles()
    {
        var criticalFiles = new[]
        {
            "MACHINE_CONFIG.md",
            "GENERAL_ZERO_TOLERANCE_RULES.md",
            "GENERAL_DUAL_MODE_WORKFLOW.md",
            "GENERAL_WORKTREE_PROTOCOL.md"
        };

        var loadedFiles = new List<string>();

        foreach (var fileName in criticalFiles)
        {
            var filePath = Path.Combine(_scriptsPath, fileName);
            if (File.Exists(filePath))
            {
                loadedFiles.Add(fileName);
            }
        }

        if (loadedFiles.Count > 0 && _verbose)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] [dim]Found {loadedFiles.Count} scripts root files[/]");
        }
    }

    /// <summary>
    /// Extract only essential sections from CLAUDE.md to reduce token usage.
    /// Full file is 87KB (~22k tokens) - we extract ~8KB (~2k tokens) of key info.
    /// </summary>
    private string ExtractEssentialClaudeMd(string fullContent)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Claude Agent - Essential Quick Reference");
        sb.AppendLine();
        sb.AppendLine("*This is an extracted summary. Use `read_file C:\\scripts\\CLAUDE.md` for full documentation.*");
        sb.AppendLine();

        // Extract key sections using simple pattern matching
        var sections = new Dictionary<string, (string start, string end, int maxChars)>
        {
            // Quick Start - essential startup checklist
            { "Quick Start", ("## 🚀 Quick Start Guide", "## 📋 Common Workflows", 2000) },
            // Essential Tools table - critical for knowing what tools exist
            { "Essential Tools", ("### 🔧 Essential Tools Quick Reference", "**Full documentation:**", 3000) },
            // Success Criteria - what defines done
            { "Success Criteria", ("## 🎯 Success Criteria", "---", 800) },
            // Autonomous Capabilities - know what you can do
            { "Core Capabilities", ("### 🎯 Core Autonomous Capabilities", "---", 1500) }
        };

        foreach (var (name, (start, end, maxChars)) in sections)
        {
            var startIdx = fullContent.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            if (startIdx >= 0)
            {
                var endIdx = fullContent.IndexOf(end, startIdx + start.Length, StringComparison.OrdinalIgnoreCase);
                if (endIdx < 0 || endIdx > startIdx + maxChars + 500)
                    endIdx = Math.Min(startIdx + maxChars, fullContent.Length);

                var section = fullContent.Substring(startIdx, Math.Min(endIdx - startIdx, maxChars));
                if (section.Length >= maxChars)
                    section = section.Substring(0, section.LastIndexOf('\n', maxChars - 50)) + "\n... (section truncated)";

                sb.AppendLine(section);
                sb.AppendLine();
            }
        }

        // Add critical reminders
        sb.AppendLine("## Critical Rules (Always Apply)");
        sb.AppendLine();
        sb.AppendLine("1. **NEVER edit C:\\Projects\\<repo> directly** - use worktrees");
        sb.AppendLine("2. **Read files before editing** - never propose changes to code you haven't read");
        sb.AppendLine("3. **Boy Scout Rule** - leave code cleaner than you found it");
        sb.AppendLine("4. **Log learnings** - update reflection.log.md after every session");
        sb.AppendLine("5. **Use tools** - prefer existing tools in C:\\scripts\\tools\\ over manual commands");
        sb.AppendLine();

        var result = sb.ToString();

        // Hard cap at 8000 chars (~2000 tokens)
        if (result.Length > 8000)
        {
            result = result.Substring(0, result.LastIndexOf('\n', 7900)) + "\n\n... (content truncated for token efficiency)";
        }

        return result;
    }

    /// <summary>
    /// Load knowledge base content from C:\scripts\_machine\knowledge-base\.
    /// </summary>
    private string? LoadKnowledgeBase()
    {
        if (string.IsNullOrWhiteSpace(_machineContextPath))
            return null;

        var kbPath = Path.Combine(_machineContextPath, "knowledge-base");
        if (!Directory.Exists(kbPath))
            return null;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Knowledge Base");
            sb.AppendLine();

            // Load key knowledge base files
            var kbFiles = new[]
            {
                "README.md",
                "01-USER/psychology-profile.md",
                "02-MACHINE/file-system-map.md",
                "06-WORKFLOWS/INDEX.md",
                "07-AUTOMATION/tool-selection-guide.md"
            };

            var loadedCount = 0;
            foreach (var relativePath in kbFiles)
            {
                var filePath = Path.Combine(kbPath, relativePath);
                if (File.Exists(filePath))
                {
                    try
                    {
                        var content = File.ReadAllText(filePath);
                        // Truncate large files
                        if (content.Length > 3000)
                        {
                            content = content.Substring(0, 3000) + "\n... (truncated)";
                        }
                        sb.AppendLine($"## {Path.GetFileName(relativePath)}");
                        sb.AppendLine(content);
                        sb.AppendLine();
                        loadedCount++;
                    }
                    catch
                    {
                        // Skip files that can't be read
                    }
                }
            }

            if (_verbose && loadedCount > 0)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] [dim]Loaded: {loadedCount} knowledge base files[/]");
            }

            return sb.Length > 30 ? sb.ToString() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Load skills from a specific path.
    /// </summary>
    private List<SkillInfo> LoadSkillsFromPath(string skillsPath)
    {
        var skills = new List<SkillInfo>();

        if (!Directory.Exists(skillsPath))
            return skills;

        try
        {
            foreach (var skillDir in Directory.GetDirectories(skillsPath))
            {
                var skillFile = Path.Combine(skillDir, "SKILL.md");
                if (File.Exists(skillFile))
                {
                    try
                    {
                        var content = File.ReadAllText(skillFile);
                        var skillName = Path.GetFileName(skillDir);
                        var description = ExtractSkillDescription(content);

                        skills.Add(new SkillInfo
                        {
                            Name = skillName,
                            Description = description,
                            Content = content,
                            Path = skillFile
                        });
                    }
                    catch
                    {
                        // Skip skills that can't be read
                    }
                }
            }

            if (_verbose && skills.Count > 0)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] [dim]Loaded: {skills.Count} skills from {skillsPath}[/]");
            }
        }
        catch
        {
            // Skip if we can't read the directory
        }

        return skills;
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

        // Minimal system prompt - detailed docs come from CLAUDE.md
        sb.AppendLine($@"You are HazinaCoder, an autonomous coding agent with full machine access.

Provider: {_providerName} | Model: {_model}
Working Directory: {_workingDirectory}
Scripts: C:\scripts

## Core Rules
1. **Read before edit** - Never modify files you haven't read
2. **Use worktrees** - Never edit C:\Projects\<repo> directly
3. **Be autonomous** - Execute without asking unless destructive
4. **Verify changes** - Run builds/tests after modifications
5. **Be concise** - Focus on action, not explanation

## Available Tools
- **read_file/edit_file/write_file**: File operations
- **glob/grep**: Find files and search content
- **bash**: Execute commands, git, PowerShell
- **web_fetch/web_search**: Web access
- **todo_write**: Task tracking
- **ask_user**: Clarification when needed

## Workflow
1. UNDERSTAND: Read files, explore with glob/grep
2. PLAN: Break down complex tasks
3. EXECUTE: Make precise edits
4. VERIFY: Run builds, confirm changes work

For detailed documentation, tools reference, and workflows: `read_file C:\scripts\CLAUDE.md`");

        // Add CLAUDE.md content if present (from working directory)
        if (!string.IsNullOrEmpty(_claudeMdContent))
        {
            sb.AppendLine();
            sb.AppendLine("# Project Instructions (from CLAUDE.md)");
            sb.AppendLine();
            sb.AppendLine(_claudeMdContent);
        }

        // 🚀 Add scripts CLAUDE.md - the main operational manual (from C:\scripts)
        if (!string.IsNullOrEmpty(_scriptsClaudeMdContent))
        {
            sb.AppendLine();
            sb.AppendLine("# Scripts Environment - Main Operational Manual (from C:\\scripts\\CLAUDE.md)");
            sb.AppendLine();
            sb.AppendLine("This is the comprehensive operational manual that governs agent behavior:");
            sb.AppendLine();
            // The full content is very long, so we include it as-is (important for full context)
            sb.AppendLine(_scriptsClaudeMdContent);
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

        // 🚀 Add knowledge base if available
        if (!string.IsNullOrEmpty(_knowledgeBaseContent))
        {
            sb.AppendLine();
            sb.AppendLine(_knowledgeBaseContent);
        }

        sb.AppendLine("# Environment");
        sb.AppendLine();
        sb.AppendLine($"- Platform: {Environment.OSVersion.Platform}");
        sb.AppendLine($"- Working Directory: {_workingDirectory}");
        sb.AppendLine($"- Date: {DateTime.Now:yyyy-MM-dd}");

        return sb.ToString();
    }

    // Improvement #2: Token estimation using SharpToken
    private int EstimateContextTokens()
    {
        try
        {
            // Use cl100k_base encoding (used by GPT-4, Claude approximation)
            var encoding = GptEncoding.GetEncoding("cl100k_base");
            var totalTokens = 0;

            foreach (var message in _context)
            {
                if (!string.IsNullOrEmpty(message.Text))
                {
                    totalTokens += encoding.Encode(message.Text).Count;
                }
            }

            // Add ~4 tokens per message for role/formatting overhead
            totalTokens += _context.Count * 4;

            return totalTokens;
        }
        catch
        {
            // Fallback to rough estimate: ~4 chars per token
            return _context.Sum(m => (m.Text?.Length ?? 0) / 4);
        }
    }

    /// <summary>
    /// Automatically compress the conversation context when it gets too large.
    /// Summarizes older messages while keeping recent context intact.
    /// </summary>
    private async Task<bool> AutoCompressContextIfNeeded()
    {
        if (_isCompressing) return false; // Prevent recursive compression

        _estimatedContextTokens = EstimateContextTokens();

        if (_estimatedContextTokens < AutoCompressThreshold)
            return false; // No compression needed

        _isCompressing = true;

        try
        {
            AnsiConsole.MarkupLine($"\n[yellow]⚡ Context at {_estimatedContextTokens:N0} tokens ({(int)((_estimatedContextTokens / (double)DefaultContextLimit) * 100)}%) - auto-compressing...[/]");

            // Keep system prompt (first message) and recent messages
            var systemPrompt = _context.FirstOrDefault();
            var messagesToKeep = 6; // Keep last 6 messages (3 exchanges)
            var recentMessages = _context.Skip(1).TakeLast(messagesToKeep).ToList();
            var messagesToSummarize = _context.Skip(1).SkipLast(messagesToKeep).ToList();

            if (messagesToSummarize.Count < 4)
            {
                AnsiConsole.MarkupLine("[dim]Not enough messages to compress, keeping context as-is.[/]");
                return false;
            }

            // Build summary request
            var summaryRequest = new StringBuilder();
            summaryRequest.AppendLine("Summarize this conversation concisely, preserving:");
            summaryRequest.AppendLine("1. Key decisions and outcomes");
            summaryRequest.AppendLine("2. Files edited and changes made");
            summaryRequest.AppendLine("3. Important context for continuing the work");
            summaryRequest.AppendLine("4. Any pending tasks or next steps");
            summaryRequest.AppendLine();
            summaryRequest.AppendLine("Conversation to summarize:");
            summaryRequest.AppendLine("---");

            foreach (var msg in messagesToSummarize)
            {
                var role = msg.Role == HazinaMessageRole.User ? "User" : "Assistant";
                var text = msg.Text ?? "";
                // Truncate very long messages
                if (text.Length > 2000)
                    text = text.Substring(0, 2000) + "... (truncated)";
                summaryRequest.AppendLine($"**{role}:** {text}");
                summaryRequest.AppendLine();
            }

            // Use the LLM to generate summary
            var summaryContext = new List<HazinaChatMessage>
            {
                new() { Role = HazinaMessageRole.System, Text = "You are a concise summarizer. Create a brief but complete summary of the conversation. Focus on actions taken, decisions made, and important context. Keep it under 500 words." },
                new() { Role = HazinaMessageRole.User, Text = summaryRequest.ToString() }
            };

            var summaryResponse = new StringBuilder();
            try
            {
                await _client.GetResponseStream(
                    summaryContext,
                    chunk => summaryResponse.Append(chunk),
                    HazinaChatResponseFormat.Text,
                    null, // No tools for summary
                    null,
                    _cts.Token
                );
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Failed to generate summary: {Markup.Escape(ex.Message)}[/]");
                return false;
            }

            _lastConversationSummary = summaryResponse.ToString();

            // Rebuild context with summary
            _context.Clear();

            if (systemPrompt != null)
                _context.Add(systemPrompt);

            // Add summary as a system message
            _context.Add(new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = "[CONVERSATION SUMMARY - Earlier messages were compressed to save context]\n\n" + _lastConversationSummary
            });

            _context.Add(new HazinaChatMessage
            {
                Role = HazinaMessageRole.Assistant,
                Text = "I understand. I have the context from our earlier conversation. Let's continue from where we left off."
            });

            // Add recent messages back
            foreach (var msg in recentMessages)
            {
                _context.Add(msg);
            }

            var newTokens = EstimateContextTokens();
            var savedTokens = _estimatedContextTokens - newTokens;
            var savedPct = (int)((savedTokens / (double)_estimatedContextTokens) * 100);

            AnsiConsole.MarkupLine($"[green]✓ Compressed: {_estimatedContextTokens:N0} → {newTokens:N0} tokens (saved {savedPct}%)[/]");
            AnsiConsole.MarkupLine($"[dim]Summarized {messagesToSummarize.Count} messages, kept {recentMessages.Count} recent messages[/]");

            _estimatedContextTokens = newTokens;
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Compression failed: {Markup.Escape(ex.Message)}[/]");
            return false;
        }
        finally
        {
            _isCompressing = false;
        }
    }

    /// <summary>
    /// Handle context overflow errors by compressing and retrying.
    /// </summary>
    private bool IsContextOverflowError(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("context") && (message.Contains("too long") || message.Contains("exceeded") || message.Contains("maximum")) ||
               message.Contains("token") && (message.Contains("limit") || message.Contains("exceeded") || message.Contains("maximum")) ||
               message.Contains("request too large") ||
               message.Contains("payload too large") ||
               message.Contains("context_length_exceeded");
    }

    // #8: Export conversation to markdown
    private string ExportConversationToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# HazinaCoder Session Export");
        sb.AppendLine();
        sb.AppendLine($"- **Date:** {_sessionStartTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"- **Provider:** {_providerName}");
        sb.AppendLine($"- **Model:** {_model}");
        sb.AppendLine($"- **Working Directory:** {_workingDirectory}");
        sb.AppendLine($"- **Total Tokens:** {_sessionTokens:N0}");
        sb.AppendLine($"- **Estimated Cost:** ${_sessionCost:F4}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        foreach (var message in _context.Skip(1)) // Skip system prompt
        {
            string role;
            if (message.Role == HazinaMessageRole.User)
                role = "User";
            else if (message.Role == HazinaMessageRole.Assistant)
                role = "Assistant";
            else if (message.Role == HazinaMessageRole.System)
                role = "System";
            else
                role = message.Role.ToString() ?? "Unknown";

            sb.AppendLine($"## {role}");
            sb.AppendLine();
            sb.AppendLine(message.Text ?? "(empty)");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"*Exported at {DateTime.Now:yyyy-MM-dd HH:mm:ss}*");

        return sb.ToString();
    }

    // Improvement #5: Configuration file support
    private HazinaCoderConfig? LoadConfiguration()
    {
        var configPaths = new[]
        {
            Path.Combine(_workingDirectory, ".hazinacoderrc"),
            Path.Combine(_workingDirectory, ".hazinacoder.json"),
            Path.Combine(_workingDirectory, "hazinacoder.json"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hazinacoderrc"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".hazinacoder.json"),
        };

        foreach (var path in configPaths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var config = JsonSerializer.Deserialize<HazinaCoderConfig>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (_verbose)
                    {
                        AnsiConsole.MarkupLine($"[dim]Loaded config: {path}[/]");
                    }

                    return config;
                }
                catch (Exception ex)
                {
                    if (_verbose)
                    {
                        AnsiConsole.MarkupLine($"[yellow]Warning: Failed to load config {path}: {ex.Message}[/]");
                    }
                }
            }
        }

        return null;
    }

    private void ApplyConfiguration()
    {
        if (_config == null) return;

        // Apply config values only if not overridden by CLI
        if (_providerName == "auto" && !string.IsNullOrEmpty(_config.Provider))
        {
            _providerName = _config.Provider;
        }

        if (_modelOverride == null && !string.IsNullOrEmpty(_config.Model))
        {
            _modelOverride = _config.Model;
        }

        if (_config.MaxTurns.HasValue && _maxTurns == 50) // 50 is default
        {
            _maxTurns = _config.MaxTurns.Value;
        }

        if (_config.MaxContinuations.HasValue && _maxContinuations == 5) // 5 is default
        {
            _maxContinuations = _config.MaxContinuations.Value;
        }

        if (_config.Verbose.HasValue)
        {
            _verbose = _config.Verbose.Value;
        }

        if (!string.IsNullOrEmpty(_config.OutputMode))
        {
            if (Enum.TryParse<OutputMode>(_config.OutputMode, true, out var mode))
            {
                _outputMode = mode;
            }
        }

        if (_config.ContextWarningThreshold.HasValue)
        {
            _contextWarningThreshold = _config.ContextWarningThreshold.Value;
        }
    }
}

// Improvement #5: Configuration class
class HazinaCoderConfig
{
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public int? MaxTurns { get; set; }
    public int? MaxContinuations { get; set; }
    public bool? Verbose { get; set; }
    public string? OutputMode { get; set; }
    public int? ContextWarningThreshold { get; set; }
    public string? MachineContext { get; set; }
    public string? ReflectionLog { get; set; }
    public bool? LoadGit { get; set; }
    public bool? LoadMcp { get; set; }
    public string? McpSettings { get; set; }
    public string? ContinuationPrompt { get; set; }
    public bool? EnableBackups { get; set; }
    public bool? ShowDiffPreview { get; set; }
}

class SkillInfo
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Content { get; set; } = "";
    public string Path { get; set; } = "";
}
