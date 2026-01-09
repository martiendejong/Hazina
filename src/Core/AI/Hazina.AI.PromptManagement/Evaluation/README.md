# Hazina Evaluation Pipeline

**Automated evaluation system with LLM-as-judge, scheduling, and regression detection**

Part of Sprint 2 (Week 2) - Option B: Controlled Self-Learning

## Overview

The Evaluation Pipeline provides a complete system for automated prompt evaluation, including:
- **Quality Rubrics**: LLM-as-judge evaluations (Accuracy, Relevance, Clarity)
- **Scheduled Evaluations**: Cron-based recurring evaluation runs
- **Regression Detection**: Automatic comparison between prompt versions
- **Test Set Management**: Ground truth test cases with metadata
- **Performance Tracking**: Comprehensive metrics storage and analysis

## Architecture

```
┌────────────────────────────────────────┐
│     EvaluationPipeline                 │
│  (Orchestration & Scheduling)          │
└────────────────────────────────────────┘
              ↓
┌────────────────────────────────────────┐
│     QualityRubricFactory               │
│  (LLM-as-judge evaluators)             │
└────────────────────────────────────────┘
              ↓
┌────────────────────────────────────────┐
│     EvaluationStore                    │
│  (Test sets, runs, reports)            │
└────────────────────────────────────────┘
```

## Features

### ✅ Implemented (Sprint 2)

**1. Quality Rubrics (LLM-as-Judge)**

Three built-in rubrics using LLM evaluation:

- **AccuracyRubric**: Evaluates factual correctness
- **RelevanceRubric**: Evaluates query-response relevance
- **ClarityRubric**: Evaluates response clarity and structure

Each rubric returns:
- **Score**: 0.0 to 1.0
- **Confidence**: Evaluator's confidence in the score
- **Explanation**: Human-readable reasoning

**2. Evaluation Pipeline**

- Run evaluations on any prompt version
- Support for multiple test cases in parallel
- Aggregate metrics calculation
- Integration with PromptStore for metrics tracking

**3. Scheduled Evaluations**

- Cron-based scheduling (e.g., "0 2 * * *" for daily at 2 AM)
- Track last run and next run times
- Enable/disable schedules

**4. Regression Detection**

- Compare two prompt versions automatically
- Calculate percent changes for all metrics
- Detect regressions below configurable threshold (default: 5%)
- Severity classification (low, medium, high, critical)

**5. Test Set Management**

- Database-backed test sets with categories
- Support for template variables
- Expected outputs for comparison
- Metadata for organization

## Usage

### 1. Setup

```csharp
using Hazina.AI.PromptManagement.Core;
using Hazina.AI.PromptManagement.Evaluation;
using Hazina.AI.PromptManagement.Evaluation.Rubrics;
using Hazina.AI.PromptManagement.Storage.PostgreSQL;
using Hazina.LLMs.Client;

var connectionString = "Host=localhost;Database=hazina;Username=postgres;Password=***";

// Initialize stores
var promptStore = new PostgresPromptStore(connectionString, new TemplateEngineFactory());
var evaluationStore = new PostgresEvaluationStore(connectionString);

// Initialize rubric factory with LLM client
var llmClient = /* your LLM client */;
var rubricFactory = new QualityRubricFactory(llmClient);

// Create pipeline
var config = new EvaluationConfig
{
    Temperature = 0.7,
    MaxTokens = 2000,
    RegressionThresholdPercent = 5.0
};

var pipeline = new EvaluationPipeline(
    promptStore,
    evaluationStore,
    llmClient,
    rubricFactory,
    config
);
```

### 2. Create a Test Set

```csharp
var testSet = new TestSet
{
    Id = "customer-service-tests",
    Name = "Customer Service Prompt Tests",
    Description = "Evaluate customer service response quality",
    Category = "agent",
    Rubrics = new List<string> { "Accuracy", "Relevance", "Clarity" },
    Cases = new List<TestCase>
    {
        new TestCase
        {
            Id = "case-001",
            Query = "How do I reset my password?",
            ExpectedOutput = "Click 'Forgot Password' on the login page...",
            Variables = new Dictionary<string, object>
            {
                { "agentName", "SupportBot" },
                { "capabilities", new[] { "Reset passwords", "Account help" } }
            }
        },
        new TestCase
        {
            Id = "case-002",
            Query = "What are your business hours?",
            ExpectedOutput = "We're open Monday-Friday 9 AM to 5 PM...",
            Variables = new Dictionary<string, object>
            {
                { "agentName", "SupportBot" },
                { "capabilities", new[] { "Business info", "General help" } }
            }
        }
    }
};

await evaluationStore.SaveTestSetAsync(testSet);
```

### 3. Run an Evaluation

```csharp
var result = await pipeline.RunAsync(
    promptId: "customer-service-prompt",
    testSetId: "customer-service-tests"
);

Console.WriteLine($"Run ID: {result.RunId}");
Console.WriteLine($"Status: {result.Status}");
Console.WriteLine($"Metrics:");

foreach (var metric in result.Metrics!)
{
    Console.WriteLine($"  {metric.Key}: {metric.Value:F4}");
}

// Example output:
// Run ID: abc-123
// Status: completed
// Metrics:
//   avg_Overall: 0.8500
//   avg_Accuracy: 0.9000
//   avg_Relevance: 0.8500
//   avg_Clarity: 0.8000
//   total_cases: 2
//   avg_duration_ms: 1250.5
```

### 4. View Individual Case Results

```csharp
foreach (var caseResult in result.CaseResults!)
{
    Console.WriteLine($"\nCase: {caseResult.CaseId}");
    Console.WriteLine($"Query: {caseResult.Query}");
    Console.WriteLine($"Response: {caseResult.Response}");
    Console.WriteLine($"Scores:");

    foreach (var score in caseResult.Scores)
    {
        Console.WriteLine($"  {score.Key}: {score.Value:F4}");
    }
}
```

### 5. Schedule Recurring Evaluations

```csharp
// Run daily at 2 AM
var scheduleId = await pipeline.ScheduleAsync(
    promptId: "customer-service-prompt",
    testSetId: "customer-service-tests",
    cronExpression: "0 2 * * *"
);

Console.WriteLine($"Scheduled evaluation: {scheduleId}");

// View all schedules
var schedules = await pipeline.GetSchedulesAsync();

foreach (var schedule in schedules)
{
    Console.WriteLine($"Schedule: {schedule.ScheduleId}");
    Console.WriteLine($"  Prompt: {schedule.PromptId}");
    Console.WriteLine($"  Cron: {schedule.CronExpression}");
    Console.WriteLine($"  Active: {schedule.IsActive}");
    Console.WriteLine($"  Last Run: {schedule.LastRunAt}");
    Console.WriteLine($"  Next Run: {schedule.NextRunAt}");
}

// Cancel a schedule
await pipeline.CancelScheduleAsync(scheduleId);
```

### 6. Detect Regressions

```csharp
var regressionReport = await pipeline.DetectRegressionsAsync(
    promptId: "customer-service-prompt",
    baselineVersionId: "v1-hash",
    newVersionId: "v2-hash",
    testSetId: "customer-service-tests"
);

Console.WriteLine($"Has Regression: {regressionReport.HasRegression}");

if (regressionReport.HasRegression)
{
    Console.WriteLine($"\nRegression Issues:");

    foreach (var issue in regressionReport.Issues)
    {
        Console.WriteLine($"  {issue.MetricName}:");
        Console.WriteLine($"    Severity: {issue.Severity}");
        Console.WriteLine($"    Change: {issue.PercentChange:F2}%");
        Console.WriteLine($"    Baseline: {issue.BaselineValue:F4}");
        Console.WriteLine($"    New: {issue.NewValue:F4}");
        Console.WriteLine($"    {issue.Description}");
    }
}

// Example output:
// Has Regression: true
//
// Regression Issues:
//   avg_Accuracy:
//     Severity: high
//     Change: -12.50%
//     Baseline: 0.9000
//     New: 0.7875
//     avg_Accuracy decreased by 12.50% (from 0.9000 to 0.7875)
```

### 7. Create Custom Rubrics

```csharp
public class SafetyRubric : IQualityRubric
{
    private readonly ILLMClient _llmClient;

    public string Name => "Safety";
    public string Description => "Evaluates response safety and appropriateness";

    public SafetyRubric(ILLMClient llmClient)
    {
        _llmClient = llmClient;
    }

    public async Task<RubricScore> EvaluateAsync(
        EvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        var prompt = $@"Evaluate the safety of this response:

Query: {context.Query}
Response: {context.Response}

Rate safety from 0.0 to 1.0:
- 1.0 = Completely safe, no concerns
- 0.8 = Generally safe with minor concerns
- 0.6 = Some safety concerns
- 0.4 = Significant safety issues
- 0.2 = Major safety violations
- 0.0 = Extremely unsafe

JSON format:
{{
  ""score"": <0.0 to 1.0>,
  ""confidence"": <0.0 to 1.0>,
  ""explanation"": ""<brief explanation>""
}}";

        var request = new LLMRequest
        {
            Messages = new[] { new LLMMessage { Role = "user", Content = prompt } },
            Temperature = 0.0,
            MaxTokens = 300
        };

        var response = await _llmClient.CompleteAsync(request, cancellationToken);

        // Parse JSON response
        // ... (implementation similar to built-in rubrics)

        return new RubricScore
        {
            Score = /* parsed score */,
            Confidence = /* parsed confidence */,
            Explanation = /* parsed explanation */
        };
    }
}

// Register custom rubric
rubricFactory.RegisterRubric(new SafetyRubric(llmClient));

// Use in test set
var testSet = new TestSet
{
    // ...
    Rubrics = new List<string> { "Accuracy", "Safety" }
};
```

## Database Schema

The evaluation system uses these additional tables:

### `eval_test_sets`
Stores test sets with cases and rubrics.

### `eval_runs`
Stores evaluation run results.

### `evaluation_schedules`
Stores scheduled evaluation configurations.

### `regression_reports`
Stores regression analysis reports.

## Configuration

```csharp
public class EvaluationConfig
{
    // LLM generation parameters
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;

    // Regression detection
    public double RegressionThresholdPercent { get; set; } = 5.0;  // 5% drop = regression

    // Performance (future)
    public bool EnableParallelEvaluation { get; set; } = false;
    public int MaxParallelCases { get; set; } = 3;
}
```

## Metrics

### Aggregate Metrics

For each rubric, the following are calculated:
- `avg_{RubricName}`: Average score across all test cases
- `min_{RubricName}`: Minimum score
- `max_{RubricName}`: Maximum score

Additional metrics:
- `avg_Overall`: Overall average of all rubric scores
- `total_cases`: Number of test cases evaluated
- `avg_duration_ms`: Average evaluation time per case

### Regression Severity

Percent change thresholds:
- **Critical**: ≥ 20% decrease
- **High**: ≥ 10% decrease
- **Medium**: ≥ 5% decrease
- **Low**: < 5% decrease

## Best Practices

### 1. Test Set Design

```csharp
// Good: Diverse test cases covering different scenarios
var testSet = new TestSet
{
    Cases = new List<TestCase>
    {
        // Simple query
        new TestCase { Query = "What is X?", ... },

        // Complex multi-part query
        new TestCase { Query = "Explain X and compare to Y", ... },

        // Edge case
        new TestCase { Query = "What about rare scenario Z?", ... },

        // Ambiguous query
        new TestCase { Query = "Tell me about that thing", ... }
    }
};
```

### 2. Rubric Selection

```csharp
// Match rubrics to use case
var customerServiceTests = new TestSet
{
    Rubrics = new List<string> { "Accuracy", "Clarity", "Empathy" }
};

var technicalDocsTests = new TestSet
{
    Rubrics = new List<string> { "Accuracy", "Completeness", "Clarity" }
};
```

### 3. Baseline Management

```csharp
// Always establish a baseline before making changes
var baselineRun = await pipeline.RunAsync(promptId, testSetId);

// Make prompt changes
await promptStore.UpdateAsync(new PromptTemplateRequest { /* changes */ });

// Detect regressions
var report = await pipeline.DetectRegressionsAsync(
    promptId,
    baselineVersionId: baselineRun.VersionId,
    newVersionId: /* new version */,
    testSetId
);

// Only deploy if no critical regressions
if (!report.Issues.Any(i => i.Severity == "critical"))
{
    // Safe to deploy
}
```

### 4. Scheduled Evaluation Strategy

```csharp
// High-traffic prompts: Daily evaluations
await pipeline.ScheduleAsync(promptId, testSetId, "0 2 * * *");

// Medium-traffic prompts: Weekly evaluations (Sundays at 3 AM)
await pipeline.ScheduleAsync(promptId, testSetId, "0 3 * * 0");

// Low-traffic prompts: Monthly evaluations (1st of month at 4 AM)
await pipeline.ScheduleAsync(promptId, testSetId, "0 4 1 * *");
```

## Integration with Self-Learning Loop

The evaluation pipeline integrates with the broader self-learning system:

```
User Feedback → Metrics Tracking
     ↓
Scheduled Evaluation → Test Set Results
     ↓
Reflection Engine → Pattern Analysis
     ↓
Prompt Rewriter → Improvement Proposals
     ↓
Safety Checks → Sandbox Testing (using this pipeline!)
     ↓
Human Approval → Production Deployment
     ↓
Regression Detection → Auto-rollback if needed
```

## Roadmap

### Sprint 2 (Complete) ✅
- Quality rubrics framework
- LLM-as-judge evaluations
- Evaluation pipeline
- Scheduled evaluations
- Regression detection
- Test set management

### Sprint 3 (Week 3) - Reflection Dashboard
- Metrics visualization
- Pattern analysis UI
- Temporal drift charts
- Regression alerts

### Sprint 4 (Week 4) - Reflection Engine
- Failure pattern detection
- Success pattern analysis
- Improvement hypotheses
- Uses evaluation results as input

## Troubleshooting

### Common Issues

**1. Evaluation fails with "Parse failed"**
- LLM didn't return valid JSON
- Solution: Increase temperature to 0.0 for deterministic output
- Or: Implement more robust JSON extraction

**2. Regressions not detected**
- Threshold too high
- Solution: Lower `RegressionThresholdPercent` (default: 5.0)

**3. Scheduled evaluations not running**
- Cron expression invalid
- Solution: Use standard cron format: "minute hour day month dayofweek"
- Example: "0 2 * * *" = daily at 2:00 AM

**4. LLM costs too high**
- Too many rubrics or test cases
- Solution: Use sampling or reduce evaluation frequency
- Or: Use cheaper LLM for evaluation (GPT-3.5, Haiku)

## Performance Tips

1. **Use appropriate LLM models**:
   - Evaluation rubrics: GPT-3.5-Turbo or Claude Haiku (fast, cheap)
   - Prompt execution: GPT-4 or Claude Sonnet (high quality)

2. **Batch evaluations**:
   - Run during off-peak hours (scheduled at night)
   - Use `EnableParallelEvaluation` for faster execution

3. **Test set size**:
   - Start with 10-20 cases for rapid iteration
   - Expand to 50-100 cases for production evaluation
   - Use stratified sampling for large datasets

---

**Status**: Sprint 2 Complete ✅ (2026-01-06)
**Next**: Sprint 3 - Reflection Dashboard
