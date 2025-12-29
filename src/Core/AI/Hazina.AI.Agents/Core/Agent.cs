using Hazina.AI.Providers.Core;
using Hazina.AI.Agents.Tools;
using Hazina.Neurochain.Core;
using System.Text.Json;

namespace Hazina.AI.Agents.Core;

/// <summary>
/// Base agent class with tool calling capabilities
/// </summary>
public class Agent
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly NeuroChainOrchestrator? _neurochain;
    private readonly List<AgentTool> _tools = new();
    private readonly AgentConfig _config;
    private readonly List<AgentMessage> _conversationHistory = new();

    public string Name { get; }
    public string Description { get; }
    public IReadOnlyList<AgentTool> Tools => _tools.AsReadOnly();

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
    /// Register a tool for this agent to use
    /// </summary>
    public void RegisterTool(AgentTool tool)
    {
        if (!_tools.Any(t => t.Name == tool.Name))
        {
            _tools.Add(tool);
        }
    }

    /// <summary>
    /// Execute agent task
    /// </summary>
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
    /// Get conversation history
    /// </summary>
    public IReadOnlyList<AgentMessage> GetConversationHistory() => _conversationHistory.AsReadOnly();

    /// <summary>
    /// Clear conversation history
    /// </summary>
    public void ClearHistory() => _conversationHistory.Clear();
}

/// <summary>
/// Agent configuration
/// </summary>
public class AgentConfig
{
    public bool UseNeurochain { get; set; } = false;
    public double MinConfidence { get; set; } = 0.8;
    public int MaxToolIterations { get; set; } = 10;
}

/// <summary>
/// Agent response
/// </summary>
public class AgentResponse
{
    public string AgentName { get; set; } = string.Empty;
    public string Task { get; set; } = string.Empty;
    public string Result { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<string> ToolsUsed { get; set; } = new();
}

/// <summary>
/// Agent message
/// </summary>
public class AgentMessage
{
    public AgentRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Dictionary<string, object>? Context { get; set; }
}

/// <summary>
/// Agent role
/// </summary>
public enum AgentRole
{
    User,
    Assistant,
    System
}

/// <summary>
/// Tool call
/// </summary>
public class ToolCall
{
    public string ToolName { get; set; } = string.Empty;
    public Dictionary<string, object> Arguments { get; set; } = new();
}
