using FuzzySharp;
using Spectre.Console;

namespace Hazina.App.HazinaCoder.Core.UI;

/// <summary>
/// Command palette with fuzzy search (like VS Code Ctrl+Shift+P)
/// </summary>
public class CommandPalette
{
    private readonly List<Command> _commands = new();
    private readonly Dictionary<string, string> _keyBindings = new();

    public CommandPalette()
    {
        RegisterDefaultCommands();
    }

    /// <summary>
    /// Register a command
    /// </summary>
    public void RegisterCommand(Command command)
    {
        _commands.Add(command);

        if (!string.IsNullOrEmpty(command.KeyBinding))
        {
            _keyBindings[command.KeyBinding] = command.Id;
        }
    }

    /// <summary>
    /// Show command palette and execute selected command
    /// </summary>
    public async Task<CommandResult> ShowAsync(string? initialQuery = null)
    {
        AnsiConsole.Clear();
        AnsiConsole.MarkupLine("[bold yellow]Command Palette[/]");
        AnsiConsole.MarkupLine("[dim]Type to search, Enter to execute, Esc to cancel[/]");
        AnsiConsole.WriteLine();

        var query = initialQuery ?? string.Empty;
        var matches = SearchCommands(query);

        while (true)
        {
            // Display matches
            DisplayMatches(matches, query);

            // Get user input
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Escape)
            {
                return new CommandResult { Cancelled = true };
            }

            if (key.Key == ConsoleKey.Enter && matches.Any())
            {
                var selected = matches.First();
                return await ExecuteCommandAsync(selected.Command);
            }

            if (key.Key == ConsoleKey.Backspace && query.Length > 0)
            {
                query = query[..^1];
                matches = SearchCommands(query);
            }
            else if (key.KeyChar != '\0' && !char.IsControl(key.KeyChar))
            {
                query += key.KeyChar;
                matches = SearchCommands(query);
            }

            // Clear and redraw
            Console.Clear();
            AnsiConsole.MarkupLine("[bold yellow]Command Palette[/]");
            AnsiConsole.MarkupLine($"[dim]Search: [/][white]{query}[/]");
            AnsiConsole.WriteLine();
        }
    }

    /// <summary>
    /// Search commands with fuzzy matching
    /// </summary>
    private List<CommandMatch> SearchCommands(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _commands
                .OrderBy(c => c.Category)
                .ThenBy(c => c.Title)
                .Select(c => new CommandMatch { Command = c, Score = 100 })
                .ToList();
        }

        var matches = _commands
            .Select(cmd =>
            {
                var titleScore = Fuzz.PartialRatio(query.ToLower(), cmd.Title.ToLower());
                var descScore = Fuzz.PartialRatio(query.ToLower(), cmd.Description.ToLower());
                var categoryScore = Fuzz.PartialRatio(query.ToLower(), cmd.Category.ToLower());

                var maxScore = Math.Max(titleScore, Math.Max(descScore, categoryScore));

                return new CommandMatch
                {
                    Command = cmd,
                    Score = maxScore
                };
            })
            .Where(m => m.Score > 60) // Threshold
            .OrderByDescending(m => m.Score)
            .ThenBy(m => m.Command.Category)
            .ThenBy(m => m.Command.Title)
            .Take(10)
            .ToList();

        return matches;
    }

    /// <summary>
    /// Display command matches
    /// </summary>
    private void DisplayMatches(List<CommandMatch> matches, string query)
    {
        if (!matches.Any())
        {
            AnsiConsole.MarkupLine("[dim]No commands found[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn(new TableColumn("Command").Width(40))
            .AddColumn(new TableColumn("Category").Width(15))
            .AddColumn(new TableColumn("Key").Width(10))
            .AddColumn(new TableColumn("Score").Width(8));

        foreach (var match in matches.Take(10))
        {
            var cmd = match.Command;
            var titleMarkup = HighlightMatch(cmd.Title, query);
            var keyBinding = !string.IsNullOrEmpty(cmd.KeyBinding) ? cmd.KeyBinding : "-";

            table.AddRow(
                $"[white]{titleMarkup}[/]\n[dim]{cmd.Description}[/]",
                $"[blue]{cmd.Category}[/]",
                $"[yellow]{keyBinding}[/]",
                $"[dim]{match.Score}[/]"
            );
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Highlight matching characters
    /// </summary>
    private string HighlightMatch(string text, string query)
    {
        if (string.IsNullOrEmpty(query))
            return text;

        // Simple highlighting - could be enhanced
        return text; // TODO: Better highlighting
    }

    /// <summary>
    /// Execute command
    /// </summary>
    private async Task<CommandResult> ExecuteCommandAsync(Command command)
    {
        try
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine($"[bold]Executing:[/] {command.Title}");
            AnsiConsole.WriteLine();

            await command.Execute();

            return new CommandResult
            {
                Success = true,
                CommandId = command.Id,
                CommandTitle = command.Title
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return new CommandResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Register default commands
    /// </summary>
    private void RegisterDefaultCommands()
    {
        // File operations
        RegisterCommand(new Command
        {
            Id = "file.scan-secrets",
            Title = "Scan for Secrets",
            Description = "Scan codebase for accidentally committed secrets",
            Category = "Security",
            KeyBinding = "Ctrl+Shift+S",
            Execute = async () =>
            {
                AnsiConsole.MarkupLine("Scanning for secrets...");
                await Task.Delay(100);
            }
        });

        RegisterCommand(new Command
        {
            Id = "file.preview-diff",
            Title = "Preview Changes",
            Description = "Show diff preview before applying changes",
            Category = "Files",
            KeyBinding = "Ctrl+Shift+D",
            Execute = async () =>
            {
                AnsiConsole.MarkupLine("Showing diff preview...");
                await Task.Delay(100);
            }
        });

        // Package management
        RegisterCommand(new Command
        {
            Id = "package.install",
            Title = "Install Package",
            Description = "Install a NuGet/npm/pip package",
            Category = "Packages",
            Execute = async () =>
            {
                AnsiConsole.MarkupLine("Installing package...");
                await Task.Delay(100);
            }
        });

        RegisterCommand(new Command
        {
            Id = "package.update",
            Title = "Update Packages",
            Description = "Check for and update outdated packages",
            Category = "Packages",
            Execute = async () =>
            {
                AnsiConsole.MarkupLine("Checking for updates...");
                await Task.Delay(100);
            }
        });

        RegisterCommand(new Command
        {
            Id = "package.audit",
            Title = "Security Audit",
            Description = "Scan packages for security vulnerabilities",
            Category = "Security",
            KeyBinding = "Ctrl+Shift+A",
            Execute = async () =>
            {
                AnsiConsole.MarkupLine("Running security audit...");
                await Task.Delay(100);
            }
        });

        // Cache management
        RegisterCommand(new Command
        {
            Id = "cache.clear",
            Title = "Clear Cache",
            Description = "Clear all cached context and files",
            Category = "Cache",
            Execute = async () =>
            {
                AnsiConsole.MarkupLine("Clearing cache...");
                await Task.Delay(100);
            }
        });

        RegisterCommand(new Command
        {
            Id = "cache.stats",
            Title = "Cache Statistics",
            Description = "Show cache hit rates and statistics",
            Category = "Cache",
            Execute = async () =>
            {
                AnsiConsole.MarkupLine("Cache statistics...");
                await Task.Delay(100);
            }
        });

        // System operations
        RegisterCommand(new Command
        {
            Id = "system.health",
            Title = "System Health",
            Description = "Show service health and circuit breaker status",
            Category = "System",
            KeyBinding = "Ctrl+Shift+H",
            Execute = async () =>
            {
                AnsiConsole.MarkupLine("System health check...");
                await Task.Delay(100);
            }
        });

        RegisterCommand(new Command
        {
            Id = "system.offline-mode",
            Title = "Toggle Offline Mode",
            Description = "Enable/disable offline operation mode",
            Category = "System",
            Execute = async () =>
            {
                AnsiConsole.MarkupLine("Toggling offline mode...");
                await Task.Delay(100);
            }
        });

        // Help
        RegisterCommand(new Command
        {
            Id = "help.shortcuts",
            Title = "Show Keyboard Shortcuts",
            Description = "Display all keyboard shortcuts",
            Category = "Help",
            KeyBinding = "Ctrl+?",
            Execute = async () =>
            {
                DisplayShortcuts();
                await Task.CompletedTask;
            }
        });
    }

    /// <summary>
    /// Display keyboard shortcuts
    /// </summary>
    private void DisplayShortcuts()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Shortcut")
            .AddColumn("Command");

        foreach (var (key, commandId) in _keyBindings.OrderBy(kv => kv.Key))
        {
            var command = _commands.FirstOrDefault(c => c.Id == commandId);
            if (command != null)
            {
                table.AddRow($"[yellow]{key}[/]", $"[white]{command.Title}[/]");
            }
        }

        AnsiConsole.Write(table);
    }
}

/// <summary>
/// Command definition
/// </summary>
public class Command
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? KeyBinding { get; set; }
    public Func<Task> Execute { get; set; } = () => Task.CompletedTask;
}

/// <summary>
/// Command match result
/// </summary>
public class CommandMatch
{
    public Command Command { get; set; } = null!;
    public int Score { get; set; }
}

/// <summary>
/// Command execution result
/// </summary>
public class CommandResult
{
    public bool Success { get; set; }
    public bool Cancelled { get; set; }
    public string? CommandId { get; set; }
    public string? CommandTitle { get; set; }
    public string? Error { get; set; }
}
