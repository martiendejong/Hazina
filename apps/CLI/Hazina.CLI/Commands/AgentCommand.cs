using System.CommandLine;
using Hazina.CLI.Infrastructure;
using Hazina.LLMs;
using Spectre.Console;

namespace Hazina.CLI.Commands;

/// <summary>
/// Agent command for tool-calling agent execution
/// </summary>
public static class AgentCommand
{
    public static Command Create()
    {
        var taskArg = new Argument<string>("task", "Task for the agent to complete");
        var maxStepsOption = new Option<int>("--max-steps", () => 10, "Maximum agent steps");

        var command = new Command("agent", "Run a tool-calling agent")
        {
            taskArg,
            maxStepsOption
        };

        command.SetHandler(async (string task, int maxSteps) =>
        {
            try
            {
                var client = HazinaConfig.GetLLMClient();

                AnsiConsole.MarkupLine($"[bold]Agent Task:[/] {task}");
                AnsiConsole.MarkupLine($"[dim]Max steps: {maxSteps}[/]");
                AnsiConsole.WriteLine();

                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Agent thinking...", async ctx =>
                    {
                        var messages = new List<HazinaChatMessage>
                        {
                            new()
                            {
                                Role = HazinaMessageRole.System,
                                Text = @"You are an AI agent that plans and executes tasks step by step.
Think through the task and explain what steps you would take to complete it."
                            },
                            new()
                            {
                                Role = HazinaMessageRole.User,
                                Text = task
                            }
                        };

                        var response = await client.GetResponse(
                            messages,
                            HazinaChatResponseFormat.Text,
                            null,
                            null,
                            CancellationToken.None
                        );

                        AnsiConsole.MarkupLine("[bold]Agent Response:[/]");
                        AnsiConsole.WriteLine(response.Result);
                    });
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }

        }, taskArg, maxStepsOption);

        return command;
    }
}
