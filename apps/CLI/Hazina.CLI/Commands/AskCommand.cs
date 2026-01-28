using System.CommandLine;
using Hazina.CLI.Infrastructure;
using Hazina.LLMs;
using Spectre.Console;

namespace Hazina.CLI.Commands;

/// <summary>
/// Simple LLM query command
/// </summary>
public static class AskCommand
{
    public static Command Create()
    {
        var questionArg = new Argument<string>("question", "Question to ask the LLM");
        var systemOption = new Option<string?>("--system", "System prompt");
        var streamOption = new Option<bool>("--stream", () => true, "Stream response");

        var command = new Command("ask", "Ask a question to an LLM")
        {
            questionArg,
            systemOption,
            streamOption
        };

        command.SetHandler(async (string question, string? system, bool stream) =>
        {
            try
            {
                var client = HazinaConfig.GetLLMClient();

                var messages = new List<HazinaChatMessage>();

                if (!string.IsNullOrEmpty(system))
                {
                    messages.Add(new HazinaChatMessage
                    {
                        Role = HazinaMessageRole.System,
                        Text = system
                    });
                }

                messages.Add(new HazinaChatMessage
                {
                    Role = HazinaMessageRole.User,
                    Text = question
                });

                if (stream)
                {
                    var sb = new System.Text.StringBuilder();
                    var response = await client.GetResponseStream(
                        messages,
                        chunk => { Console.Write(chunk); sb.Append(chunk); },
                        HazinaChatResponseFormat.Text,
                        null,
                        null,
                        CancellationToken.None
                    );
                    Console.WriteLine();
                }
                else
                {
                    var response = await client.GetResponse(messages, HazinaChatResponseFormat.Text, null, null, CancellationToken.None);
                    Console.WriteLine(response.Result);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            }

        }, questionArg, systemOption, streamOption);

        return command;
    }
}
