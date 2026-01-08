using Hazina.Tools.Services.ToolAgent.Models;

namespace Hazina.Tools.Services.ToolAgent.Abstractions;

/// <summary>
/// Service for executing actions via the tool agent orchestration layer.
/// The tool agent receives high-level actions from the chat agent and orchestrates
/// which specialized tools to call to accomplish the task.
/// </summary>
/// <remarks>
/// This is Layer 2 in the 3-layer architecture:
/// - Layer 1 (Chat Agent): Decides WHAT to do → calls invoke_tool_agent
/// - Layer 2 (Tool Agent): Decides HOW to do it → calls specialized tools
/// - Layer 3 (Specialized Tools): Does the work → generates content
/// </remarks>
public interface IToolAgentService
{
    /// <summary>
    /// Executes an action by orchestrating the appropriate specialized tools.
    /// </summary>
    /// <param name="request">The tool agent request with action and context</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing executed tools and summary</returns>
    Task<ToolAgentResult> ExecuteActionAsync(
        ToolAgentRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the available actions that can be executed.
    /// </summary>
    Task<IReadOnlyList<ToolAgentAction>> GetAvailableActionsAsync(
        CancellationToken cancellationToken = default);
}
