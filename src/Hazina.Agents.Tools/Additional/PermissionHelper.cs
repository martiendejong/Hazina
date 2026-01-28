using Spectre.Console;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Helper for permission checks before destructive operations
/// </summary>
public static class PermissionHelper
{
    private static HashSet<string> _approvedPatterns = new();
    private static bool _approveAll = false;

    /// <summary>
    /// Dangerous command patterns that require confirmation
    /// </summary>
    private static readonly string[] DangerousPatterns = new[]
    {
        // File deletion
        "rm ", "rm -", "del ", "rmdir", "rd ", "Remove-Item",
        // Git destructive
        "git push --force", "git push -f", "git reset --hard", "git clean -f",
        // System
        "format ", "diskpart", "shutdown", "reboot",
        // Database
        "DROP ", "DELETE FROM", "TRUNCATE ",
        // Process
        "taskkill", "kill ", "Stop-Process",
        // Recursive operations
        "-Recurse", "-r ", " -rf",
    };

    /// <summary>
    /// Check if a command requires permission and prompt user if needed
    /// </summary>
    public static bool CheckPermission(string command, out string? denialReason)
    {
        denialReason = null;

        if (_approveAll)
            return true;

        // Check if command matches any dangerous pattern
        var matchedPattern = DangerousPatterns
            .FirstOrDefault(p => command.Contains(p, StringComparison.OrdinalIgnoreCase));

        if (matchedPattern == null)
            return true; // Safe command, allow

        // Check if already approved
        if (_approvedPatterns.Contains(command))
            return true;

        // Prompt user for permission
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[yellow]Permission Required[/]").RuleStyle("yellow"));
        AnsiConsole.MarkupLine("[yellow]The assistant wants to run a potentially destructive command:[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(command)
            .Header("[red]Command[/]")
            .BorderColor(Color.Red));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Matched pattern: {Markup.Escape(matchedPattern)}[/]");
        AnsiConsole.WriteLine();

        AnsiConsole.MarkupLine("[cyan]Options:[/]");
        AnsiConsole.MarkupLine("  [green]y[/] - Allow this command");
        AnsiConsole.MarkupLine("  [green]a[/] - Allow all commands this session");
        AnsiConsole.MarkupLine("  [red]n[/] - Deny this command");
        AnsiConsole.WriteLine();

        AnsiConsole.Markup("[yellow]Allow? (y/a/n): [/]");
        var response = Console.ReadLine()?.Trim().ToLower() ?? "n";

        AnsiConsole.Write(new Rule().RuleStyle("dim"));

        switch (response)
        {
            case "y":
            case "yes":
                _approvedPatterns.Add(command);
                return true;

            case "a":
            case "all":
                _approveAll = true;
                AnsiConsole.MarkupLine("[yellow]All commands approved for this session.[/]");
                return true;

            default:
                denialReason = "User denied permission for this command";
                return false;
        }
    }

    /// <summary>
    /// Reset permissions (for new session)
    /// </summary>
    public static void Reset()
    {
        _approvedPatterns.Clear();
        _approveAll = false;
    }

    /// <summary>
    /// Check if approve-all is enabled
    /// </summary>
    public static bool IsApproveAllEnabled => _approveAll;
}
