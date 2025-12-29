using Hazina.AI.Agents.Core;
using Hazina.AI.Providers.Core;

namespace Hazina.AI.Agents.Coordination;

/// <summary>
/// Coordinates multiple agents working together
/// </summary>
public class MultiAgentCoordinator
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly List<Agent> _agents = new();
    private readonly CoordinationStrategy _strategy;

    public MultiAgentCoordinator(
        IProviderOrchestrator orchestrator,
        CoordinationStrategy strategy = CoordinationStrategy.Sequential)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _strategy = strategy;
    }

    /// <summary>
    /// Register an agent
    /// </summary>
    public void RegisterAgent(Agent agent)
    {
        if (!_agents.Any(a => a.Name == agent.Name))
        {
            _agents.Add(agent);
        }
    }

    /// <summary>
    /// Execute task with multiple agents
    /// </summary>
    public async Task<CoordinationResult> ExecuteAsync(
        string task,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var result = new CoordinationResult
        {
            Task = task,
            StartTime = DateTime.UtcNow
        };

        try
        {
            switch (_strategy)
            {
                case CoordinationStrategy.Sequential:
                    result = await ExecuteSequentialAsync(task, context, cancellationToken);
                    break;

                case CoordinationStrategy.Parallel:
                    result = await ExecuteParallelAsync(task, context, cancellationToken);
                    break;

                case CoordinationStrategy.Debate:
                    result = await ExecuteDebateAsync(task, context, cancellationToken);
                    break;

                case CoordinationStrategy.Hierarchical:
                    result = await ExecuteHierarchicalAsync(task, context, cancellationToken);
                    break;

                default:
                    result.Success = false;
                    result.Error = $"Unknown strategy: {_strategy}";
                    break;
            }
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;
        return result;
    }

    /// <summary>
    /// Execute agents sequentially (pipeline)
    /// </summary>
    private async Task<CoordinationResult> ExecuteSequentialAsync(
        string task,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var result = new CoordinationResult
        {
            Task = task,
            Strategy = CoordinationStrategy.Sequential,
            StartTime = DateTime.UtcNow
        };

        var currentInput = task;
        var sharedContext = context ?? new Dictionary<string, object>();

        foreach (var agent in _agents)
        {
            var agentResponse = await agent.ExecuteAsync(currentInput, sharedContext, cancellationToken);
            result.AgentResponses.Add(agentResponse);

            if (!agentResponse.Success)
            {
                result.Success = false;
                result.Error = $"Agent {agent.Name} failed: {agentResponse.Error}";
                return result;
            }

            // Output of one agent becomes input to next
            currentInput = agentResponse.Result;
        }

        result.Success = true;
        result.FinalAnswer = currentInput;
        return result;
    }

    /// <summary>
    /// Execute agents in parallel
    /// </summary>
    private async Task<CoordinationResult> ExecuteParallelAsync(
        string task,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var result = new CoordinationResult
        {
            Task = task,
            Strategy = CoordinationStrategy.Parallel,
            StartTime = DateTime.UtcNow
        };

        var tasks = _agents.Select(agent => agent.ExecuteAsync(task, context, cancellationToken));
        var responses = await Task.WhenAll(tasks);

        result.AgentResponses.AddRange(responses);
        result.Success = responses.All(r => r.Success);

        if (result.Success)
        {
            // Aggregate responses
            result.FinalAnswer = await AggregateResponsesAsync(responses, task, cancellationToken);
        }
        else
        {
            result.Error = "One or more agents failed";
        }

        return result;
    }

    /// <summary>
    /// Execute agents in debate/discussion mode
    /// </summary>
    private async Task<CoordinationResult> ExecuteDebateAsync(
        string task,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var result = new CoordinationResult
        {
            Task = task,
            Strategy = CoordinationStrategy.Debate,
            StartTime = DateTime.UtcNow
        };

        const int maxRounds = 3;
        var currentContext = context ?? new Dictionary<string, object>();
        var previousResponses = new List<string>();

        for (int round = 0; round < maxRounds; round++)
        {
            var roundResponses = new List<AgentResponse>();

            foreach (var agent in _agents)
            {
                var debateTask = BuildDebateTask(task, round, previousResponses);
                var response = await agent.ExecuteAsync(debateTask, currentContext, cancellationToken);
                roundResponses.Add(response);
                result.AgentResponses.Add(response);

                if (response.Success)
                {
                    previousResponses.Add($"[{agent.Name}]: {response.Result}");
                }
            }

            // Check for consensus
            if (await HasConsensusAsync(roundResponses, cancellationToken))
            {
                break;
            }
        }

        result.Success = true;
        result.FinalAnswer = await SynthesizeDebateAsync(result.AgentResponses, task, cancellationToken);
        return result;
    }

    /// <summary>
    /// Execute with hierarchical coordination
    /// </summary>
    private async Task<CoordinationResult> ExecuteHierarchicalAsync(
        string task,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var result = new CoordinationResult
        {
            Task = task,
            Strategy = CoordinationStrategy.Hierarchical,
            StartTime = DateTime.UtcNow
        };

        if (_agents.Count == 0)
        {
            result.Success = false;
            result.Error = "No agents available";
            return result;
        }

        // First agent acts as coordinator
        var coordinator = _agents[0];
        var workers = _agents.Skip(1).ToList();

        // Coordinator breaks down task
        var planningTask = $"Break down this task into subtasks for {workers.Count} workers:\n{task}";
        var planResponse = await coordinator.ExecuteAsync(planningTask, context, cancellationToken);
        result.AgentResponses.Add(planResponse);

        if (!planResponse.Success)
        {
            result.Success = false;
            result.Error = "Coordinator failed to plan";
            return result;
        }

        // Parse subtasks (simple parsing)
        var subtasks = ParseSubtasks(planResponse.Result);

        // Execute subtasks with workers
        var workerTasks = new List<Task<AgentResponse>>();
        for (int i = 0; i < Math.Min(subtasks.Count, workers.Count); i++)
        {
            workerTasks.Add(workers[i].ExecuteAsync(subtasks[i], context, cancellationToken));
        }

        var workerResponses = await Task.WhenAll(workerTasks);
        result.AgentResponses.AddRange(workerResponses);

        // Coordinator synthesizes results
        var synthesisTask = $"Synthesize these worker results for the task '{task}':\n" +
            string.Join("\n", workerResponses.Select((r, i) => $"Worker {i + 1}: {r.Result}"));

        var synthesisResponse = await coordinator.ExecuteAsync(synthesisTask, context, cancellationToken);
        result.AgentResponses.Add(synthesisResponse);

        result.Success = synthesisResponse.Success;
        result.FinalAnswer = synthesisResponse.Result;
        result.Error = synthesisResponse.Error;

        return result;
    }

    /// <summary>
    /// Build debate task for a round
    /// </summary>
    private string BuildDebateTask(string originalTask, int round, List<string> previousResponses)
    {
        if (round == 0)
        {
            return originalTask;
        }

        var task = $"Task: {originalTask}\n\nPrevious round responses:\n";
        task += string.Join("\n", previousResponses);
        task += "\n\nProvide your perspective considering the above responses.";
        return task;
    }

    /// <summary>
    /// Check if agents have reached consensus
    /// </summary>
    private async Task<bool> HasConsensusAsync(
        List<AgentResponse> responses,
        CancellationToken cancellationToken)
    {
        // Simple similarity check - in practice, would use embeddings or LLM
        var successfulResponses = responses.Where(r => r.Success).Select(r => r.Result).ToList();

        if (successfulResponses.Count < 2)
            return false;

        // Check if all responses are similar
        var first = successfulResponses[0];
        return successfulResponses.All(r => CalculateSimilarity(first, r) > 0.7);
    }

    /// <summary>
    /// Simple text similarity
    /// </summary>
    private double CalculateSimilarity(string text1, string text2)
    {
        var words1 = text1.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = text2.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var set1 = new HashSet<string>(words1);
        var set2 = new HashSet<string>(words2);

        var intersection = set1.Intersect(set2).Count();
        var union = set1.Union(set2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    /// <summary>
    /// Aggregate parallel responses
    /// </summary>
    private async Task<string> AggregateResponsesAsync(
        AgentResponse[] responses,
        string originalTask,
        CancellationToken cancellationToken)
    {
        var prompt = $"Aggregate these agent responses for the task '{originalTask}':\n\n";
        for (int i = 0; i < responses.Length; i++)
        {
            prompt += $"Agent {i + 1}: {responses[i].Result}\n\n";
        }
        prompt += "Provide a synthesized answer.";

        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage { Role = HazinaMessageRole.User, Text = prompt }
        };

        var response = await _orchestrator.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            null,
            null,
            cancellationToken
        );

        return response.Result;
    }

    /// <summary>
    /// Synthesize debate results
    /// </summary>
    private async Task<string> SynthesizeDebateAsync(
        List<AgentResponse> responses,
        string originalTask,
        CancellationToken cancellationToken)
    {
        var prompt = $"Synthesize the final answer from this debate on '{originalTask}':\n\n";
        foreach (var response in responses)
        {
            prompt += $"{response.AgentName}: {response.Result}\n\n";
        }
        prompt += "What is the best synthesized answer?";

        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage { Role = HazinaMessageRole.User, Text = prompt }
        };

        var llmResponse = await _orchestrator.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            null,
            null,
            cancellationToken
        );

        return llmResponse.Result;
    }

    /// <summary>
    /// Parse subtasks from coordinator response
    /// </summary>
    private List<string> ParseSubtasks(string response)
    {
        var subtasks = new List<string>();
        var lines = response.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("-") || trimmed.StartsWith("•") ||
                (trimmed.Length > 2 && char.IsDigit(trimmed[0]) && trimmed[1] == '.'))
            {
                var task = trimmed.TrimStart('-', '•', ' ', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.');
                subtasks.Add(task.Trim());
            }
        }

        return subtasks.Count > 0 ? subtasks : new List<string> { response };
    }
}

/// <summary>
/// Coordination strategies
/// </summary>
public enum CoordinationStrategy
{
    Sequential,   // Agents work in pipeline
    Parallel,     // Agents work independently and results are aggregated
    Debate,       // Agents debate/discuss to reach consensus
    Hierarchical  // One agent coordinates others
}

/// <summary>
/// Coordination result
/// </summary>
public class CoordinationResult
{
    public string Task { get; set; } = string.Empty;
    public CoordinationStrategy Strategy { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string FinalAnswer { get; set; } = string.Empty;
    public string? Error { get; set; }
    public List<AgentResponse> AgentResponses { get; set; } = new();
}
