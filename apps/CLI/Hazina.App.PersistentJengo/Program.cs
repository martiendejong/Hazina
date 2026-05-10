using Hazina.AgenticOrchestration.Services.PersistentSession;
using Hazina.LLMs.Client;
using Hazina.LLMs.OpenAI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

// Banner
AnsiConsole.Write(
    new FigletText("Persistent Jengo")
        .LeftJustified()
        .Color(Color.Green));

AnsiConsole.MarkupLine("[dim]Always-on AGI • Phase 1: Persistent Session Core[/]");
AnsiConsole.WriteLine();

// Setup DI
var services = new ServiceCollection();

services.AddLogging(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

services.AddSingleton<ILlmProviderClient, OpenAIClient>();
services.AddSingleton<IPersistentSessionService, PersistentSessionService>();

var provider = services.BuildServiceProvider();
var sessionService = provider.GetRequiredService<IPersistentSessionService>();

// Start or resume session
string? sessionId = null;
var existingSessions = Directory.GetFiles(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hazina", "PersistentSessions"),
    "*.state.json");

if (existingSessions.Length > 0)
{
    var resume = AnsiConsole.Confirm("Found existing session(s). Resume last session?");

    if (resume)
    {
        var lastSession = existingSessions.OrderByDescending(File.GetLastWriteTime).First();
        sessionId = Path.GetFileNameWithoutExtension(lastSession).Replace(".state", "");
        AnsiConsole.MarkupLine($"[green]Resuming session:[/] {sessionId}");
    }
}

sessionId = await sessionService.StartAsync(sessionId);

AnsiConsole.MarkupLine($"[green]✓[/] Session active: [bold]{sessionId}[/]");
AnsiConsole.MarkupLine("[dim]Type your message or 'exit' to quit, 'stats' for statistics[/]");
AnsiConsole.WriteLine();

// REPL loop
while (true)
{
    var state = await sessionService.GetStateAsync(sessionId);

    // Prompt
    var prompt = AnsiConsole.Prompt(
        new TextPrompt<string>("[bold cyan]You:[/] ")
            .AllowEmpty());

    if (string.IsNullOrWhiteSpace(prompt)) continue;

    if (prompt.Equals("exit", StringComparison.OrdinalIgnoreCase))
    {
        var shouldArchive = AnsiConsole.Confirm("Archive this session?");
        if (shouldArchive)
        {
            await sessionService.ArchiveSessionAsync(sessionId);
            AnsiConsole.MarkupLine("[green]✓[/] Session archived");
        }
        break;
    }

    if (prompt.Equals("stats", StringComparison.OrdinalIgnoreCase))
    {
        if (state != null)
        {
            var table = new Table();
            table.AddColumn("Metric");
            table.AddColumn("Value");
            table.AddRow("Session ID", state.SessionId);
            table.AddRow("Created", state.CreatedAt.ToLocalTime().ToString("g"));
            table.AddRow("Last Active", state.LastActive.ToLocalTime().ToString("g"));
            table.AddRow("Turns", state.TurnCount.ToString());
            table.AddRow("Total Tokens", state.TotalTokens.ToString("N0"));
            table.AddRow("Messages", state.Context.Messages.Count.ToString());
            table.AddRow("Truncated", state.Context.TruncatedCount.ToString());

            AnsiConsole.Write(table);
        }
        continue;
    }

    // Send to Jengo
    try
    {
        AnsiConsole.Status()
            .Start("[dim]Jengo is thinking...[/]", async ctx =>
            {
                var response = await sessionService.SendMessageAsync(sessionId, prompt);

                AnsiConsole.MarkupLine($"[bold green]Jengo:[/] {response}");
                AnsiConsole.WriteLine();
            });
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
    }
}

AnsiConsole.MarkupLine("[dim]Goodbye![/]");
