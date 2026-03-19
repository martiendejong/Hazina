using Hazina.AI.Providers.Core;
using Hazina.AI.Agents.Tools;
using Hazina.Neurochain.Core;
using System.Text.Json;

namespace Hazina.AI.Agents.Core;

/// <summary>
/// Base agent class with tool calling and NeuroChain reasoning capabilities.
/// </summary>
/// <remarks>
/// <para>
/// An <see cref="Agent"/> wraps an <see cref="IProviderOrchestrator"/> and an optional
/// <see cref="NeuroChainOrchestrator"/> to execute tasks. When tools are registered, the agent
/// enters an agentic loop: it calls the LLM, parses any <c>TOOL: ToolName(args)</c> directives
/// from the response, executes the matching <see cref="AgentTool"/>, and feeds results back until
/// the model produces a final answer (up to <see cref="AgentConfig.MaxToolIterations"/> iterations).
/// </para>
/// <para>
/// Conversation history is retained in-memory across <see cref="ExecuteAsync"/> calls. Call
/// <see cref="ClearHistory"/> to reset between independent tasks.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var orchestrator = new ProviderOrchestrator();
/// orchestrator.RegisterProvider("openai", openAiClient, openAiMetadata);
///
/// var agent = new Agent("CodeReviewer", "Reviews code for quality issues", orchestrator);
/// agent.RegisterTool(new CalculatorTool());
///
/// var result = await agent.ExecuteAsync("Calculate the cyclomatic complexity of this method: ...");
/// Console.WriteLine(result.Success ? result.Result : result.Error);
/// </code>
/// </example>
public class Agent
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly NeuroChainOrchestrator? _neurochain;
    private readonly List<AgentTool> _tools = new();
    private readonly AgentConfig _config;
    private readonly List<AgentMessage> _conversationHistory = new();

    /// <summary>Gets the agent's display name.</summary>
    public string Name { get; }

    /// <summary>Gets the agent's role description, injected into the system prompt.</summary>
    public string Description { get; }

    /// <summary>Gets the read-only list of tools registered with this agent.</summary>
    public IReadOnlyList<AgentTool> Tools => _tools.AsReadOnly();

    /// <summary>
    /// Initializes a new <see cref="Agent"/> instance.
    /// </summary>
    /// <param name="name">Display name for the agent (used in logging and responses).</param>
    /// <param name="description">Role description injected into the system prompt.</param>
    /// <param name="orchestrator">LLM provider orchestrator used to generate responses.</param>
    /// <param name="neurochain">Optional NeuroChain orchestrator for structured reasoning. When provided
    /// and <see cref="AgentConfig.UseNeurochain"/> is <see langword="true"/>, reasoning goes through
    /// NeuroChain instead of the raw LLM.</param>
    /// <param name="config">Optional agent configuration. Defaults to <see cref="AgentConfig"/> with
    /// sensible defaults when <see langword="null"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="name"/>, <paramref name="description"/>, or
    /// <paramref name="orchestrator"/> is <see langword="null"/>.
    /// </exception>
    public Agent(
        string name,
        string description,
        IProviderOrchestrator orchestrator,
        NeuroChainOrchestrator? neurochain = null,
        AgentConfig? config = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Description = description ?? throw new ArgumentNullException(nameof(description));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _neurochain = neurochain;
        _config = config ?? new AgentConfig();
    }

    /// <summary>
    /// Registers a tool with this agent. Duplicate tool names are silently ignored.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    public void RegisterTool(AgentTool tool)
    {
        if (!_tools.Any(t => t.Name == tool.Name))
        {
            _tools.Add(tool);
        }
    }

    /// <summary>
    /// Executes a task, automatically handling tool calls until a final response is produced.
    /// </summary>
    /// <param name="task">Natural-language description of the task for the agent to perform.</param>
    /// <param name="context">Optional key/value pairs injected into the system prompt as context.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// An <see cref="AgentResponse"/> with <see cref="AgentResponse.Success"/> set to
    /// <see langword="true"/> and <see cref="AgentResponse.Result"/> populated on success,
    /// or <see cref="AgentResponse.Error"/> populated on failure.
    /// </returns>
    public async Task<AgentResponse> ExecuteAsync(
        string task,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var response = new AgentResponse
        {
            AgentName = Name,
            Task = task,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Add task to conversation history
            _conversationHistory.Add(new AgentMessage
            {
                Role = AgentRole.User,
                Content = task,
                Context = context
            });

            // Execute with or without tools
            if (_tools.Count > 0)
            {
                response.Result = await ExecuteWithToolsAsync(task, context, cancellationToken);
            }
            else
            {
                response.Result = await ExecuteSimpleAsync(task, context, cancellationToken);
            }

            response.Success = true;

            // Add result to conversation history
            _conversationHistory.Add(new AgentMessage
            {
                Role = AgentRole.Assistant,
                Content = response.Result
            });
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = ex.Message;
        }

        response.EndTime = DateTime.UtcNow;
        response.Duration = response.EndTime - response.StartTime;
        return response;
    }

    /// <summary>
    /// Execute simple task without tools
    /// </summary>
    private async Task<string> ExecuteSimpleAsync(
        string task,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSystemPrompt(context);
        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = systemPrompt
            },
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = task
            }
        };

        if (_neurochain != null && _config.UseNeurochain)
        {
            var result = await _neurochain.ReasonAsync(
                task,
                new ReasoningContext
                {
                    MinConfidence = _config.MinConfidence,
                    Domain = $"Agent: {Name}"
                },
                cancellationToken
            );
            return result.FinalAnswer;
        }
        else
        {
            var response = await _orchestrator.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                null,
                null,
                cancellationToken
            );
            return response.Result;
        }
    }

    /// <summary>
    /// Execute task with tool calling
    /// </summary>
    private async Task<string> ExecuteWithToolsAsync(
        string task,
        Dictionary<string, object>? context,
        CancellationToken cancellationToken)
    {
        var systemPrompt = BuildSystemPromptWithTools(context);
        var userMessage = task;

        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.System,
                Text = systemPrompt
            },
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = userMessage
            }
        };

        int iteration = 0;
        const int maxIterations = 10;

        while (iteration < maxIterations)
        {
            var response = await _orchestrator.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                null,
                null,
                cancellationToken
            );

            // Check if response contains tool calls
            var toolCalls = ParseToolCalls(response.Result);

            if (toolCalls.Count == 0)
            {
                // No tool calls, return final answer
                return response.Result;
            }

            // Execute tool calls
            foreach (var toolCall in toolCalls)
            {
                var toolResult = await ExecuteToolAsync(toolCall, cancellationToken);

                messages.Add(new HazinaChatMessage
                {
                    Role = HazinaMessageRole.Assistant,
                    Text = $"[Tool Call: {toolCall.ToolName}]"
                });

                messages.Add(new HazinaChatMessage
                {
                    Role = HazinaMessageRole.User,
                    Text = $"[Tool Result: {toolResult}]"
                });
            }

            iteration++;
        }

        return "Max iterations reached. Unable to complete task.";
    }

    /// <summary>
    /// Parse tool calls from LLM response
    /// </summary>
    private List<ToolCall> ParseToolCalls(string response)
    {
        var toolCalls = new List<ToolCall>();

        // Simple pattern matching for tool calls
        // Format: TOOL: ToolName(arg1=value1, arg2=value2)
        var lines = response.Split('\n');
        foreach (var line in lines)
        {
            if (line.TrimStart().StartsWith("TOOL:", StringComparison.OrdinalIgnoreCase))
            {
                var toolSpec = line.Substring(line.IndexOf(':') + 1).Trim();
                var toolCall = ParseToolSpec(toolSpec);
                if (toolCall != null)
                {
                    toolCalls.Add(toolCall);
                }
            }
        }

        return toolCalls;
    }

    /// <summary>
    /// Parse tool specification
    /// </summary>
    private ToolCall? ParseToolSpec(string spec)
    {
        // Parse: ToolName(arg1=value1, arg2=value2)
        var parenIndex = spec.IndexOf('(');
        if (parenIndex < 0)
            return null;

        var toolName = spec.Substring(0, parenIndex).Trim();
        var argsStr = spec.Substring(parenIndex + 1, spec.LastIndexOf(')') - parenIndex - 1);

        var args = new Dictionary<string, object>();
        if (!string.IsNullOrWhiteSpace(argsStr))
        {
            var argPairs = argsStr.Split(',');
            foreach (var pair in argPairs)
            {
                var parts = pair.Split('=', 2);
                if (parts.Length == 2)
                {
                    args[parts[0].Trim()] = parts[1].Trim();
                }
            }
        }

        return new ToolCall
        {
            ToolName = toolName,
            Arguments = args
        };
    }

    /// <summary>
    /// Execute a tool call
    /// </summary>
    private async Task<string> ExecuteToolAsync(ToolCall toolCall, CancellationToken cancellationToken)
    {
        var tool = _tools.FirstOrDefault(t => t.Name.Equals(toolCall.ToolName, StringComparison.OrdinalIgnoreCase));
        if (tool == null)
        {
            return $"Error: Tool '{toolCall.ToolName}' not found";
        }

        try
        {
            var result = await tool.ExecuteAsync(toolCall.Arguments, cancellationToken);
            return result.Success ? result.Output : $"Error: {result.Error}";
        }
        catch (Exception ex)
        {
            return $"Error executing tool: {ex.Message}";
        }
    }

    /// <summary>
    /// Build system prompt
    /// </summary>
    private string BuildSystemPrompt(Dictionary<string, object>? context)
    {
        var prompt = $"You are {Name}, an AI agent. {Description}";

        if (context != null && context.Count > 0)
        {
            prompt += "\n\nContext:\n";
            foreach (var kvp in context)
            {
                prompt += $"- {kvp.Key}: {kvp.Value}\n";
            }
        }

        return prompt;
    }

    /// <summary>
    /// Build system prompt with tool descriptions
    /// </summary>
    private string BuildSystemPromptWithTools(Dictionary<string, object>? context)
    {
        var prompt = BuildSystemPrompt(context);

        if (_tools.Count > 0)
        {
            prompt += "\n\nAvailable Tools:\n";
            foreach (var tool in _tools)
            {
                prompt += $"\n- {tool.Name}: {tool.Description}\n";
                prompt += "  Parameters:\n";
                foreach (var param in tool.Parameters)
                {
                    prompt += $"    - {param.Key} ({param.Value.Type}): {param.Value.Description}\n";
                }
            }

            prompt += "\nTo use a tool, respond with:\nTOOL: ToolName(param1=value1, param2=value2)\n";
        }

        return prompt;
    }

    /// <summary>
    /// Returns the full conversation history accumulated across all <see cref="ExecuteAsync"/> calls.
    /// </summary>
    /// <returns>Read-only list of messages in chronological order.</returns>
    public IReadOnlyList<AgentMessage> GetConversationHistory() => _conversationHistory.AsReadOnly();

    /// <summary>
    /// Clears the in-memory conversation history, resetting the agent to a clean state.
    /// </summary>
    public void ClearHistory() => _conversationHistory.Clear();
}

/// <summary>
/// Configuration options for an <see cref="Agent"/> instance.
/// </summary>
public class AgentConfig
{
    /// <summary>
    /// When <see langword="true"/>, tasks are routed through the NeuroChain orchestrator for
    /// structured multi-step reasoning instead of a direct LLM call.
    /// Requires a <c>neurochain</c> instance to be passed to the <see cref="Agent"/> constructor.
    /// Default: <see langword="false"/>.
    /// </summary>
    public bool UseNeurochain { get; set; } = false;

    /// <summary>
    /// Minimum confidence threshold (0.0–1.0) for NeuroChain reasoning to be accepted.
    /// Responses below this threshold trigger re-reasoning. Default: <c>0.8</c>.
    /// </summary>
    public double MinConfidence { get; set; } = 0.8;

    /// <summary>
    /// Maximum number of tool-calling iterations per <see cref="Agent.ExecuteAsync"/> call.
    /// Prevents infinite loops in agentic workflows. Default: <c>10</c>.
    /// </summary>
    public int MaxToolIterations { get; set; } = 10;
}

/// <summary>
/// Result returned by <see cref="Agent.ExecuteAsync"/>.
/// </summary>
public class AgentResponse
{
    /// <summary>Gets or sets the name of the agent that produced this response.</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>Gets or sets the task description that was executed.</summary>
    public string Task { get; set; } = string.Empty;

    /// <summary>Gets or sets the final text result from the agent. Empty string on failure.</summary>
    public string Result { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when task execution began.</summary>
    public DateTime StartTime { get; set; }

    /// <summary>Gets or sets the UTC timestamp when task execution completed.</summary>
    public DateTime EndTime { get; set; }

    /// <summary>Gets or sets the total elapsed time for the task.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>Gets or sets a value indicating whether the task completed successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? Error { get; set; }

    /// <summary>Gets or sets the names of tools invoked during task execution.</summary>
    public List<string> ToolsUsed { get; set; } = new();
}

/// <summary>
/// A single message in an agent's conversation history.
/// </summary>
public class AgentMessage
{
    /// <summary>Gets or sets the role of the participant who produced this message.</summary>
    public AgentRole Role { get; set; }

    /// <summary>Gets or sets the text content of the message.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the UTC timestamp when the message was created.</summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets optional context data attached to the message.</summary>
    public Dictionary<string, object>? Context { get; set; }
}

/// <summary>
/// Role of a participant in an agent conversation.
/// </summary>
public enum AgentRole
{
    /// <summary>Human or system input initiating a task.</summary>
    User,

    /// <summary>Response generated by the agent/LLM.</summary>
    Assistant,

    /// <summary>System-level instructions (e.g., persona or tool descriptions).</summary>
    System
}

/// <summary>
/// Represents a parsed tool call extracted from an LLM response.
/// </summary>
public class ToolCall
{
    /// <summary>Gets or sets the name of the tool to invoke.</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Gets or sets the named arguments to pass to the tool.</summary>
    public Dictionary<string, object> Arguments { get; set; } = new();
}
