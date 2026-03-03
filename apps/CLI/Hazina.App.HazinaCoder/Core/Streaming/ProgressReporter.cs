using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Spectre.Console;

namespace Hazina.App.HazinaCoder.Core.Streaming;

/// <summary>
/// Rich progress reporting using Spectre.Console.
/// Provides progress bars, spinners, and status updates.
///
/// Improvement #89: Progress Bars & Status Updates (Value: 8/10, Effort: 4/10, Ratio: 2.00)
/// </summary>
public class ProgressReporter
{
    private readonly Dictionary<string, ProgressTask> _tasks;
    private ProgressContext? _progressContext;
    private bool _isActive;

    public ProgressReporter()
    {
        _tasks = new Dictionary<string, ProgressTask>();
        _isActive = false;
    }

    /// <summary>
    /// Start progress display
    /// </summary>
    public async Task StartAsync(Func<ProgressContext, Task> action)
    {
        _isActive = true;

        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                _progressContext = ctx;
                await action(ctx);
            });

        _isActive = false;
    }

    /// <summary>
    /// Add new task to progress display
    /// </summary>
    public ProgressTask? AddTask(string description, double maxValue = 100)
    {
        if (_progressContext == null)
            return null;

        var task = _progressContext.AddTask(description, maxValue: maxValue);
        _tasks[description] = task;

        return task;
    }

    /// <summary>
    /// Update task progress
    /// </summary>
    public void UpdateProgress(string taskName, double value, string? description = null)
    {
        if (!_tasks.TryGetValue(taskName, out var task))
            return;

        task.Value = value;

        if (description != null)
            task.Description = description;
    }

    /// <summary>
    /// Complete task
    /// </summary>
    public void CompleteTask(string taskName)
    {
        if (!_tasks.TryGetValue(taskName, out var task))
            return;

        task.Value = task.MaxValue;
        task.StopTask();
    }

    /// <summary>
    /// Display status message
    /// </summary>
    public async Task ShowStatusAsync(string message, Func<StatusContext, Task> action)
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync(message, action);
    }

    /// <summary>
    /// Display live table (for real-time updates)
    /// </summary>
    public async Task ShowLiveTableAsync(Func<Task> action)
    {
        var table = new Table()
            .AddColumn("Metric")
            .AddColumn("Value");

        await AnsiConsole.Live(table)
            .StartAsync(async ctx =>
            {
                await action();
            });
    }

    /// <summary>
    /// Display rule (separator)
    /// </summary>
    public void ShowRule(string title)
    {
        AnsiConsole.Write(new Rule($"[yellow]{title}[/]"));
    }

    /// <summary>
    /// Display panel with content
    /// </summary>
    public void ShowPanel(string title, string content, Color? borderColor = null)
    {
        var panel = new Panel(content)
            .Header(title)
            .BorderColor(borderColor ?? Color.Blue);

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Display tree structure
    /// </summary>
    public void ShowTree(string title, Action<Tree> buildTree)
    {
        var tree = new Tree(title);
        buildTree(tree);
        AnsiConsole.Write(tree);
    }

    /// <summary>
    /// Display bar chart
    /// </summary>
    public void ShowBarChart(string title, Dictionary<string, double> data)
    {
        var chart = new BarChart()
            .Label($"[bold]{title}[/]")
            .CenterLabel();

        foreach (var kvp in data)
        {
            chart.AddItem(kvp.Key, kvp.Value, Color.Blue);
        }

        AnsiConsole.Write(chart);
    }

    /// <summary>
    /// Ask for confirmation
    /// </summary>
    public bool Confirm(string message, bool defaultAnswer = true)
    {
        return AnsiConsole.Confirm(message, defaultAnswer);
    }

    /// <summary>
    /// Prompt for input
    /// </summary>
    public string Prompt(string message)
    {
        return AnsiConsole.Prompt(
            new TextPrompt<string>(message)
                .PromptStyle("green")
        );
    }

    /// <summary>
    /// Select from list
    /// </summary>
    public T SelectFrom<T>(string title, List<T> choices) where T : notnull
    {
        return AnsiConsole.Prompt(
            new SelectionPrompt<T>()
                .Title(title)
                .AddChoices(choices)
        );
    }

    /// <summary>
    /// Display success message
    /// </summary>
    public void Success(string message)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] {message}");
    }

    /// <summary>
    /// Display error message
    /// </summary>
    public void Error(string message)
    {
        AnsiConsole.MarkupLine($"[red]✗[/] {message}");
    }

    /// <summary>
    /// Display warning message
    /// </summary>
    public void Warning(string message)
    {
        AnsiConsole.MarkupLine($"[yellow]⚠[/] {message}");
    }

    /// <summary>
    /// Display info message
    /// </summary>
    public void Info(string message)
    {
        AnsiConsole.MarkupLine($"[blue]ℹ[/] {message}");
    }

    /// <summary>
    /// Display markup text
    /// </summary>
    public void Markup(string text)
    {
        AnsiConsole.Markup(text);
    }

    /// <summary>
    /// Display markup line
    /// </summary>
    public void MarkupLine(string text)
    {
        AnsiConsole.MarkupLine(text);
    }

    /// <summary>
    /// Clear console
    /// </summary>
    public void Clear()
    {
        AnsiConsole.Clear();
    }
}

/// <summary>
/// Console streaming endpoint implementation
/// </summary>
public class ConsoleStreamingEndpoint : IStreamingEndpoint
{
    private readonly ProgressReporter _reporter;

    public ConsoleStreamingEndpoint()
    {
        _reporter = new ProgressReporter();
    }

    public Task InitializeAsync()
    {
        _reporter.ShowRule("Agent Session Started");
        return Task.CompletedTask;
    }

    public Task SendEventAsync(AgentEvent agentEvent)
    {
        // Format and display event based on type
        switch (agentEvent.EventType)
        {
            case AgentEventType.PlanningStarted:
                _reporter.Info($"Planning: {agentEvent.Message}");
                break;

            case AgentEventType.ActionStarted:
                _reporter.Info($"Action: {agentEvent.Message}");
                break;

            case AgentEventType.ActionCompleted:
                _reporter.Success($"Completed: {agentEvent.Message}");
                break;

            case AgentEventType.ActionFailed:
                _reporter.Error($"Failed: {agentEvent.Message}");
                break;

            case AgentEventType.BuildProgress:
                if (agentEvent is BuildEvent buildEvent)
                {
                    if (buildEvent.ErrorCount > 0)
                        _reporter.Error(buildEvent.OutputLine);
                    else if (buildEvent.WarningCount > 0)
                        _reporter.Warning(buildEvent.OutputLine);
                    else
                        _reporter.Info(buildEvent.OutputLine);
                }
                break;

            case AgentEventType.TestProgress:
                if (agentEvent is TestEvent testEvent)
                {
                    var color = testEvent.FailedTests > 0 ? "red" : "green";
                    _reporter.MarkupLine($"[{color}]Test: {testEvent.TestName} - {testEvent.PassedTests}/{testEvent.TotalTests} passed[/]");
                }
                break;

            case AgentEventType.TokenUsageUpdate:
                if (agentEvent is TokenUsageEvent tokenEvent)
                {
                    _reporter.Info($"Tokens: {tokenEvent.TotalTokens} (${tokenEvent.CostUSD:F4})");
                }
                break;

            case AgentEventType.SummaryGenerated:
                if (agentEvent is SummaryEvent summaryEvent)
                {
                    _reporter.ShowRule("Session Complete");
                    _reporter.ShowPanel("Summary", summaryEvent.Summary, Color.Green);
                }
                break;

            default:
                _reporter.Info(agentEvent.Message);
                break;
        }

        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _reporter.ShowRule("Agent Session Ended");
        return ValueTask.CompletedTask;
    }
}
