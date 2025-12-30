# Agent Planning and Tracing

Deep agent primitives for long-running, reliable agent workflows.

## AgentPlan

Structured planning for complex agent tasks:

```csharp
var plan = new AgentPlan
{
    PlanId = "task-123",
    Goal = "Analyze quarterly sales data and generate report",
    Steps = new List<PlanStep>
    {
        new PlanStep
        {
            StepId = "step-1",
            Description = "Retrieve Q1-Q4 sales data from database",
            Status = StepStatus.Pending
        },
        new PlanStep
        {
            StepId = "step-2",
            Description = "Calculate year-over-year growth metrics",
            Status = StepStatus.Pending,
            Dependencies = new List<string> { "step-1" }
        },
        new PlanStep
        {
            StepId = "step-3",
            Description = "Generate visualizations and summary report",
            Status = StepStatus.Pending,
            Dependencies = new List<string> { "step-2" }
        }
    }
};

// Execute plan
var nextStep = plan.GetNextStep();
plan.ActivateStep(nextStep.StepId);
// ... do work ...
plan.CompleteStep(nextStep.StepId, result: "Retrieved 12,543 records");

// Track progress
Console.WriteLine($"Progress: {plan.GetProgress():P0}");
```

## AgentTrace

Comprehensive execution traces for debugging and auditing:

```csharp
var trace = new AgentTrace
{
    TraceId = "trace-abc123",
    AgentName = "SalesAnalyst",
    Task = "Generate quarterly report"
};

// Record tool calls
trace.AddToolCall("DatabaseQuery",
    new Dictionary<string, object> { ["query"] = "SELECT * FROM sales" },
    result: "12543 rows");

// Record decisions
trace.AddDecision(
    decision: "Use median instead of mean for outlier data",
    reasoning: "Dataset contains extreme outliers that skew the mean",
    context: new Dictionary<string, object> { ["outlierCount"] = 47 }
);

// Record retrievals
trace.AddRetrieval(
    query: "previous quarter sales reports",
    retrievedChunkIds: new List<string> { "report-2024-q3", "report-2024-q2" },
    rerankerUsed: "LlmJudgeReranker"
);

// Export to Markdown for review
var markdown = TraceSerialization.ExportTraceToMarkdown(trace);
```

## AgentWorkspace

Deterministic file storage for agent runs:

```csharp
var factory = new AgentWorkspaceFactory("/data/agent-workspaces");
var workspace = factory.CreateWorkspace(prefix: "report");

// Write files
await workspace.WriteFileAsync("data.csv", csvContent);
await workspace.WriteFileAsync("output/report.pdf", pdfBytes);

// Save trace and plan
await TraceSerialization.SaveTraceAsync(trace, workspace.GetTracePath(trace.TraceId));
await TraceSerialization.SavePlanAsync(plan, workspace.GetPlanPath(plan.PlanId));

// List all files
var files = workspace.ListFiles();

// Cleanup old workspaces
factory.CleanupOldWorkspaces(olderThanDays: 30);
```

## Benefits

1. **Explicit reasoning** - Plans make agent logic visible and inspectable
2. **Debuggability** - Traces record every action for post-mortem analysis
3. **Resumability** - Serialized plans enable resuming interrupted tasks
4. **Auditing** - Complete trace of all decisions and tool uses
5. **Isolation** - Deterministic workspaces prevent file conflicts

## Integration with Existing Agent

```csharp
public class TrackedAgent : Agent
{
    private readonly AgentTrace _trace;
    private readonly AgentWorkspace _workspace;

    protected override async Task<string> ExecuteToolAsync(ToolCall toolCall, CancellationToken ct)
    {
        var result = await base.ExecuteToolAsync(toolCall, ct);

        // Add to trace
        _trace.AddToolCall(toolCall.ToolName, toolCall.Arguments, result);

        return result;
    }
}
```
