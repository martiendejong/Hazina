using Hazina.AI.Guardrails;
using Hazina.AI.Providers.Core;
using Hazina.AI.RAG.Core;
using Hazina.AI.Workflows.Configuration;
using Hazina.LLMs;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.Workflows.Engine;

/// <summary>
/// Enhanced workflow engine with per-step configuration support
/// </summary>
public class EnhancedWorkflowEngine
{
    private readonly IProviderOrchestrator _llmOrchestrator;
    private readonly Dictionary<string, RAGEngine> _ragEngines;
    private readonly IGuardrailPipeline _guardrailPipeline;
    private readonly ILogger<EnhancedWorkflowEngine> _logger;

    // Events for real-time monitoring
    public event EventHandler<StepStartedEventArgs>? StepStarted;
    public event EventHandler<StepCompletedEventArgs>? StepCompleted;
    public event EventHandler<StepFailedEventArgs>? StepFailed;
    public event EventHandler<WorkflowCompletedEventArgs>? WorkflowCompleted;

    public EnhancedWorkflowEngine(
        IProviderOrchestrator llmOrchestrator,
        Dictionary<string, RAGEngine> ragEngines,
        IGuardrailPipeline guardrailPipeline,
        ILogger<EnhancedWorkflowEngine> logger)
    {
        _llmOrchestrator = llmOrchestrator ?? throw new ArgumentNullException(nameof(llmOrchestrator));
        _ragEngines = ragEngines ?? throw new ArgumentNullException(nameof(ragEngines));
        _guardrailPipeline = guardrailPipeline ?? throw new ArgumentNullException(nameof(guardrailPipeline));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute workflow from .hazina file
    /// </summary>
    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        string workflowPath,
        Dictionary<string, object> initialContext,
        CancellationToken cancellationToken = default)
    {
        // Load workflow configuration
        var config = HazinaWorkflowConfigParser.LoadFromFile(workflowPath);

        return await ExecuteWorkflowAsync(config, initialContext, cancellationToken);
    }

    /// <summary>
    /// Execute workflow from configuration
    /// </summary>
    public async Task<WorkflowExecutionResult> ExecuteWorkflowAsync(
        WorkflowConfig config,
        Dictionary<string, object> initialContext,
        CancellationToken cancellationToken = default)
    {
        var result = new WorkflowExecutionResult
        {
            WorkflowName = config.Name,
            StartTime = DateTime.UtcNow
        };

        var context = new WorkflowExecutionContext(initialContext);

        try
        {
            foreach (var step in config.Steps)
            {
                var stepResult = await ExecuteStepAsync(step, context, cancellationToken);
                result.StepResults.Add(stepResult);

                if (!stepResult.Success && !step.ContinueOnFailure)
                {
                    result.Success = false;
                    result.Error = $"Step '{step.Name}' failed: {stepResult.Error}";
                    break;
                }

                // Update context with step output
                if (stepResult.Success && !string.IsNullOrEmpty(step.OutputKey))
                {
                    context.SetValue(step.OutputKey, stepResult.Output);
                }
            }

            result.Success = result.StepResults.All(r => r.Success);
            result.FinalContext = context.GetAllValues();
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "Workflow execution failed: {WorkflowName}", config.Name);
        }

        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;

        // Raise completion event
        WorkflowCompleted?.Invoke(this, new WorkflowCompletedEventArgs(result));

        return result;
    }

    /// <summary>
    /// Execute a single workflow step with per-step configuration
    /// </summary>
    private async Task<StepExecutionResult> ExecuteStepAsync(
        WorkflowStepConfig step,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken)
    {
        var stepResult = new StepExecutionResult
        {
            StepName = step.Name,
            StartTime = DateTime.UtcNow
        };

        // Raise step started event
        StepStarted?.Invoke(this, new StepStartedEventArgs(step.Name));

        try
        {
            // Process input template with context variables
            var processedInput = context.ProcessTemplate(step.Input);

            // Execute RAG search if configured
            string? ragContext = null;
            if (step.RAGConfig != null)
            {
                ragContext = await ExecuteRAGSearchAsync(processedInput, step.RAGConfig, cancellationToken);
                stepResult.RAGResultsCount = ragContext?.Split('\n').Length ?? 0;
            }

            // Build final prompt
            var finalPrompt = ragContext != null
                ? $"Context:\n{ragContext}\n\nQuery: {processedInput}"
                : processedInput;

            // Execute guardrails (pre-execution)
            if (step.Guardrails.Any())
            {
                var preGuardrailResult = await _guardrailPipeline.ExecuteAsync(
                    finalPrompt,
                    step.Guardrails,
                    GuardrailStage.PreExecution,
                    cancellationToken);

                if (!preGuardrailResult.Passed)
                {
                    stepResult.Success = false;
                    stepResult.Error = $"Pre-execution guardrail failed: {preGuardrailResult.FailureReason}";
                    StepFailed?.Invoke(this, new StepFailedEventArgs(step.Name, stepResult.Error));
                    return stepResult;
                }
            }

            // Execute LLM with step-specific configuration
            var llmResponse = await ExecuteLLMCallAsync(
                finalPrompt,
                step.LLMConfig ?? new LLMStepConfig(),
                cancellationToken);

            stepResult.Output = llmResponse;
            stepResult.TokensUsed = EstimateTokens(finalPrompt) + EstimateTokens(llmResponse);
            stepResult.EstimatedCost = EstimateCost(step.LLMConfig?.Model ?? "gpt-3.5-turbo", stepResult.TokensUsed);

            // Execute guardrails (post-execution)
            if (step.Guardrails.Any())
            {
                var postGuardrailResult = await _guardrailPipeline.ExecuteAsync(
                    llmResponse,
                    step.Guardrails,
                    GuardrailStage.PostExecution,
                    cancellationToken);

                if (!postGuardrailResult.Passed)
                {
                    stepResult.Success = false;
                    stepResult.Error = $"Post-execution guardrail failed: {postGuardrailResult.FailureReason}";
                    StepFailed?.Invoke(this, new StepFailedEventArgs(step.Name, stepResult.Error));
                    return stepResult;
                }
            }

            stepResult.Success = true;
            StepCompleted?.Invoke(this, new StepCompletedEventArgs(step.Name, stepResult));
        }
        catch (Exception ex)
        {
            stepResult.Success = false;
            stepResult.Error = ex.Message;
            _logger.LogError(ex, "Step execution failed: {StepName}", step.Name);
            StepFailed?.Invoke(this, new StepFailedEventArgs(step.Name, ex.Message));
        }

        stepResult.EndTime = DateTime.UtcNow;
        stepResult.Duration = stepResult.EndTime - stepResult.StartTime;
        return stepResult;
    }

    /// <summary>
    /// Execute RAG search with step-specific configuration
    /// </summary>
    private async Task<string?> ExecuteRAGSearchAsync(
        string query,
        RAGStepConfig ragConfig,
        CancellationToken cancellationToken)
    {
        if (!_ragEngines.TryGetValue(ragConfig.StoreName, out var ragEngine))
        {
            _logger.LogWarning("RAG store not found: {StoreName}", ragConfig.StoreName);
            return null;
        }

        var ragOptions = new RAGQueryOptions
        {
            TopK = ragConfig.TopK,
            MinSimilarity = ragConfig.MinSimilarity,
            UseEmbeddings = ragConfig.UseEmbeddings,
            MaxContextLength = ragConfig.MaxContextLength
        };

        // TODO: Add metadata filter parsing when needed

        var ragResponse = await ragEngine.QueryAsync(query, ragOptions, cancellationToken);
        return ragResponse.ContextUsed;
    }

    /// <summary>
    /// Execute LLM call with step-specific configuration
    /// </summary>
    private async Task<string> ExecuteLLMCallAsync(
        string prompt,
        LLMStepConfig llmConfig,
        CancellationToken cancellationToken)
    {
        // NOTE: Current IProviderOrchestrator interface doesn't support per-call configuration
        // For now, we'll use default settings. In future, we'll enhance the interface.
        // TODO (Phase 2): Add overload to IProviderOrchestrator.GetResponse() that accepts temperature, maxTokens, etc.

        var messages = new List<HazinaChatMessage>
        {
            new HazinaChatMessage
            {
                Role = HazinaMessageRole.User,
                Text = prompt
            }
        };

        var response = await _llmOrchestrator.GetResponse(
            messages,
            HazinaChatResponseFormat.Text,
            null,
            null,
            cancellationToken
        );

        return response.Result;
    }

    // Helper methods
    private int EstimateTokens(string text)
    {
        // Rough estimate: ~4 characters per token
        return text.Length / 4;
    }

    private decimal EstimateCost(string model, int tokens)
    {
        // Rough cost estimates (per 1K tokens)
        var costPer1KTokens = model.ToLowerInvariant() switch
        {
            "gpt-4" => 0.03m,
            "gpt-4-turbo" => 0.01m,
            "gpt-3.5-turbo" => 0.0015m,
            _ => 0.001m
        };

        return (tokens / 1000.0m) * costPer1KTokens;
    }
}

/// <summary>
/// Workflow execution context with variable substitution
/// </summary>
public class WorkflowExecutionContext
{
    private readonly Dictionary<string, object> _values;

    public WorkflowExecutionContext(Dictionary<string, object> initialValues)
    {
        _values = new Dictionary<string, object>(initialValues);
    }

    public void SetValue(string key, object value)
    {
        _values[key] = value;
    }

    public object? GetValue(string key)
    {
        return _values.TryGetValue(key, out var value) ? value : null;
    }

    public Dictionary<string, object> GetAllValues()
    {
        return new Dictionary<string, object>(_values);
    }

    /// <summary>
    /// Process template string by replacing {variableName} placeholders
    /// </summary>
    public string ProcessTemplate(string template)
    {
        var result = template;
        foreach (var kvp in _values)
        {
            result = result.Replace($"{{{kvp.Key}}}", kvp.Value?.ToString() ?? "");
        }
        return result;
    }
}

/// <summary>
/// Comprehensive workflow execution result
/// </summary>
public class WorkflowExecutionResult
{
    public string WorkflowName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<StepExecutionResult> StepResults { get; set; } = new();
    public Dictionary<string, object> FinalContext { get; set; } = new();

    public int TotalTokensUsed => StepResults.Sum(r => r.TokensUsed);
    public decimal TotalEstimatedCost => StepResults.Sum(r => r.EstimatedCost);
}

/// <summary>
/// Step execution result with detailed metrics
/// </summary>
public class StepExecutionResult
{
    public string StepName { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int TokensUsed { get; set; }
    public decimal EstimatedCost { get; set; }
    public int RAGResultsCount { get; set; }
}

// Event argument classes
public class StepStartedEventArgs : EventArgs
{
    public string StepName { get; }
    public StepStartedEventArgs(string stepName) { StepName = stepName; }
}

public class StepCompletedEventArgs : EventArgs
{
    public string StepName { get; }
    public StepExecutionResult Result { get; }
    public StepCompletedEventArgs(string stepName, StepExecutionResult result)
    {
        StepName = stepName;
        Result = result;
    }
}

public class StepFailedEventArgs : EventArgs
{
    public string StepName { get; }
    public string Error { get; }
    public StepFailedEventArgs(string stepName, string error)
    {
        StepName = stepName;
        Error = error;
    }
}

public class WorkflowCompletedEventArgs : EventArgs
{
    public WorkflowExecutionResult Result { get; }
    public WorkflowCompletedEventArgs(WorkflowExecutionResult result)
    {
        Result = result;
    }
}
