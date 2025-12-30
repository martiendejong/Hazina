namespace Hazina.AI.Agents.Planning;

/// <summary>
/// Structured plan for a long-running agent task with step tracking.
/// Makes agent reasoning and progress explicit and inspectable.
/// </summary>
public class AgentPlan
{
    /// <summary>
    /// Unique identifier for this plan
    /// </summary>
    public required string PlanId { get; init; }

    /// <summary>
    /// High-level goal or task description
    /// </summary>
    public required string Goal { get; init; }

    /// <summary>
    /// Ordered list of steps to achieve the goal
    /// </summary>
    public List<PlanStep> Steps { get; init; } = new();

    /// <summary>
    /// When the plan was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// When the plan was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Overall plan status
    /// </summary>
    public PlanStatus Status { get; set; } = PlanStatus.Pending;

    /// <summary>
    /// Optional metadata
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Get all pending steps
    /// </summary>
    public List<PlanStep> GetPendingSteps() =>
        Steps.Where(s => s.Status == StepStatus.Pending).ToList();

    /// <summary>
    /// Get current active step
    /// </summary>
    public PlanStep? GetActiveStep() =>
        Steps.FirstOrDefault(s => s.Status == StepStatus.Active);

    /// <summary>
    /// Get next step to execute
    /// </summary>
    public PlanStep? GetNextStep() =>
        Steps.FirstOrDefault(s => s.Status == StepStatus.Pending);

    /// <summary>
    /// Mark a step as active
    /// </summary>
    public void ActivateStep(string stepId)
    {
        var step = Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step != null)
        {
            step.Status = StepStatus.Active;
            step.StartedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Mark a step as completed
    /// </summary>
    public void CompleteStep(string stepId, string? result = null)
    {
        var step = Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step != null)
        {
            step.Status = StepStatus.Done;
            step.CompletedAt = DateTime.UtcNow;
            step.Result = result;
            UpdatedAt = DateTime.UtcNow;

            if (Steps.All(s => s.Status == StepStatus.Done))
            {
                Status = PlanStatus.Completed;
            }
        }
    }

    /// <summary>
    /// Mark a step as failed
    /// </summary>
    public void FailStep(string stepId, string error)
    {
        var step = Steps.FirstOrDefault(s => s.StepId == stepId);
        if (step != null)
        {
            step.Status = StepStatus.Failed;
            step.CompletedAt = DateTime.UtcNow;
            step.Error = error;
            UpdatedAt = DateTime.UtcNow;
            Status = PlanStatus.Failed;
        }
    }

    /// <summary>
    /// Calculate plan progress (fraction of steps done)
    /// </summary>
    public double GetProgress()
    {
        if (Steps.Count == 0) return 0.0;
        var doneCount = Steps.Count(s => s.Status == StepStatus.Done);
        return (double)doneCount / Steps.Count;
    }
}

/// <summary>
/// Individual step in an agent plan
/// </summary>
public class PlanStep
{
    /// <summary>
    /// Unique identifier for this step
    /// </summary>
    public required string StepId { get; init; }

    /// <summary>
    /// Description of what this step should accomplish
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Current status of the step
    /// </summary>
    public StepStatus Status { get; set; } = StepStatus.Pending;

    /// <summary>
    /// When step execution started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When step execution completed (success or failure)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Result or output from this step (if successful)
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Error message (if failed)
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Dependencies - step IDs that must complete before this step
    /// </summary>
    public List<string> Dependencies { get; init; } = new();

    /// <summary>
    /// Optional metadata for the step
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }
}

/// <summary>
/// Step execution status
/// </summary>
public enum StepStatus
{
    Pending,
    Active,
    Done,
    Failed,
    Skipped
}

/// <summary>
/// Overall plan status
/// </summary>
public enum PlanStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled
}
