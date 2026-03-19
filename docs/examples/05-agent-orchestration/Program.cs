using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.Agents;
using Hazina.LLMs;

namespace Hazina.Examples.AgentOrchestration;

/// <summary>
/// Demonstrates multi-agent orchestration with Hazina.
///
/// This example shows:
/// - Creating multiple specialized agents
/// - Coordinating agents to solve complex tasks
/// - Agent-to-agent communication
/// - Supervisor pattern for agent orchestration
///
/// Prerequisites:
/// - .NET 8.0 or higher
/// - OpenAI API key
///
/// Run: dotnet run
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Agent Orchestration Example ===\n");

        // 1. Setup: Get API key from environment
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

        if (string.IsNullOrEmpty(apiKey))
        {
            Console.WriteLine("ERROR: OPENAI_API_KEY environment variable not set!");
            return;
        }

        try
        {
            // 2. Create AI client
            var ai = QuickSetup.SetupOpenAI(apiKey);
            Console.WriteLine("✓ AI client initialized\n");

            // 3. Create specialized agents
            var researchAgent = new Agent("Researcher", ai)
            {
                SystemPrompt = "You are a research specialist. Your job is to gather comprehensive, accurate " +
                              "information about topics. Provide detailed, well-structured research summaries with " +
                              "key facts and insights. Focus on accuracy and depth."
            };

            var writerAgent = new Agent("Writer", ai)
            {
                SystemPrompt = "You are a professional writer. Your job is to transform research into clear, " +
                              "engaging content. Write in a friendly but professional tone. Make complex topics " +
                              "accessible. Use examples and analogies where helpful."
            };

            var editorAgent = new Agent("Editor", ai)
            {
                SystemPrompt = "You are a senior editor. Your job is to review content for clarity, accuracy, " +
                              "grammar, and tone. Provide constructive feedback and suggest improvements. " +
                              "Ensure content is polished and professional."
            };

            var supervisorAgent = new Agent("Supervisor", ai)
            {
                SystemPrompt = "You are a project supervisor. Coordinate between research, writing, and editing " +
                              "teams. Break down complex tasks, assign work appropriately, synthesize results, " +
                              "and deliver final output. Ensure quality at each step."
            };

            Console.WriteLine("✓ Created 4 specialized agents:\n");
            Console.WriteLine("  - Researcher: Gathers information");
            Console.WriteLine("  - Writer: Creates content");
            Console.WriteLine("  - Editor: Reviews and refines");
            Console.WriteLine("  - Supervisor: Coordinates workflow\n");

            // 4. Example 1: Simple sequential workflow
            Console.WriteLine("--- Example 1: Sequential Workflow ---\n");

            var topic = "What is Retrieval-Augmented Generation (RAG)?";
            Console.WriteLine($"Task: Create article about '{topic}'\n");

            // Step 1: Research
            Console.WriteLine("[Researcher] Gathering information...");
            var researchResult = await researchAgent.ExecuteAsync(
                $"Research this topic and provide key facts: {topic}"
            );
            Console.WriteLine($"Research complete: {researchResult.Length} chars\n");

            // Step 2: Write
            Console.WriteLine("[Writer] Creating article...");
            var writeResult = await writerAgent.ExecuteAsync(
                $"Write a clear, engaging 200-word article based on this research:\n\n{researchResult}"
            );
            Console.WriteLine($"Draft complete: {writeResult.Length} chars\n");

            // Step 3: Edit
            Console.WriteLine("[Editor] Reviewing article...");
            var editResult = await editorAgent.ExecuteAsync(
                $"Review this article and provide the final polished version:\n\n{writeResult}"
            );
            Console.WriteLine($"Editing complete\n");

            Console.WriteLine("=== Final Article ===\n");
            Console.WriteLine(editResult);
            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // 5. Example 2: Supervisor-coordinated workflow
            Console.WriteLine("--- Example 2: Supervisor Coordination ---\n");

            var complexTask = "Create a comprehensive guide to getting started with Hazina framework, " +
                            "including installation, basic usage, and a simple example.";

            Console.WriteLine($"Complex Task: {complexTask}\n");

            Console.WriteLine("[Supervisor] Planning workflow...");
            var supervisionResult = await supervisorAgent.ExecuteAsync(
                $"Task: {complexTask}\n\n" +
                $"You have three teams available:\n" +
                $"1. Research team - gathers information\n" +
                $"2. Writing team - creates content\n" +
                $"3. Editing team - reviews and polishes\n\n" +
                $"Break down this task, coordinate the teams, and deliver the final guide. " +
                $"Note: You are simulating team coordination. Describe what each team would do, " +
                $"then synthesize the final output."
            );

            Console.WriteLine("\n=== Supervisor's Coordinated Result ===\n");
            Console.WriteLine(supervisionResult);
            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // 6. Example 3: Parallel agent execution
            Console.WriteLine("--- Example 3: Parallel Research ---\n");

            var researchTopics = new[]
            {
                "Benefits of RAG over fine-tuning",
                "Common RAG pitfalls and how to avoid them",
                "Best practices for RAG in production"
            };

            Console.WriteLine("Researching 3 topics in parallel...\n");

            var parallelTasks = researchTopics.Select(async topic =>
            {
                Console.WriteLine($"[Researcher] Starting: {topic}");
                var result = await researchAgent.ExecuteAsync($"Research this topic concisely: {topic}");
                Console.WriteLine($"[Researcher] Completed: {topic}");
                return new { Topic = topic, Result = result };
            }).ToArray();

            var parallelResults = await Task.WhenAll(parallelTasks);

            Console.WriteLine("\n=== Research Results ===\n");
            foreach (var result in parallelResults)
            {
                Console.WriteLine($"Topic: {result.Topic}");
                Console.WriteLine($"Summary: {result.Result.Substring(0, Math.Min(150, result.Result.Length))}...\n");
            }

            // 7. Synthesize parallel results
            Console.WriteLine("[Writer] Synthesizing research into article...");
            var synthesisPrompt = "Combine these research findings into a cohesive article:\n\n" +
                                string.Join("\n\n---\n\n", parallelResults.Select(r => $"Topic: {r.Topic}\n\n{r.Result}"));

            var synthesizedArticle = await writerAgent.ExecuteAsync(synthesisPrompt);

            Console.WriteLine("\n=== Synthesized Article ===\n");
            Console.WriteLine(synthesizedArticle);

            Console.WriteLine("\n" + new string('=', 60) + "\n");
            Console.WriteLine("✓ Success! Multi-agent orchestration is working.");

            Console.WriteLine("\nKey patterns demonstrated:");
            Console.WriteLine("  1. Sequential workflow: Research → Write → Edit");
            Console.WriteLine("  2. Supervisor coordination: Plan → Delegate → Synthesize");
            Console.WriteLine("  3. Parallel execution: Multiple agents working simultaneously");
            Console.WriteLine("  4. Specialization: Each agent has specific expertise");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nERROR: {ex.Message}");
            Console.WriteLine($"\nStack trace:\n{ex.StackTrace}");
        }
    }
}

/// <summary>
/// Simple agent implementation for orchestration examples.
/// </summary>
public class Agent
{
    private readonly string _name;
    private readonly ILLMClient _llm;

    public string SystemPrompt { get; set; } = "You are a helpful assistant.";

    public Agent(string name, ILLMClient llm)
    {
        _name = name;
        _llm = llm;
    }

    public async Task<string> ExecuteAsync(string task)
    {
        var messages = new List<HazinaChatMessage>
        {
            new() { Role = HazinaMessageRole.System, Text = SystemPrompt },
            new() { Role = HazinaMessageRole.User, Text = task }
        };

        var response = await _llm.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            toolsContext: null,
            images: null,
            CancellationToken.None
        );

        return response.Result ?? "No response";
    }
}
