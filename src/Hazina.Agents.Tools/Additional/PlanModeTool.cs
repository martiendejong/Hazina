using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace Hazina.Agents.Tools.Additional;

/// <summary>
/// Tools for plan mode - structured planning before implementation
/// </summary>
public static class PlanModeTool
{
    private static bool _inPlanMode = false;
    private static string? _currentPlan = null;
    private static string? _planFile = null;

    public static bool IsInPlanMode => _inPlanMode;
    public static string? CurrentPlan => _currentPlan;

    public static HazinaChatTool CreateEnterPlanMode(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "enter_plan_mode",
            description: "Enter plan mode before starting a non-trivial implementation task. Use this to design an approach and get user approval before writing code.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "task_description",
                    Type = "string",
                    Required = true,
                    Description = "Brief description of the task to plan"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("task_description", out var descElement))
                        return "Error: task_description parameter is required";

                    var taskDescription = descElement.GetString();
                    if (string.IsNullOrWhiteSpace(taskDescription))
                        return "Error: task_description cannot be empty";

                    _inPlanMode = true;
                    _currentPlan = null;
                    _planFile = Path.Combine(workingDirectory, ".hazinacoder-plan.md");

                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Rule("[blue]Plan Mode[/]").RuleStyle("blue"));
                    AnsiConsole.MarkupLine($"[blue]Planning:[/] {Markup.Escape(taskDescription)}");
                    AnsiConsole.MarkupLine("[dim]Explore the codebase and design your approach.[/]");
                    AnsiConsole.MarkupLine("[dim]Use exit_plan_mode when ready for approval.[/]");
                    AnsiConsole.Write(new Rule().RuleStyle("blue"));
                    AnsiConsole.WriteLine();

                    return $@"Entered Plan Mode.

Task: {taskDescription}

In plan mode, you should:
1. Explore the codebase using read_file, glob, grep
2. Understand existing patterns and architecture
3. Design your implementation approach
4. Write your plan to the plan file

When ready, use exit_plan_mode to present your plan for user approval.

NOTE: In plan mode, avoid making code changes. Focus on research and planning.";
                }
                catch (Exception ex)
                {
                    return $"Error entering plan mode: {ex.Message}";
                }
            }
        );
    }

    public static HazinaChatTool CreateExitPlanMode(string workingDirectory)
    {
        return new HazinaChatTool(
            name: "exit_plan_mode",
            description: "Exit plan mode and present your plan for user approval. The plan should be complete and ready to implement.",
            parameters: new List<ChatToolParameter>
            {
                new()
                {
                    Name = "plan_summary",
                    Type = "string",
                    Required = true,
                    Description = "Summary of the implementation plan"
                },
                new()
                {
                    Name = "plan_steps",
                    Type = "array",
                    Required = true,
                    Description = "Array of plan steps, each with 'step' (description) and optionally 'files' (affected files)"
                }
            },
            execute: async (messages, call, cancel) =>
            {
                try
                {
                    if (!_inPlanMode)
                    {
                        return "Not in plan mode. Use enter_plan_mode first.";
                    }

                    using JsonDocument argsJson = JsonDocument.Parse(call.FunctionArguments);
                    var root = argsJson.RootElement;

                    if (!root.TryGetProperty("plan_summary", out var summaryElement))
                        return "Error: plan_summary parameter is required";

                    var summary = summaryElement.GetString() ?? "";

                    var steps = new List<(string Step, string[] Files)>();
                    if (root.TryGetProperty("plan_steps", out var stepsElement))
                    {
                        foreach (var stepJson in stepsElement.EnumerateArray())
                        {
                            var step = stepJson.TryGetProperty("step", out var s) ? s.GetString() ?? "" : "";
                            var files = new List<string>();
                            if (stepJson.TryGetProperty("files", out var f))
                            {
                                foreach (var file in f.EnumerateArray())
                                {
                                    var fileName = file.GetString();
                                    if (!string.IsNullOrEmpty(fileName))
                                        files.Add(fileName);
                                }
                            }
                            if (!string.IsNullOrEmpty(step))
                                steps.Add((step, files.ToArray()));
                        }
                    }

                    // Display the plan
                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Rule("[green]Implementation Plan[/]").RuleStyle("green"));
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"[white]{Markup.Escape(summary)}[/]");
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[cyan]Steps:[/]");

                    for (int i = 0; i < steps.Count; i++)
                    {
                        var (step, files) = steps[i];
                        AnsiConsole.MarkupLine($"  [yellow]{i + 1}.[/] {Markup.Escape(step)}");
                        if (files.Length > 0)
                        {
                            AnsiConsole.MarkupLine($"     [dim]Files: {string.Join(", ", files)}[/]");
                        }
                    }

                    AnsiConsole.WriteLine();
                    AnsiConsole.Write(new Rule().RuleStyle("green"));
                    AnsiConsole.WriteLine();

                    // Ask for approval
                    AnsiConsole.MarkupLine("[cyan]Options:[/]");
                    AnsiConsole.MarkupLine("  [green]y[/] - Approve and start implementation");
                    AnsiConsole.MarkupLine("  [yellow]m[/] - Request modifications to the plan");
                    AnsiConsole.MarkupLine("  [red]n[/] - Reject the plan");
                    AnsiConsole.WriteLine();
                    AnsiConsole.Markup("[yellow]Approve this plan? (y/m/n): [/]");

                    var response = Console.ReadLine()?.Trim().ToLower() ?? "n";

                    AnsiConsole.Write(new Rule().RuleStyle("dim"));

                    _currentPlan = FormatPlan(summary, steps);

                    switch (response)
                    {
                        case "y":
                        case "yes":
                            _inPlanMode = false;
                            return $"Plan approved! Proceeding with implementation.\n\n{_currentPlan}";

                        case "m":
                        case "modify":
                            AnsiConsole.Markup("[yellow]What modifications are needed? [/]");
                            var modifications = Console.ReadLine()?.Trim() ?? "";
                            return $"Plan needs modifications. User feedback:\n\n{modifications}\n\nPlease update your plan and use exit_plan_mode again.";

                        default:
                            _inPlanMode = false;
                            _currentPlan = null;
                            return "Plan rejected. Exited plan mode. Please discuss with the user what they would like to do instead.";
                    }
                }
                catch (Exception ex)
                {
                    return $"Error exiting plan mode: {ex.Message}";
                }
            }
        );
    }

    private static string FormatPlan(string summary, List<(string Step, string[] Files)> steps)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Implementation Plan");
        sb.AppendLine();
        sb.AppendLine(summary);
        sb.AppendLine();
        sb.AppendLine("### Steps");
        sb.AppendLine();

        for (int i = 0; i < steps.Count; i++)
        {
            var (step, files) = steps[i];
            sb.AppendLine($"{i + 1}. {step}");
            if (files.Length > 0)
            {
                sb.AppendLine($"   - Files: {string.Join(", ", files)}");
            }
        }

        return sb.ToString();
    }

    public static void Reset()
    {
        _inPlanMode = false;
        _currentPlan = null;
        _planFile = null;
    }
}
