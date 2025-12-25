using System.Text;

namespace Hazina.LLMClientTools.Examples;

/// <summary>
/// Example showing how to use Claude Code and Codex tools to run
/// multiple AI coding agents working on different tasks simultaneously.
/// </summary>
public static class MultiAgentCodingExample
{
    /// <summary>
    /// Example: Parallel execution of multiple coding agents on independent tasks
    /// </summary>
    public static async Task<Dictionary<string, string>> RunParallelCodingTasksAsync(
        Dictionary<string, (string task, string workingDir)> tasks,
        CancellationToken cancel = default)
    {
        var results = new Dictionary<string, string>();

        // Run all tasks in parallel
        var taskList = tasks.Select(async kvp =>
        {
            var (agentName, (task, workingDir)) = (kvp.Key, kvp.Value);

            string result;
            try
            {
                // Decide which tool to use based on agent name or task complexity
                if (agentName.Contains("claude", StringComparison.OrdinalIgnoreCase))
                {
                    result = await ClaudeCodeRunner.RunClaudeCodeAsync(
                        task,
                        workingDirectory: workingDir,
                        timeoutSeconds: 600,
                        cancel: cancel);
                }
                else if (agentName.Contains("codex", StringComparison.OrdinalIgnoreCase))
                {
                    result = await CodexRunner.RunCodexAsync(
                        task,
                        workingDirectory: workingDir,
                        timeoutSeconds: 600,
                        cancel: cancel);
                }
                else
                {
                    // Default to Claude Code
                    result = await ClaudeCodeRunner.RunClaudeCodeAsync(
                        task,
                        workingDirectory: workingDir,
                        timeoutSeconds: 600,
                        cancel: cancel);
                }
            }
            catch (Exception ex)
            {
                result = $"ERROR: {ex.Message}";
            }

            return (agentName, result);
        });

        var completedTasks = await Task.WhenAll(taskList);

        foreach (var (agentName, result) in completedTasks)
        {
            results[agentName] = result;
        }

        return results;
    }

    /// <summary>
    /// Example usage demonstrating parallel coding agents
    /// </summary>
    public static async Task DemoParallelAgentsAsync()
    {
        Console.WriteLine("=== Multi-Agent Parallel Coding Demo ===\n");

        // Define multiple independent coding tasks
        var tasks = new Dictionary<string, (string task, string workingDir)>
        {
            ["claude-frontend"] = (
                "Implement new user profile page with React hooks and TypeScript",
                @"C:\Projects\MyApp\frontend"
            ),
            ["claude-backend"] = (
                "Add database migration for user preferences table using Entity Framework",
                @"C:\Projects\MyApp\backend"
            ),
            ["codex-tests"] = (
                "Write comprehensive unit tests for the authentication service using xUnit",
                @"C:\Projects\MyApp\backend\tests"
            ),
            ["codex-docs"] = (
                "Generate detailed API documentation from OpenAPI 3.0 spec with examples",
                @"C:\Projects\MyApp\docs"
            )
        };

        Console.WriteLine($"Starting {tasks.Count} parallel coding agents...\n");

        // Execute all tasks in parallel
        var results = await RunParallelCodingTasksAsync(tasks, CancellationToken.None);

        // Display results
        foreach (var (agent, result) in results)
        {
            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"Agent: {agent}");
            Console.WriteLine($"{'=',-60}");
            Console.WriteLine(result);
        }

        Console.WriteLine($"\n{'=',-60}");
        Console.WriteLine("All agents completed!");
    }

    /// <summary>
    /// Example showing sequential agent coordination
    /// </summary>
    public static async Task DemoSequentialAgentsAsync()
    {
        Console.WriteLine("=== Multi-Agent Sequential Coding Demo ===\n");

        var projectDir = @"C:\Projects\MyApp";

        // Step 1: Claude Code creates the feature
        Console.WriteLine("Step 1: Claude Code implementing feature...");
        var implementationResult = await ClaudeCodeRunner.RunClaudeCodeAsync(
            "Implement a new REST API endpoint for user profile updates with validation",
            workingDirectory: projectDir,
            timeoutSeconds: 300
        );
        Console.WriteLine($"Result: {implementationResult}\n");

        // Step 2: Codex writes tests
        Console.WriteLine("Step 2: Codex writing tests...");
        var testResult = await CodexRunner.RunCodexAsync(
            "Write comprehensive unit and integration tests for the user profile update endpoint",
            workingDirectory: projectDir,
            timeoutSeconds: 300
        );
        Console.WriteLine($"Result: {testResult}\n");

        // Step 3: Claude Code refactors based on test findings
        Console.WriteLine("Step 3: Claude Code refactoring...");
        var refactorResult = await ClaudeCodeRunner.RunClaudeCodeAsync(
            "Refactor the user profile endpoint to improve testability and separation of concerns",
            workingDirectory: projectDir,
            timeoutSeconds: 300
        );
        Console.WriteLine($"Result: {refactorResult}\n");

        Console.WriteLine("Sequential workflow completed!");
    }
}
