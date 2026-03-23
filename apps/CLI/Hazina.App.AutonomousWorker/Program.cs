using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hazina.AgenticOrchestration.Services;
using Hazina.AgenticOrchestration.Services.Execution;
using Hazina.AgenticOrchestration.Services.Monitoring;
using Spectre.Console;

namespace Hazina.App.AutonomousWorker;

class Program
{
    static async Task Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("Jengo Autonomous").Color(Color.Green));
        AnsiConsole.MarkupLine("[green]Phase 2: Autonomous Decision Loop[/]");
        AnsiConsole.WriteLine();

        // Load configuration from environment or user input
        var config = await LoadConfiguration();

        // Initialize services
        var clickUpMonitor = new ClickUpEventMonitor(
            config.ClickUpApiKey,
            config.ClickUpListIds
        );

        var githubMonitor = new GitHubEventMonitor(
            config.GitHubToken,
            config.GitHubOwner,
            config.GitHubRepo
        );

        var workQueue = new WorkPriorityQueue();

        var executor = new AutonomousExecutor(
            config.ClaudeCodePath,
            config.WorkingDirectory
        );

        var worker = new AutonomousWorkerService(
            clickUpMonitor,
            githubMonitor,
            workQueue,
            executor
        );

        // Start autonomous loop
        using var cts = new CancellationTokenSource();

        // Handle Ctrl+C gracefully
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            AnsiConsole.MarkupLine("[yellow]Stopping autonomous worker...[/]");
            cts.Cancel();
        };

        // Run status display in background
        _ = Task.Run(() => DisplayStatus(worker, cts.Token));

        // Start worker loop
        await worker.StartAsync(cts.Token);

        AnsiConsole.MarkupLine("[green]Autonomous worker stopped. Goodbye![/]");
    }

    static async Task<Configuration> LoadConfiguration()
    {
        var config = new Configuration();

        // ClickUp
        config.ClickUpApiKey = Environment.GetEnvironmentVariable("CLICKUP_API_KEY")
            ?? AnsiConsole.Prompt(new TextPrompt<string>("Enter ClickUp API key:").Secret());

        var listIdsInput = Environment.GetEnvironmentVariable("CLICKUP_LIST_IDS")
            ?? AnsiConsole.Ask<string>("Enter ClickUp list IDs (comma-separated):");
        config.ClickUpListIds = new List<string>(listIdsInput.Split(',', StringSplitOptions.TrimEntries));

        // GitHub
        config.GitHubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN")
            ?? AnsiConsole.Prompt(new TextPrompt<string>("Enter GitHub token:").Secret());

        config.GitHubOwner = Environment.GetEnvironmentVariable("GITHUB_OWNER")
            ?? AnsiConsole.Ask<string>("Enter GitHub owner:");

        config.GitHubRepo = Environment.GetEnvironmentVariable("GITHUB_REPO")
            ?? AnsiConsole.Ask<string>("Enter GitHub repo:");

        // Claude Code
        config.ClaudeCodePath = Environment.GetEnvironmentVariable("CLAUDE_CODE_PATH")
            ?? @"C:\Users\HP\AppData\Local\Programs\claude-code\claude.exe";

        config.WorkingDirectory = Environment.GetEnvironmentVariable("WORKING_DIRECTORY")
            ?? @"C:\Projects";

        return config;
    }

    static async Task DisplayStatus(AutonomousWorkerService worker, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

                var status = worker.GetStatus();

                AnsiConsole.MarkupLine($"[dim][[{DateTime.Now:HH:mm:ss}]] Queue: {status.QueueSize} | Running: {status.RunningExecutions}[/]");

                if (status.NextTask != null)
                {
                    AnsiConsole.MarkupLine($"[dim]Next: {status.NextTask.Title.EscapeMarkup()}[/]");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}

class Configuration
{
    public string ClickUpApiKey { get; set; } = string.Empty;
    public List<string> ClickUpListIds { get; set; } = new();
    public string GitHubToken { get; set; } = string.Empty;
    public string GitHubOwner { get; set; } = string.Empty;
    public string GitHubRepo { get; set; } = string.Empty;
    public string ClaudeCodePath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
}
