using Hazina.LLMs;
using Hazina.Tools.Models;
using Hazina.Tools.Services.ToolAgent.Abstractions;
using Hazina.Tools.Services.ToolAgent.Models;
using Hazina.Tools.Services.ToolAgent.ToolsContexts;
using Microsoft.Extensions.Logging;

namespace Hazina.Tools.Services.ToolAgent.Services;

/// <summary>
/// Implementation of IToolAgentService that uses LLM calls to orchestrate specialized tools.
/// This is Layer 2 in the 3-layer architecture - it receives high-level actions and decides
/// which specialized tools to call.
/// </summary>
public class ToolAgentService : IToolAgentService
{
    private readonly Func<string, Task<ILLMClient>> _clientFactory;
    private readonly ILogger<ToolAgentService>? _logger;

    private const string SystemPrompt = @"You are a tool orchestration agent for Brand2Boost.
You receive high-level actions from the chat agent and execute them by calling specialized tools.

Available actions:
- update_brand_profile: Update brand-related analysis fields
- generate_logo: Generate logo images
- store_conversation_data: Extract and store conversation data
- show_guidance: Show guidance cards to user

Available tools:
- GetAnalysisFields: Get list of available analysis fields (metadata only)
- TriggerAnalysisFieldGeneration: Trigger generation of a specific analysis field
- TriggerImageGeneration: Trigger logo/image generation
- StoreGatheredData: Store extracted data
- ShowGuidanceCard: Show UI guidance card

Your job: translate the action into the appropriate tool calls.

Be efficient - call only the tools needed for the action.
Be specific - when triggering generation, provide clear instructions.";

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolAgentService"/> class.
    /// </summary>
    /// <param name="clientFactory">Factory function that takes a task name and returns an LLM client.
    /// This will be the ModelRouter.GetClientForTaskAsync method.</param>
    /// <param name="logger">Optional logger.</param>
    public ToolAgentService(
        Func<string, Task<ILLMClient>> clientFactory,
        ILogger<ToolAgentService>? logger = null)
    {
        _clientFactory = clientFactory ?? throw new ArgumentNullException(nameof(clientFactory));
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ToolAgentResult> ExecuteActionAsync(
        ToolAgentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Action))
        {
            return new ToolAgentResult
            {
                Success = false,
                Summary = "No action specified",
                Error = "Action parameter is required"
            };
        }

        try
        {
            // Get LLM client for tool orchestration task (uses Ollama with fallback to OpenAI)
            var client = await _clientFactory("tool.orchestration");

            // Build tools context for the tool agent
            var toolsContext = new ToolAgentToolsContext(
                request.ProjectId,
                request.ChatId,
                request.UserId);

            // Build messages for the tool agent
            var messages = BuildMessages(request.Action, request.ContextHint);

            _logger?.LogInformation(
                "Tool agent executing action '{Action}' for project {ProjectId}",
                request.Action, request.ProjectId);

            // Execute with tools
            var response = await client.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                toolsContext,
                images: null,
                cancellationToken);

            var executedTools = toolsContext.ExecutedTools;

            _logger?.LogInformation(
                "Tool agent completed action '{Action}', executed {ToolCount} tools",
                request.Action, executedTools.Count);

            return new ToolAgentResult
            {
                Success = true,
                Summary = response.Result ?? "Action completed",
                ExecutedTools = executedTools
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Tool agent failed to execute action '{Action}' for project {ProjectId}",
                request.Action, request.ProjectId);

            return new ToolAgentResult
            {
                Success = false,
                Summary = "Action failed",
                Error = ex.Message
            };
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ToolAgentAction>> GetAvailableActionsAsync(
        CancellationToken cancellationToken = default)
    {
        var actions = new List<ToolAgentAction>
        {
            new()
            {
                Action = "update_brand_profile",
                Description = "Update brand profile and related analysis fields based on conversation",
                IsAsync = true,
                EstimatedDurationSeconds = 10
            },
            new()
            {
                Action = "generate_logo",
                Description = "Generate logo images based on brand identity",
                IsAsync = true,
                EstimatedDurationSeconds = 60
            },
            new()
            {
                Action = "store_conversation_data",
                Description = "Extract and store relevant data from conversation",
                IsAsync = true,
                EstimatedDurationSeconds = 5
            },
            new()
            {
                Action = "show_guidance",
                Description = "Show guidance card to user",
                IsAsync = false,
                EstimatedDurationSeconds = 1
            }
        };

        return Task.FromResult<IReadOnlyList<ToolAgentAction>>(actions);
    }

    private List<HazinaChatMessage> BuildMessages(string action, string? contextHint)
    {
        var messages = new List<HazinaChatMessage>
        {
            new(HazinaMessageRole.System, SystemPrompt),
            new(HazinaMessageRole.User, BuildUserMessage(action, contextHint))
        };

        return messages;
    }

    private string BuildUserMessage(string action, string? contextHint)
    {
        var hint = string.IsNullOrWhiteSpace(contextHint)
            ? "No additional context provided."
            : $"Context: {contextHint}";

        return action switch
        {
            "update_brand_profile" =>
                $"Execute action: update_brand_profile. {hint}\n\n" +
                "First use GetAnalysisFields to see what fields are available, " +
                "then trigger generation for relevant brand-related fields like brand-profile, mission, vision.",

            "generate_logo" =>
                $"Execute action: generate_logo. {hint}\n\n" +
                "Use TriggerImageGeneration to generate logo images based on the brand identity.",

            "store_conversation_data" =>
                $"Execute action: store_conversation_data. {hint}\n\n" +
                "Use StoreGatheredData to extract and store relevant information from the conversation.",

            "show_guidance" =>
                $"Execute action: show_guidance. {hint}\n\n" +
                "Use ShowGuidanceCard to show an appropriate guidance card to the user.",

            _ =>
                $"Execute action: {action}. {hint}\n\n" +
                "Determine the appropriate tools to call for this action."
        };
    }
}
