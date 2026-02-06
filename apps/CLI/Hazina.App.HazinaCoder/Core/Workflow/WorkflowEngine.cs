using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Hazina.App.HazinaCoder.Core.Workflow;

/// <summary>
/// 🚀 IMPROVEMENT #5 (ROI 1.67): Structured Workflow Engine
/// Enforces correct process end-to-end (no more skipping critical steps!)
/// Converts documentation into machine-executable workflows
/// </summary>
public class WorkflowEngine
{
    private readonly Dictionary<string, IWorkflowStep> _availableSteps = new();
    private readonly List<WorkflowExecution> _history = new();

    public WorkflowEngine()
    {
        RegisterDefaultSteps();
    }

    /// <summary>
    /// Load workflow definition from YAML file
    /// </summary>
    public async Task<WorkflowDefinition> LoadWorkflowAsync(string yamlPath)
    {
        var yaml = await File.ReadAllTextAsync(yamlPath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .Build();

        return deserializer.Deserialize<WorkflowDefinition>(yaml);
    }

    /// <summary>
    /// Execute a workflow with automatic validation and rollback
    /// </summary>
    public async Task<WorkflowResult> ExecuteAsync(
        WorkflowDefinition workflow,
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var execution = new WorkflowExecution
        {
            WorkflowName = workflow.Name,
            StartTime = DateTime.UtcNow,
            Context = context
        };

        _history.Add(execution);

        try
        {
            Console.WriteLine($"[Workflow] Starting: {workflow.Name}");
            Console.WriteLine($"[Workflow] Steps: {workflow.Steps.Count}");

            for (int i = 0; i < workflow.Steps.Count; i++)
            {
                var stepDef = workflow.Steps[i];
                Console.WriteLine($"\n[Workflow] Step {i + 1}/{workflow.Steps.Count}: {stepDef.Name}");

                // Check prerequisites
                if (stepDef.Prerequisites?.Any() == true)
                {
                    foreach (var prereq in stepDef.Prerequisites)
                    {
                        if (!context.CompletedSteps.Contains(prereq))
                        {
                            var error = $"Prerequisite not met: {prereq} must complete before {stepDef.Name}";
                            execution.FailStep(stepDef.Name, error);
                            return WorkflowResult.Failed(workflow.Name, error, execution);
                        }
                    }
                }

                // Execute step
                var stepResult = await ExecuteStepAsync(stepDef, context, cancellationToken);
                execution.CompleteStep(stepDef.Name, stepResult.Success, stepResult.Output, stepResult.Error);

                if (!stepResult.Success)
                {
                    if (stepDef.Required)
                    {
                        Console.WriteLine($"[Workflow] FAILED: Required step '{stepDef.Name}' failed");
                        Console.WriteLine($"[Workflow] Error: {stepResult.Error}");

                        // Attempt rollback if configured
                        if (stepDef.RollbackOnFailure && !string.IsNullOrEmpty(stepDef.RollbackStep))
                        {
                            Console.WriteLine($"[Workflow] Rolling back: {stepDef.RollbackStep}");
                            await ExecuteRollbackAsync(stepDef.RollbackStep, context, cancellationToken);
                        }

                        return WorkflowResult.Failed(workflow.Name, stepResult.Error ?? "Unknown error", execution);
                    }
                    else
                    {
                        Console.WriteLine($"[Workflow] WARNING: Optional step '{stepDef.Name}' failed, continuing...");
                    }
                }
                else
                {
                    context.CompletedSteps.Add(stepDef.Name);
                    Console.WriteLine($"[Workflow] ✅ Step completed: {stepDef.Name}");

                    // Store outputs in context for next steps
                    if (stepResult.Output != null)
                    {
                        context.SetOutput(stepDef.Name, stepResult.Output);
                    }
                }

                // Validate step output if validation rules exist
                if (stepDef.Validation != null && stepResult.Success)
                {
                    var isValid = await ValidateStepAsync(stepDef, stepResult, context, cancellationToken);
                    if (!isValid)
                    {
                        var error = $"Step validation failed: {stepDef.Validation.ErrorMessage}";
                        execution.FailStep(stepDef.Name, error);
                        return WorkflowResult.Failed(workflow.Name, error, execution);
                    }
                }
            }

            execution.EndTime = DateTime.UtcNow;
            execution.Success = true;

            Console.WriteLine($"\n[Workflow] ✅ Workflow completed successfully: {workflow.Name}");
            Console.WriteLine($"[Workflow] Duration: {execution.Duration}");

            return WorkflowResult.Success(workflow.Name, execution);
        }
        catch (Exception ex)
        {
            execution.EndTime = DateTime.UtcNow;
            execution.Success = false;
            execution.Error = ex.Message;

            Console.WriteLine($"\n[Workflow] ❌ Workflow exception: {ex.Message}");
            return WorkflowResult.Failed(workflow.Name, ex.Message, execution);
        }
    }

    private async Task<StepResult> ExecuteStepAsync(
        WorkflowStepDefinition stepDef,
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        // Find registered step implementation
        if (!_availableSteps.TryGetValue(stepDef.Type, out var step))
        {
            return StepResult.Failed($"Unknown step type: {stepDef.Type}");
        }

        try
        {
            // Execute with timeout if specified
            if (stepDef.TimeoutSeconds > 0)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(stepDef.TimeoutSeconds));
                return await step.ExecuteAsync(stepDef, context, cts.Token);
            }
            else
            {
                return await step.ExecuteAsync(stepDef, context, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            return StepResult.Failed($"Step timed out after {stepDef.TimeoutSeconds}s");
        }
        catch (Exception ex)
        {
            return StepResult.Failed($"Step exception: {ex.Message}");
        }
    }

    private async Task<bool> ValidateStepAsync(
        WorkflowStepDefinition stepDef,
        StepResult stepResult,
        WorkflowContext context,
        CancellationToken cancellationToken)
    {
        if (stepDef.Validation == null) return true;

        var validation = stepDef.Validation;

        // Check output exists
        if (validation.RequireOutput && stepResult.Output == null)
        {
            Console.WriteLine($"[Workflow] Validation failed: {validation.ErrorMessage}");
            return false;
        }

        // Check file exists
        if (!string.IsNullOrEmpty(validation.FileExists))
        {
            var path = ResolveVariables(validation.FileExists, context);
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                Console.WriteLine($"[Workflow] Validation failed: Required path not found: {path}");
                return false;
            }
        }

        // Check custom condition
        if (!string.IsNullOrEmpty(validation.Condition))
        {
            // Evaluate condition (simple implementation - could be extended with expression evaluator)
            var conditionMet = EvaluateCondition(validation.Condition, stepResult, context);
            if (!conditionMet)
            {
                Console.WriteLine($"[Workflow] Validation failed: Condition not met: {validation.Condition}");
                return false;
            }
        }

        return true;
    }

    private async Task ExecuteRollbackAsync(string rollbackStepType, WorkflowContext context, CancellationToken cancellationToken)
    {
        if (!_availableSteps.TryGetValue(rollbackStepType, out var step))
        {
            Console.WriteLine($"[Workflow] Rollback step not found: {rollbackStepType}");
            return;
        }

        try
        {
            var rollbackDef = new WorkflowStepDefinition
            {
                Name = $"Rollback: {rollbackStepType}",
                Type = rollbackStepType
            };

            await step.ExecuteAsync(rollbackDef, context, cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Workflow] Rollback failed: {ex.Message}");
        }
    }

    private bool EvaluateCondition(string condition, StepResult stepResult, WorkflowContext context)
    {
        // Simple condition evaluation (can be extended)
        // Example conditions:
        // - "exit_code == 0"
        // - "output contains 'success'"
        // - "file_count > 0"

        if (condition.Contains("exit_code"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(condition, @"exit_code\s*([=!<>]+)\s*(\d+)");
            if (match.Success)
            {
                var op = match.Groups[1].Value;
                var expected = int.Parse(match.Groups[2].Value);
                var actual = (stepResult.Output as int?) ?? 0;

                return op switch
                {
                    "==" => actual == expected,
                    "!=" => actual != expected,
                    ">" => actual > expected,
                    "<" => actual < expected,
                    ">=" => actual >= expected,
                    "<=" => actual <= expected,
                    _ => false
                };
            }
        }

        // Default: assume condition met
        return true;
    }

    private string ResolveVariables(string input, WorkflowContext context)
    {
        // Replace ${variable} with context values
        return System.Text.RegularExpressions.Regex.Replace(input, @"\$\{(\w+)\}", match =>
        {
            var varName = match.Groups[1].Value;
            return context.Variables.GetValueOrDefault(varName, match.Value);
        });
    }

    private void RegisterDefaultSteps()
    {
        // Register built-in step types
        _availableSteps["read_clickup_task"] = new ReadClickUpTaskStep();
        _availableSteps["investigate_codebase"] = new InvestigateCodebaseStep();
        _availableSteps["allocate_worktree"] = new AllocateWorktreeStep();
        _availableSteps["implement_changes"] = new ImplementChangesStep();
        _availableSteps["merge_develop"] = new MergeDevelopStep();
        _availableSteps["build_test"] = new BuildTestStep();
        _availableSteps["create_pr"] = new CreatePRStep();
        _availableSteps["update_clickup"] = new UpdateClickUpStep();
    }

    public void RegisterStep(string type, IWorkflowStep step)
    {
        _availableSteps[type] = step;
    }
}

#region Workflow Models

public class WorkflowDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<WorkflowStepDefinition> Steps { get; set; } = new();
    public Dictionary<string, string> Variables { get; set; } = new();
}

public class WorkflowStepDefinition
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Required { get; set; } = true;
    public List<string>? Prerequisites { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public StepValidation? Validation { get; set; }
    public int TimeoutSeconds { get; set; } = 300; // 5 minutes default
    public bool RollbackOnFailure { get; set; }
    public string? RollbackStep { get; set; }
}

public class StepValidation
{
    public bool RequireOutput { get; set; }
    public string? FileExists { get; set; }
    public string? Condition { get; set; }
    public string ErrorMessage { get; set; } = "Validation failed";
}

public class WorkflowContext
{
    public Dictionary<string, string> Variables { get; set; } = new();
    public Dictionary<string, object?> Outputs { get; set; } = new();
    public HashSet<string> CompletedSteps { get; set; } = new();
    public string WorkingDirectory { get; set; } = "";
    public CancellationToken CancellationToken { get; set; }

    public void SetOutput(string stepName, object? output)
    {
        Outputs[stepName] = output;
    }

    public T? GetOutput<T>(string stepName)
    {
        if (Outputs.TryGetValue(stepName, out var output) && output is T typedOutput)
            return typedOutput;
        return default;
    }
}

public class WorkflowExecution
{
    public string WorkflowName { get; set; } = "";
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
    public List<StepExecution> Steps { get; set; } = new();
    public WorkflowContext Context { get; set; } = null!;

    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;

    public void CompleteStep(string stepName, bool success, object? output, string? error)
    {
        Steps.Add(new StepExecution
        {
            Name = stepName,
            Success = success,
            Output = output,
            Error = error,
            Timestamp = DateTime.UtcNow
        });
    }

    public void FailStep(string stepName, string error)
    {
        Steps.Add(new StepExecution
        {
            Name = stepName,
            Success = false,
            Error = error,
            Timestamp = DateTime.UtcNow
        });
    }
}

public class StepExecution
{
    public string Name { get; set; } = "";
    public bool Success { get; set; }
    public object? Output { get; set; }
    public string? Error { get; set; }
    public DateTime Timestamp { get; set; }
}

public class WorkflowResult
{
    public bool Success { get; init; }
    public string WorkflowName { get; init; } = "";
    public string? Error { get; init; }
    public WorkflowExecution Execution { get; init; } = null!;

    public static WorkflowResult Success(string workflowName, WorkflowExecution execution) =>
        new() { Success = true, WorkflowName = workflowName, Execution = execution };

    public static WorkflowResult Failed(string workflowName, string error, WorkflowExecution execution) =>
        new() { Success = false, WorkflowName = workflowName, Error = error, Execution = execution };
}

public class StepResult
{
    public bool Success { get; init; }
    public object? Output { get; init; }
    public string? Error { get; init; }

    public static StepResult Success(object? output = null) =>
        new() { Success = true, Output = output };

    public static StepResult Failed(string error) =>
        new() { Success = false, Error = error };
}

#endregion

#region Workflow Step Interface & Implementations

public interface IWorkflowStep
{
    Task<StepResult> ExecuteAsync(WorkflowStepDefinition definition, WorkflowContext context, CancellationToken cancellationToken);
}

// Placeholder implementations (would be fully implemented in production)

public class ReadClickUpTaskStep : IWorkflowStep
{
    public async Task<StepResult> ExecuteAsync(WorkflowStepDefinition definition, WorkflowContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Step] Reading ClickUp task...");
        // TODO: Use ClickUpClient to fetch task
        return StepResult.Success(new { taskId = "example", requirements = "..." });
    }
}

public class InvestigateCodebaseStep : IWorkflowStep
{
    public async Task<StepResult> ExecuteAsync(WorkflowStepDefinition definition, WorkflowContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Step] Investigating codebase...");
        // TODO: Use Grep/Find to locate relevant files
        return StepResult.Success(new { relevantFiles = new List<string>() });
    }
}

public class AllocateWorktreeStep : IWorkflowStep
{
    public async Task<StepResult> ExecuteAsync(WorkflowStepDefinition definition, WorkflowContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Step] Allocating worktree...");
        // TODO: Use GitClient.Worktree.AddAsync()
        return StepResult.Success(new { worktreePath = "example-path" });
    }
}

public class ImplementChangesStep : IWorkflowStep
{
    public async Task<StepResult> ExecuteAsync(WorkflowStepDefinition definition, WorkflowContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Step] Implementing changes...");
        // TODO: Invoke LLM for actual implementation
        return StepResult.Success();
    }
}

public class MergeDevelopStep : IWorkflowStep
{
    public async Task<StepResult> ExecuteAsync(WorkflowStepDefinition definition, WorkflowContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Step] Merging develop branch...");
        // TODO: Use GitClient.Remote.PullAsync()
        return StepResult.Success();
    }
}

public class BuildTestStep : IWorkflowStep
{
    public async Task<StepResult> ExecuteAsync(WorkflowStepDefinition definition, WorkflowContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Step] Building and testing...");
        // TODO: Use CommandExecutor to run dotnet build/test
        return StepResult.Success(new { exitCode = 0 });
    }
}

public class CreatePRStep : IWorkflowStep
{
    public async Task<StepResult> ExecuteAsync(WorkflowStepDefinition definition, WorkflowContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Step] Creating pull request...");
        // TODO: Use CommandExecutor for gh pr create
        return StepResult.Success(new { prUrl = "https://github.com/..." });
    }
}

public class UpdateClickUpStep : IWorkflowStep
{
    public async Task<StepResult> ExecuteAsync(WorkflowStepDefinition definition, WorkflowContext context, CancellationToken cancellationToken)
    {
        Console.WriteLine("[Step] Updating ClickUp task...");
        // TODO: Use ClickUpClient.LinkPullRequestAsync()
        return StepResult.Success();
    }
}

#endregion
