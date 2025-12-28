using Hazina.LLMs.GoogleADK.Core;
using Hazina.LLMs.GoogleADK.Events;
using Microsoft.Extensions.Logging;

namespace Hazina.LLMs.GoogleADK.Agents;

/// <summary>
/// LLM-powered agent that uses language models for reasoning and decision-making.
/// This agent type leverages LLMs for flexible, language-focused tasks.
/// </summary>
public class LlmAgent : BaseAgent
{
    private readonly ILLMClient _llmClient;
    private readonly List<HazinaChatMessage> _conversationHistory = new();
    private readonly int _maxHistorySize;
    private IToolsContext? _toolsContext;

    /// <summary>
    /// System instructions for the LLM
    /// </summary>
    public string SystemInstructions { get; set; } = string.Empty;

    /// <summary>
    /// Response format for LLM output
    /// </summary>
    public HazinaChatResponseFormat ResponseFormat { get; set; } = HazinaChatResponseFormat.Text;

    /// <summary>
    /// Whether to stream responses
    /// </summary>
    public bool EnableStreaming { get; set; } = false;

    /// <summary>
    /// Conversation history
    /// </summary>
    public IReadOnlyList<HazinaChatMessage> ConversationHistory => _conversationHistory.AsReadOnly();

    public LlmAgent(
        string name,
        ILLMClient llmClient,
        AgentContext? context = null,
        int maxHistorySize = 50) : base(name, context)
    {
        _llmClient = llmClient ?? throw new ArgumentNullException(nameof(llmClient));
        _maxHistorySize = maxHistorySize;
    }

    /// <summary>
    /// Set tools context for the agent
    /// </summary>
    public void SetToolsContext(IToolsContext toolsContext)
    {
        _toolsContext = toolsContext;
    }

    /// <summary>
    /// Clear conversation history
    /// </summary>
    public void ClearHistory()
    {
        _conversationHistory.Clear();
        Context.Log(LogLevel.Information, "Conversation history cleared");
    }

    /// <summary>
    /// Add a message to conversation history
    /// </summary>
    public void AddMessage(HazinaMessageRole role, string content)
    {
        var message = new HazinaChatMessage(role, content);
        _conversationHistory.Add(message);

        // Trim history if it exceeds max size
        while (_conversationHistory.Count > _maxHistorySize)
        {
            _conversationHistory.RemoveAt(0);
        }

        Context.EmitEvent(new MessageEvent
        {
            Role = role.Role,
            Content = content
        });
    }

    protected override async Task OnInitializeAsync(CancellationToken cancellationToken)
    {
        // Add system instructions to conversation if provided
        if (!string.IsNullOrEmpty(SystemInstructions))
        {
            _conversationHistory.Add(new HazinaChatMessage(HazinaMessageRole.System, SystemInstructions));
            Context.Log(LogLevel.Information, "System instructions set");
        }

        await base.OnInitializeAsync(cancellationToken);
    }

    protected override async Task<AgentResult> OnExecuteAsync(string input, CancellationToken cancellationToken)
    {
        try
        {
            // Add user message to history
            AddMessage(HazinaMessageRole.User, input);

            // Prepare messages
            var messages = new List<HazinaChatMessage>(_conversationHistory);

            string response;
            TokenUsageInfo? usage = null;

            if (EnableStreaming)
            {
                // Streaming response
                var chunks = new List<string>();

                var result = await _llmClient.GetResponseStream(
                    messages,
                    chunk =>
                    {
                        chunks.Add(chunk);
                        Context.EmitEvent(new StreamChunkEvent
                        {
                            Chunk = chunk,
                            ChunkIndex = chunks.Count - 1
                        });
                    },
                    ResponseFormat,
                    _toolsContext,
                    null,
                    cancellationToken
                );

                response = result.Result;
                usage = result.TokenUsage;
            }
            else
            {
                // Non-streaming response
                var result = await _llmClient.GetResponse(
                    messages,
                    ResponseFormat,
                    _toolsContext,
                    null,
                    cancellationToken
                );

                response = result.Result;
                usage = result.TokenUsage;
            }

            // Add assistant response to history
            AddMessage(HazinaMessageRole.Assistant, response);

            // Create result with metadata
            var agentResult = AgentResult.CreateSuccess(response);
            if (usage != null)
            {
                agentResult.Metadata["tokenUsage"] = new
                {
                    inputTokens = usage.InputTokens,
                    outputTokens = usage.OutputTokens,
                    totalTokens = usage.InputTokens + usage.OutputTokens,
                    inputCost = usage.InputCost,
                    outputCost = usage.OutputCost,
                    totalCost = usage.InputCost + usage.OutputCost,
                    model = usage.ModelName
                };
            }

            agentResult.Metadata["historySize"] = _conversationHistory.Count;

            return agentResult;
        }
        catch (Exception ex)
        {
            Context.Log(LogLevel.Error, "Error executing LLM agent: {Error}", ex.Message);
            return AgentResult.CreateFailure($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Execute with structured input including images
    /// </summary>
    public async Task<AgentResult> ExecuteWithImagesAsync(
        string input,
        List<ImageData> images,
        CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;
        Context.CancellationToken = cancellationToken;

        try
        {
            Context.State.Status = AgentStatus.Running;

            // Add user message
            AddMessage(HazinaMessageRole.User, input);

            var messages = new List<HazinaChatMessage>(_conversationHistory);

            var result = await _llmClient.GetResponse(
                messages,
                ResponseFormat,
                _toolsContext,
                images,
                cancellationToken
            );

            var response = result.Result;

            // Add assistant response
            AddMessage(HazinaMessageRole.Assistant, response);

            Context.State.Status = AgentStatus.Completed;

            var duration = DateTime.UtcNow - startTime;
            Context.EmitEvent(new AgentCompletedEvent
            {
                Success = true,
                Result = response,
                Duration = duration
            });

            var agentResult = AgentResult.CreateSuccess(response);
            if (result.TokenUsage != null)
            {
                agentResult.Metadata["tokenUsage"] = new
                {
                    inputTokens = result.TokenUsage.InputTokens,
                    outputTokens = result.TokenUsage.OutputTokens,
                    totalCost = result.TokenUsage.InputCost + result.TokenUsage.OutputCost
                };
            }

            return agentResult;
        }
        catch (Exception ex)
        {
            Context.State.Status = AgentStatus.Error;
            Context.Log(LogLevel.Error, "Error in ExecuteWithImagesAsync: {Error}", ex.Message);
            throw;
        }
    }
}
