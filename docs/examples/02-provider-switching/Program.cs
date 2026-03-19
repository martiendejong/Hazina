using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Hazina.LLMs.Anthropic;
using Hazina.LLMs.Gemini;
using Hazina.LLMs.Registry;

namespace Hazina.Examples.ProviderSwitching;

/// <summary>
/// Demonstrates switching between LLM providers (OpenAI, Anthropic, Google) with Hazina.
///
/// This example shows:
/// - Configuring multiple LLM providers
/// - Using the same code with different providers
/// - Setting up automatic failover
/// - Provider selection based on task requirements
///
/// Prerequisites:
/// - .NET 8.0 or higher
/// - At least one LLM API key (OpenAI, Anthropic, or Google)
///
/// Run: dotnet run
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Provider Switching Example ===\n");

        // 1. Setup providers (check which API keys are available)
        var openaiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        var anthropicKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        var googleKey = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");

        if (string.IsNullOrEmpty(openaiKey) && string.IsNullOrEmpty(anthropicKey) && string.IsNullOrEmpty(googleKey))
        {
            Console.WriteLine("ERROR: No API keys found!");
            Console.WriteLine("\nSet at least one of these environment variables:");
            Console.WriteLine("  OPENAI_API_KEY=sk-your-key-here");
            Console.WriteLine("  ANTHROPIC_API_KEY=sk-ant-your-key-here");
            Console.WriteLine("  GOOGLE_API_KEY=your-key-here");
            return;
        }

        // 2. Create provider registry
        var registry = new LLMProviderRegistry();

        // 3. Register available providers
        var availableProviders = new List<string>();

        if (!string.IsNullOrEmpty(openaiKey))
        {
            var openai = new OpenAIClientWrapper(new OpenAIConfig
            {
                ApiKey = openaiKey,
                Model = "gpt-4"
            });
            registry.Register("openai", openai);
            availableProviders.Add("openai");
            Console.WriteLine("✓ Registered OpenAI (GPT-4)");
        }

        if (!string.IsNullOrEmpty(anthropicKey))
        {
            var claude = new ClaudeClientWrapper(new ClaudeConfig
            {
                ApiKey = anthropicKey,
                Model = "claude-3-opus-20240229"
            });
            registry.Register("claude", claude);
            availableProviders.Add("claude");
            Console.WriteLine("✓ Registered Anthropic (Claude 3 Opus)");
        }

        if (!string.IsNullOrEmpty(googleKey))
        {
            var gemini = new GeminiClientWrapper(new GeminiConfig
            {
                ApiKey = googleKey,
                Model = "gemini-pro"
            });
            registry.Register("gemini", gemini);
            availableProviders.Add("gemini");
            Console.WriteLine("✓ Registered Google (Gemini Pro)");
        }

        Console.WriteLine($"\nTotal providers: {availableProviders.Count}\n");

        // 4. Set default provider and fallback chain
        if (availableProviders.Count > 0)
        {
            registry.SetDefaultProvider(availableProviders[0]);
            Console.WriteLine($"Default provider: {availableProviders[0]}");

            if (availableProviders.Count > 1)
            {
                registry.SetFallbackChain(availableProviders.ToArray());
                Console.WriteLine($"Fallback chain: {string.Join(" → ", availableProviders)}");
            }
        }

        Console.WriteLine("\n--- Testing Each Provider ---\n");

        // 5. Test the same query with each provider
        var question = "Explain what a Large Language Model is in one sentence.";

        foreach (var providerName in availableProviders)
        {
            try
            {
                Console.WriteLine($"[{providerName.ToUpper()}]");
                Console.WriteLine($"Question: {question}");

                var provider = registry.GetProvider(providerName);
                var messages = new List<HazinaChatMessage>
                {
                    new() { Role = HazinaMessageRole.User, Text = question }
                };

                var startTime = DateTime.UtcNow;
                var response = await provider.GetResponse(
                    messages,
                    HazinaChatResponseFormat.Text,
                    null,
                    null,
                    CancellationToken.None
                );
                var duration = (DateTime.UtcNow - startTime).TotalMilliseconds;

                Console.WriteLine($"Answer: {response.Result}\n");

                // Show metrics
                if (response.TokenUsage != null)
                {
                    Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens} " +
                        $"(in: {response.TokenUsage.InputTokens}, out: {response.TokenUsage.OutputTokens})");
                }
                Console.WriteLine($"Latency: {duration:F0}ms");

                // Estimate cost (approximate pricing)
                if (response.TokenUsage != null)
                {
                    decimal costPerInputToken = providerName switch
                    {
                        "openai" => 0.03m / 1000m,      // GPT-4: $0.03/1K input
                        "claude" => 0.015m / 1000m,     // Claude Opus: $0.015/1K input
                        "gemini" => 0.00025m / 1000m,   // Gemini Pro: $0.00025/1K input
                        _ => 0
                    };

                    decimal costPerOutputToken = providerName switch
                    {
                        "openai" => 0.06m / 1000m,      // GPT-4: $0.06/1K output
                        "claude" => 0.075m / 1000m,     // Claude Opus: $0.075/1K output
                        "gemini" => 0.0005m / 1000m,    // Gemini Pro: $0.0005/1K output
                        _ => 0
                    };

                    var cost = (response.TokenUsage.InputTokens * costPerInputToken) +
                               (response.TokenUsage.OutputTokens * costPerOutputToken);

                    Console.WriteLine($"Estimated cost: ${cost:F6}");
                }

                Console.WriteLine(new string('-', 60) + "\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR with {providerName}: {ex.Message}\n");
            }
        }

        // 6. Demonstrate provider selection based on task
        Console.WriteLine("--- Task-Based Provider Selection ---\n");

        Console.WriteLine("Scenario: You want to optimize for cost vs quality\n");

        if (availableProviders.Contains("gemini"))
        {
            Console.WriteLine("Simple task (translation) → Use Gemini (cheapest):");
            var gemini = registry.GetProvider("gemini");
            var simpleTask = await AskQuestion(gemini, "Translate 'Hello' to Spanish");
            Console.WriteLine($"Answer: {simpleTask}\n");
        }

        if (availableProviders.Contains("claude"))
        {
            Console.WriteLine("Complex reasoning task → Use Claude Opus (best reasoning):");
            var claude = registry.GetProvider("claude");
            var complexTask = await AskQuestion(claude, "What are the philosophical implications of AI consciousness?");
            Console.WriteLine($"Answer: {complexTask}\n");
        }

        if (availableProviders.Contains("openai"))
        {
            Console.WriteLine("General purpose task → Use GPT-4 (balanced):");
            var openai = registry.GetProvider("openai");
            var generalTask = await AskQuestion(openai, "Write a haiku about programming");
            Console.WriteLine($"Answer: {generalTask}\n");
        }

        Console.WriteLine("✓ Success! You can now switch providers based on your needs.");
        Console.WriteLine("\nKey takeaway:");
        Console.WriteLine("  Same code (ILLMClient interface) works with all providers.");
        Console.WriteLine("  Choose provider based on: cost, latency, quality, or features.");
    }

    static async Task<string> AskQuestion(ILLMClient llm, string question)
    {
        var messages = new List<HazinaChatMessage>
        {
            new() { Role = HazinaMessageRole.User, Text = question }
        };

        var response = await llm.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            null,
            null,
            CancellationToken.None
        );

        return response.Result ?? "No response";
    }
}
