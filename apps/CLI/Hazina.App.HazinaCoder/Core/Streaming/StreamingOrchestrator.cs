using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.Streaming;

/// <summary>
/// Orchestrates streaming of agent events via IAsyncEnumerable.
/// Supports SSE, WebSocket, and console output.
///
/// Improvement #86: IAsyncEnumerable Streaming API (Value: 10/10, Effort: 6/10, Ratio: 1.67)
/// Improvement #87: Server-Sent Events Support (Value: 9/10, Effort: 5/10, Ratio: 1.80)
/// </summary>
public class StreamingOrchestrator
{
    private readonly Channel<AgentEvent> _eventChannel;
    private readonly List<IStreamingEndpoint> _endpoints;
    private bool _isStreaming;

    public StreamingOrchestrator(int channelCapacity = 1000)
    {
        _eventChannel = Channel.CreateBounded<AgentEvent>(new BoundedChannelOptions(channelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        _endpoints = new List<IStreamingEndpoint>();
        _isStreaming = false;
    }

    /// <summary>
    /// Register streaming endpoint (SSE, WebSocket, Console, etc.)
    /// </summary>
    public void RegisterEndpoint(IStreamingEndpoint endpoint)
    {
        _endpoints.Add(endpoint);
        Console.WriteLine($"📡 Registered streaming endpoint: {endpoint.GetType().Name}");
    }

    /// <summary>
    /// Start streaming (initialize endpoints)
    /// </summary>
    public async Task StartStreamingAsync()
    {
        _isStreaming = true;

        foreach (var endpoint in _endpoints)
        {
            await endpoint.InitializeAsync();
        }

        Console.WriteLine($"✅ Streaming started with {_endpoints.Count} endpoint(s)");
    }

    /// <summary>
    /// Stop streaming
    /// </summary>
    public async Task StopStreamingAsync()
    {
        _isStreaming = false;
        _eventChannel.Writer.Complete();

        foreach (var endpoint in _endpoints)
        {
            await endpoint.DisposeAsync();
        }

        Console.WriteLine("⏹️ Streaming stopped");
    }

    /// <summary>
    /// Emit event to all streaming endpoints
    /// </summary>
    public async Task EmitEventAsync(AgentEvent agentEvent)
    {
        if (!_isStreaming)
            return;

        // Write to channel
        await _eventChannel.Writer.WriteAsync(agentEvent);

        // Send to all endpoints
        foreach (var endpoint in _endpoints)
        {
            try
            {
                await endpoint.SendEventAsync(agentEvent);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to send event to {endpoint.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Get event stream as IAsyncEnumerable (for consumers)
    /// </summary>
    public async IAsyncEnumerable<AgentEvent> GetEventStreamAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var agentEvent in _eventChannel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return agentEvent;
        }
    }

    /// <summary>
    /// Convenience methods for common events
    /// </summary>
    public async Task EmitPlanningStartedAsync(int totalSteps)
    {
        await EmitEventAsync(new PlanningEvent
        {
            EventType = AgentEventType.PlanningStarted,
            Message = $"Planning started - {totalSteps} steps",
            TotalSteps = totalSteps
        });
    }

    public async Task EmitPlanningStepAsync(int stepNumber, int totalSteps, string step)
    {
        await EmitEventAsync(new PlanningEvent
        {
            EventType = AgentEventType.PlanningStep,
            Message = $"Step {stepNumber}/{totalSteps}: {step}",
            StepNumber = stepNumber,
            TotalSteps = totalSteps,
            Step = step
        });
    }

    public async Task EmitActionStartedAsync(string actionType)
    {
        await EmitEventAsync(new ActionEvent
        {
            EventType = AgentEventType.ActionStarted,
            Message = $"Action started: {actionType}",
            ActionType = actionType,
            ProgressPercentage = 0
        });
    }

    public async Task EmitActionProgressAsync(string actionType, double progress, string message)
    {
        await EmitEventAsync(new ActionEvent
        {
            EventType = AgentEventType.ActionProgress,
            Message = message,
            ActionType = actionType,
            ProgressPercentage = progress
        });
    }

    public async Task EmitActionCompletedAsync(string actionType)
    {
        await EmitEventAsync(new ActionEvent
        {
            EventType = AgentEventType.ActionCompleted,
            Message = $"Action completed: {actionType}",
            ActionType = actionType,
            ProgressPercentage = 100
        });
    }

    public async Task EmitToolExecutionAsync(string toolName, string? parameters = null)
    {
        await EmitEventAsync(new ToolExecutionEvent
        {
            EventType = AgentEventType.ToolExecutionStarted,
            Message = $"Executing tool: {toolName}",
            ToolName = toolName,
            ToolParameters = parameters
        });
    }

    public async Task EmitBuildProgressAsync(string projectPath, string outputLine, int errors, int warnings)
    {
        await EmitEventAsync(new BuildEvent
        {
            EventType = AgentEventType.BuildProgress,
            Message = outputLine,
            ProjectPath = projectPath,
            OutputLine = outputLine,
            ErrorCount = errors,
            WarningCount = warnings
        });
    }

    public async Task EmitTestProgressAsync(string testName, int total, int passed, int failed)
    {
        await EmitEventAsync(new TestEvent
        {
            EventType = AgentEventType.TestProgress,
            Message = $"Test: {testName} - {passed}/{total} passed",
            TestName = testName,
            TotalTests = total,
            PassedTests = passed,
            FailedTests = failed
        });
    }

    public async Task EmitTokenUsageAsync(int inputTokens, int outputTokens, decimal cost, string model)
    {
        await EmitEventAsync(new TokenUsageEvent
        {
            EventType = AgentEventType.TokenUsageUpdate,
            Message = $"Token usage: {inputTokens + outputTokens} tokens (${cost:F4})",
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = inputTokens + outputTokens,
            CostUSD = cost,
            ModelName = model
        });
    }

    public async Task EmitProgressAsync(string taskName, double percentage, string currentStep, TimeSpan? eta = null)
    {
        await EmitEventAsync(new ProgressEvent
        {
            EventType = AgentEventType.ActionProgress,
            Message = $"{taskName}: {percentage:F1}% - {currentStep}",
            TaskName = taskName,
            ProgressPercentage = percentage,
            CurrentStep = currentStep,
            EstimatedTimeRemaining = eta
        });
    }

    public async Task EmitFileChangeAsync(string filePath, FileChangeType changeType)
    {
        await EmitEventAsync(new FileChangeEvent
        {
            EventType = AgentEventType.FileChanged,
            Message = $"File {changeType}: {filePath}",
            FilePath = filePath,
            ChangeType = changeType
        });
    }

    public async Task EmitSummaryAsync(
        string summary,
        int totalActions,
        int successful,
        int failed,
        TimeSpan duration,
        decimal cost)
    {
        await EmitEventAsync(new SummaryEvent
        {
            EventType = AgentEventType.SummaryGenerated,
            Message = summary,
            Summary = summary,
            TotalActions = totalActions,
            SuccessfulActions = successful,
            FailedActions = failed,
            TotalDuration = duration,
            TotalCostUSD = cost
        });
    }
}

/// <summary>
/// Interface for streaming endpoints (SSE, WebSocket, Console, etc.)
/// </summary>
public interface IStreamingEndpoint : IAsyncDisposable
{
    Task InitializeAsync();
    Task SendEventAsync(AgentEvent agentEvent);
}
