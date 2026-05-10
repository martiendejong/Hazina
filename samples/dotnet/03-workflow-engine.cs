// Sample 03 – Multi-Agent Workflow with WorkflowEngine
//
// Shows how to compose multiple agents into a sequential workflow where each
// step's output is injected into the next step's task via context variables.
//
// Prerequisites (NuGet):
//   dotnet add package Hazina.AI.Agents
//   dotnet add package Hazina.AI.Providers

using Hazina.AI.Agents.Core;
using Hazina.AI.Agents.Workflows;
using Hazina.AI.Providers.Core;

// ── 1. Set up agents ──────────────────────────────────────────────────────
// (Assumes orchestrator has been set up per sample 01)
var researchAgent = new Agent("Researcher", "Researches topics using available tools.", orchestrator);
var summaryAgent  = new Agent("Summarizer", "Writes clear executive summaries.", orchestrator);
var reviewAgent   = new Agent("Reviewer",   "Reviews content for accuracy and tone.", orchestrator);

// ── 2. Register agents with the engine ────────────────────────────────────
var engine = new WorkflowEngine(new WorkflowConfig
{
    DefaultMaxIterations = 5,
    StopOnFirstFailure   = true
});

engine.RegisterAgent(researchAgent);
engine.RegisterAgent(summaryAgent);
engine.RegisterAgent(reviewAgent);

// ── 3. Define the workflow ────────────────────────────────────────────────
var workflow = new Workflow
{
    Name        = "ResearchAndPublish",
    Description = "Research a topic, summarize findings, and review before publishing.",
    Steps       =
    [
        // Step 1 – research the topic (output stored in 'raw_research')
        new WorkflowStep
        {
            Name       = "Research",
            Type       = StepType.AgentTask,
            AgentName  = "Researcher",
            Task       = "Research the following topic and list key findings: {topic}",
            OutputKey  = "raw_research",
            ContinueOnFailure = false
        },

        // Step 2 – summarize the research findings
        new WorkflowStep
        {
            Name       = "Summarize",
            Type       = StepType.AgentTask,
            AgentName  = "Summarizer",
            Task       = "Summarize these research findings in 3–5 bullet points: {raw_research}",
            OutputKey  = "summary",
            ContinueOnFailure = false
        },

        // Step 3 – parallel quality checks (tone + fact check run simultaneously)
        new WorkflowStep
        {
            Name  = "QualityChecks",
            Type  = StepType.Parallel,
            ParallelSteps =
            [
                new WorkflowStep
                {
                    Name      = "ToneCheck",
                    Type      = StepType.AgentTask,
                    AgentName = "Reviewer",
                    Task      = "Check the tone of this summary: {summary}",
                    OutputKey = "tone_result"
                },
                new WorkflowStep
                {
                    Name      = "FactCheck",
                    Type      = StepType.AgentTask,
                    AgentName = "Reviewer",
                    Task      = "Verify the accuracy of this summary: {summary}",
                    OutputKey = "fact_result"
                }
            ]
        }
    ]
};

// ── 4. Execute the workflow ───────────────────────────────────────────────
var workflowResult = await engine.ExecuteWorkflowAsync(
    workflow,
    initialContext: new Dictionary<string, object>
    {
        ["topic"] = "The impact of AI agents on software development productivity"
    },
    cancellationToken: CancellationToken.None);

// ── 5. Inspect results ────────────────────────────────────────────────────
Console.WriteLine($"Workflow: {workflowResult.WorkflowName}");
Console.WriteLine($"Success : {workflowResult.Success}");
Console.WriteLine($"Duration: {workflowResult.Duration.TotalSeconds:F1}s");

foreach (var step in workflowResult.StepResults)
{
    var status = step.Success ? "OK" : "FAIL";
    Console.WriteLine($"  [{status}] {step.StepName} – {step.Duration.TotalSeconds:F1}s");
    if (!step.Success)
        Console.WriteLine($"         Error: {step.Error}");
}

if (workflowResult.FinalContext?.TryGetValue("summary", out var summary) == true)
    Console.WriteLine($"\nFinal summary:\n{summary}");
