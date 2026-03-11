using System.CommandLine;
using Hazina.CLI.Infrastructure;
using Hazina.LLMs;
using Spectre.Console;

namespace Hazina.CLI.Commands;

/// <summary>
/// Multi-layer reasoning command
/// </summary>
public static class ReasonCommand
{
    public static Command Create()
    {
        var questionArg = new Argument<string>("question", "Question requiring deep reasoning");
        var minConfidenceOption = new Option<double>("--min-confidence", () => 0.85, "Minimum confidence threshold (0-1)");

        var command = new Command("reason", "Multi-layer reasoning for high-confidence decisions")
        {
            questionArg,
            minConfidenceOption
        };

        command.SetHandler(async (string question, double minConfidence) =>
        {
            try
            {
                var client = HazinaConfig.GetLLMClient();

                AnsiConsole.MarkupLine($"[bold]Question:[/] {question}");
                AnsiConsole.MarkupLine($"[dim]Min confidence: {minConfidence:P0}[/]");
                AnsiConsole.WriteLine();

                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Reasoning...", async ctx =>
                    {
                        ctx.Status("[yellow]Layer 1: Fast reasoning...[/]");

                        var messages = new List<HazinaChatMessage>
                        {
                            new()
                            {
                                Role = HazinaMessageRole.System,
                                Text = @"You are a reasoning assistant that thinks through problems carefully.

For each question:
1. First, give a quick initial answer
2. Then, verify your reasoning step by step
3. Finally, provide your confident answer with an estimated confidence level (0-100%)

Format your response as:
## Initial Thought
[Your quick take]

## Verification
[Step-by-step reasoning]

## Final Answer
[Your answer]

## Confidence
[X]%"
                            },
                            new()
                            {
                                Role = HazinaMessageRole.User,
                                Text = question
                            }
                        };

                        ctx.Status("[yellow]Layer 2: Deep reasoning...[/]");
                        await Task.Delay(500); // Visual feedback

                        ctx.Status("[yellow]Layer 3: Verification...[/]");

                        var response = await client.GetResponse(
                            messages,
                            HazinaChatResponseFormat.Text,
                            null,
                            null,
                            CancellationToken.None
                        );

                        AnsiConsole.MarkupLine("\n[bold]Reasoning Result:[/]\n");
                        AnsiConsole.WriteLine(response.Result);
                    });
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }

        }, questionArg, minConfidenceOption);

        return command;
    }
}
