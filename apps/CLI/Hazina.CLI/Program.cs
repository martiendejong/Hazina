using System.CommandLine;
using Hazina.CLI.Commands;

namespace Hazina.CLI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Hazina CLI - AI-powered tools for development")
        {
            Name = "hazina"
        };

        // Add subcommands
        rootCommand.AddCommand(AskCommand.Create());
        rootCommand.AddCommand(RagCommand.Create());
        rootCommand.AddCommand(AgentCommand.Create());
        rootCommand.AddCommand(ReasonCommand.Create());
        rootCommand.AddCommand(LongDocCommand.Create());

        return await rootCommand.InvokeAsync(args);
    }
}
