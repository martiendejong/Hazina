using Hazina.Agents.Coding.Memory;
using Hazina.AI.Providers.Core;

namespace Hazina.Agents.Coding;

/// <summary>
/// Main coding agent orchestrator.
/// Sets up GLM planner, tool executor, and agent loop.
/// Manages one full coding task from start to finish.
/// </summary>
public class CodingAgent
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly CodingAgentOptions _options;

    public CodingAgent(IProviderOrchestrator orchestrator, CodingAgentOptions? options = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _options = options ?? new CodingAgentOptions();
    }

    /// <summary>
    /// Run a coding task through the agent loop
    /// </summary>
    public async Task<AgentRunResult> RunAsync(CodingTask task)
    {
        if (task == null)
        {
            throw new ArgumentNullException(nameof(task));
        }

        if (string.IsNullOrWhiteSpace(task.Description))
        {
            throw new ArgumentException("Task description is required", nameof(task));
        }

        if (string.IsNullOrWhiteSpace(task.WorkingDirectory))
        {
            throw new ArgumentException("Working directory is required", nameof(task));
        }

        if (!Directory.Exists(task.WorkingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Working directory not found: {task.WorkingDirectory}"
            );
        }

        var taskId = AgentSummaryStore.GenerateTaskId(task.Description);

        try
        {
            var planner = new GlmPlanner(_orchestrator, _options.PlannerOptions);

            var executor = new ToolExecutor(task.WorkingDirectory);

            var memoryStore = new AgentSummaryStore(task.WorkingDirectory);

            if (_options.ClearMemoryOnStart)
            {
                memoryStore.ClearSummaries(taskId);
            }

            var loop = new AgentLoop(planner, executor, memoryStore, taskId);

            var result = await loop.RunAsync(task);

            return result;
        }
        catch (Exception ex)
        {
            return new AgentRunResult
            {
                Success = false,
                Iterations = 0,
                Summary = $"Agent failed to initialize: {ex.Message}",
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Create a coding task from simple parameters
    /// </summary>
    public static CodingTask CreateTask(
        string description,
        string workingDirectory,
        int maxIterations = 5,
        string? testCommand = null)
    {
        return new CodingTask
        {
            Description = description,
            WorkingDirectory = workingDirectory,
            MaxIterations = maxIterations,
            TestCommand = testCommand
        };
    }
}

/// <summary>
/// Configuration options for coding agent
/// </summary>
public class CodingAgentOptions
{
    /// <summary>
    /// GLM planner options
    /// </summary>
    public GlmPlannerOptions PlannerOptions { get; set; } = new GlmPlannerOptions();

    /// <summary>
    /// Clear memory on start (default: false)
    /// </summary>
    public bool ClearMemoryOnStart { get; set; } = false;
}
