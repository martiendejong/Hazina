using Hazina.AI.FluentAPI.Configuration;
using Hazina.LLMs;

namespace Hazina.Examples.HelloWorld;

/// <summary>
/// Your first Hazina application - demonstrates basic setup and AI query.
///
/// Prerequisites:
/// - .NET 8.0 or higher
/// - OpenAI API key set in OPENAI_API_KEY environment variable
///
/// Run: dotnet run
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Hazina Hello World ===\n");

        // 1. Setup: Get API key from environment
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("ERROR: OPENAI_API_KEY environment variable not set!");
            Console.WriteLine("\nTo set it:");
            Console.WriteLine("  Windows: set OPENAI_API_KEY=sk-your-key-here");
            Console.WriteLine("  Linux/Mac: export OPENAI_API_KEY=sk-your-key-here");
            return;
        }

        // 2. Create AI client using QuickSetup
        var ai = QuickSetup.SetupOpenAI(apiKey);

        Console.WriteLine("AI client initialized (OpenAI GPT-4)\n");

        // 3. Create a simple question
        var question = "What is 2+2?";

        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = question
            }
        };

        Console.WriteLine($"Question: {question}");

        // 4. Get response from AI
        try
        {
            var response = await ai.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                toolsContext: null,
                images: null,
                CancellationToken.None
            );

            // 5. Display results
            Console.WriteLine($"Answer: {response.Content.Text}\n");

            // 6. Show token usage and cost estimate
            Console.WriteLine($"Tokens used: {response.TokenUsage.TotalTokens} " +
                $"(input: {response.TokenUsage.InputTokens}, output: {response.TokenUsage.OutputTokens})");

            // Approximate cost (GPT-4 pricing: $0.03/1K input tokens, $0.06/1K output tokens)
            var estimatedCost = (response.TokenUsage.InputTokens * 0.03m / 1000m) +
                                (response.TokenUsage.OutputTokens * 0.06m / 1000m);
            Console.WriteLine($"Estimated cost: ${estimatedCost:F4}");

            Console.WriteLine("\n✓ Success! Your first Hazina application is working.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine("\nCommon issues:");
            Console.WriteLine("  - Invalid API key");
            Console.WriteLine("  - Network connection problems");
            Console.WriteLine("  - Rate limit exceeded");
        }
    }
}
