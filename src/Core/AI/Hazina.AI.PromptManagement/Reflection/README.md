# Hazina Prompt Management - Reflection Engine

## Overview

The Reflection Engine is an automated system that analyzes prompt performance patterns and generates data-driven improvement hypotheses using LLM-based analysis. It bridges the gap between raw evaluation data and actionable prompt improvements.

## Core Concept

The Reflection Engine operates on a simple principle:
1. **Analyze**: Examine evaluation results to detect failure and success patterns
2. **Reflect**: Use an LLM to deeply analyze patterns and understand root causes
3. **Hypothesize**: Generate specific, testable improvement suggestions
4. **Store**: Persist insights for consumption by the Prompt Rewriter

## Architecture

### Components

1. **IReflectionEngine** - Main reflection interface
   - `GenerateReportAsync()` - Create comprehensive reflection report for a time period
   - `GetReportAsync()` - Retrieve existing report
   - `GetReportHistoryAsync()` - View historical reflections
   - `AnalyzePatternsAsync()` - Deep-dive into specific patterns

2. **IReflectionStore** - Persistence layer
   - PostgreSQL implementation with JSONB storage
   - Efficient querying by prompt and date range
   - Cleanup support for old reports

3. **ReflectionReport** - Structured output
   - Failure pattern summaries
   - Success pattern summaries
   - Improvement hypotheses with confidence scores
   - Overall assessment from LLM

## How It Works

### Step 1: Data Collection

The Reflection Engine queries evaluation data over a specified time window:

```csharp
var report = await reflectionEngine.GenerateReportAsync(
    promptId: "my-rag-prompt",
    startDate: DateTime.UtcNow.AddDays(-30),
    endDate: DateTime.UtcNow,
    minRunsRequired: 10  // Minimum evaluations needed
);
```

**Requirements**:
- Minimum 10 evaluation runs (configurable)
- Evaluation runs must have quality scores (Accuracy, Relevance, Clarity, etc.)
- Time window should be meaningful (typically 7-30 days)

### Step 2: Pattern Detection

Uses the `IPatternAnalyzer` to identify recurring issues and successes:

**Failure Patterns** (detected at 5% frequency threshold):
- Low accuracy scores (factual errors)
- Low relevance scores (off-topic responses)
- High latency (>5s response time)

**Success Patterns** (detected at 10% frequency threshold):
- Excellent accuracy (>0.9 scores)
- Strong relevance alignment
- Fast, high-quality responses

### Step 3: LLM-Based Analysis

The engine constructs a detailed prompt for an LLM containing:
- Current prompt template
- Performance metrics
- Detected patterns with examples
- Success factors

**Example LLM Prompt Structure**:
```
You are an expert AI prompt engineer analyzing prompt performance data.
Generate specific, actionable improvement hypotheses based on the patterns below.

## Prompt Being Analyzed
**Name**: RAG Document Retrieval
**Category**: rag
**Current Template**:
```
You are a helpful assistant...
```

## Performance Metrics
- Success Rate: 78.5%
- Accuracy: 0.72
- Relevance: 0.81
- Clarity: 0.88

## Failure Patterns Detected
### Pattern: Low accuracy scores - factual errors
- Frequency: 15.3% (23 occurrences)
- Root Cause: Prompt lacks fact-checking instructions
- Examples: [...]

## Task
Generate 2-5 improvement hypotheses in JSON format...
```

### Step 4: Hypothesis Generation

The LLM returns structured hypotheses:

```json
[
  {
    "hypothesis": "Add explicit fact-checking step",
    "rationale": "15% of cases have accuracy issues due to lack of verification instructions",
    "expectedImpact": 0.12,
    "confidence": 0.85,
    "priority": "high",
    "addressedPatterns": ["low-accuracy-abc123"],
    "targetMetrics": ["Accuracy", "Overall"],
    "suggestedChanges": [
      {
        "changeType": "Add",
        "section": "instructions",
        "currentValue": "",
        "proposedValue": "Before responding, verify facts against the retrieved documents...",
        "reason": "Explicit verification reduces factual errors"
      }
    ]
  }
]
```

### Step 5: Overall Assessment

An LLM generates a natural language summary:

```
The prompt performs well overall with a 78.5% success rate. However, accuracy is
the weakest metric at 0.72, primarily due to insufficient fact-checking guidance.
The most impactful improvement would be adding explicit verification steps before
generating responses. Implementation of the top 2 hypotheses could improve overall
performance by an estimated 10-15%.
```

### Step 6: Storage

The complete report is saved to the database:

```sql
INSERT INTO reflection_reports (
    report_id, prompt_id, start_date, end_date,
    total_runs, success_rate,
    failure_patterns, success_patterns,
    improvement_hypotheses, overall_assessment
) VALUES (...);
```

## Usage Examples

### Basic Report Generation

```csharp
var reflectionEngine = new ReflectionEngine(
    patternAnalyzer,
    promptStore,
    reflectionStore,
    llmClient
);

// Generate report for last 30 days
var report = await reflectionEngine.GenerateReportAsync(
    "my-prompt-id",
    DateTime.UtcNow.AddDays(-30),
    DateTime.UtcNow
);

Console.WriteLine($"Success Rate: {report.SuccessRate:P}");
Console.WriteLine($"Failure Patterns: {report.FailurePatterns.Count}");
Console.WriteLine($"Hypotheses Generated: {report.ImprovementHypotheses.Count}");

// View top hypotheses
foreach (var hypothesis in report.ImprovementHypotheses
    .OrderByDescending(h => h.ExpectedImpact * h.Confidence)
    .Take(3))
{
    Console.WriteLine($"\n{hypothesis.Hypothesis}");
    Console.WriteLine($"  Expected Impact: {hypothesis.ExpectedImpact:P}");
    Console.WriteLine($"  Confidence: {hypothesis.Confidence:P}");
    Console.WriteLine($"  Priority: {hypothesis.Priority}");
}
```

### Accessing Reflection History

```csharp
// Get recent reflections for a prompt
var history = await reflectionEngine.GetReportHistoryAsync(
    "my-prompt-id",
    limit: 5
);

// Track improvement over time
for (int i = 0; i < history.Count - 1; i++)
{
    var current = history[i];
    var previous = history[i + 1];

    var improvement = current.SuccessRate - previous.SuccessRate;
    Console.WriteLine($"Period {current.StartDate:yyyy-MM-dd}: {improvement:+0.0%;-0.0%}");
}
```

### Analyzing Specific Patterns

```csharp
// Deep-dive into particular failure patterns
var targetPatternIds = new List<string>
{
    "low-accuracy-abc123",
    "high-latency-def456"
};

var targetedHypotheses = await reflectionEngine.AnalyzePatternsAsync(
    "my-prompt-id",
    targetPatternIds
);

// These hypotheses are highly focused on the specific patterns
foreach (var hypothesis in targetedHypotheses)
{
    Console.WriteLine($"Addresses: {string.Join(", ", hypothesis.AddressedPatterns)}");
    Console.WriteLine($"Changes:");
    foreach (var change in hypothesis.SuggestedChanges)
    {
        Console.WriteLine($"  {change.ChangeType} in {change.Section}: {change.Reason}");
    }
}
```

## Data Model

### ReflectionReport

```csharp
public class ReflectionReport
{
    // Identity
    public string ReportId { get; set; }
    public string PromptId { get; set; }
    public DateTime CreatedAt { get; set; }

    // Analysis period
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    // Performance summary
    public int TotalRuns { get; set; }
    public double SuccessRate { get; set; }  // 0.0 to 1.0
    public Dictionary<string, double> AverageMetrics { get; set; }

    // Patterns
    public List<FailurePatternSummary> FailurePatterns { get; set; }
    public List<SuccessPatternSummary> SuccessPatterns { get; set; }

    // Insights
    public List<ImprovementHypothesis> ImprovementHypotheses { get; set; }
    public string OverallAssessment { get; set; }

    // Metadata
    public Dictionary<string, object> Metadata { get; set; }
}
```

### FailurePatternSummary

```csharp
public class FailurePatternSummary
{
    public string PatternId { get; set; }
    public string Description { get; set; }
    public double Frequency { get; set; }  // 0.0 to 1.0
    public int Occurrences { get; set; }
    public List<string> Examples { get; set; }
    public string RootCause { get; set; }
    public List<string> AffectedMetrics { get; set; }
    public double Impact { get; set; }  // Severity * Frequency
}
```

### SuccessPatternSummary

```csharp
public class SuccessPatternSummary
{
    public string PatternId { get; set; }
    public string Description { get; set; }
    public double Frequency { get; set; }
    public int Occurrences { get; set; }
    public List<string> Examples { get; set; }
    public List<string> KeyFactors { get; set; }
    public Dictionary<string, double> MetricImpact { get; set; }
}
```

### ImprovementHypothesis

```csharp
public class ImprovementHypothesis
{
    public string HypothesisId { get; set; }
    public string Hypothesis { get; set; }  // What to change
    public string Rationale { get; set; }  // Why it will help
    public double ExpectedImpact { get; set; }  // 0.0 to 1.0
    public double Confidence { get; set; }  // 0.0 to 1.0
    public string Priority { get; set; }  // low|medium|high|critical

    public List<string> AddressedPatterns { get; set; }
    public List<string> TargetMetrics { get; set; }
    public List<SuggestedChange> SuggestedChanges { get; set; }
}
```

### SuggestedChange

```csharp
public class SuggestedChange
{
    public string ChangeType { get; set; }  // Add|Remove|Modify|Restructure
    public string Section { get; set; }  // instructions|examples|constraints|format
    public string CurrentValue { get; set; }
    public string ProposedValue { get; set; }
    public string Reason { get; set; }
}
```

## Integration with Other Components

### From Evaluation Pipeline

The Reflection Engine consumes evaluation results:

```csharp
// Evaluation Pipeline generates:
eval_runs → metrics → patterns

// Reflection Engine analyzes:
patterns → hypotheses
```

### To Prompt Rewriter (Sprint 5)

Hypotheses feed into the Prompt Rewriter:

```csharp
var latestReport = await reflectionStore.GetLatestReportAsync("my-prompt-id");

foreach (var hypothesis in latestReport.ImprovementHypotheses)
{
    // Rewriter applies suggested changes
    var proposal = await promptRewriter.ApplyHypothesisAsync(
        currentPrompt,
        hypothesis
    );
}
```

### Dashboard Integration

Reflection reports can be visualized:

```sql
-- Latest reflection summary
SELECT
    rr.report_id,
    rr.success_rate,
    JSONB_ARRAY_LENGTH(rr.failure_patterns) as failure_count,
    JSONB_ARRAY_LENGTH(rr.improvement_hypotheses) as hypothesis_count,
    rr.overall_assessment
FROM reflection_reports rr
WHERE rr.prompt_id = 'my-prompt-id'
ORDER BY rr.created_at DESC
LIMIT 1;

-- Hypothesis effectiveness over time
SELECT
    rr.created_at,
    h.hypothesis,
    h.expected_impact,
    h.priority
FROM reflection_reports rr,
LATERAL JSONB_TO_RECORDSET(rr.improvement_hypotheses) AS h(
    hypothesis TEXT,
    expected_impact FLOAT,
    priority TEXT
)
WHERE rr.prompt_id = 'my-prompt-id'
ORDER BY rr.created_at DESC;
```

## Best Practices

### 1. Analysis Frequency

- **High-traffic prompts**: Weekly reflections
- **Medium-traffic prompts**: Bi-weekly reflections
- **Low-traffic prompts**: Monthly reflections

Wait for sufficient data (minimum 10-20 evaluations) before generating reports.

### 2. Time Windows

- **Short-term (7 days)**: Recent performance, quick iterations
- **Medium-term (30 days)**: Stable patterns, confident hypotheses
- **Long-term (90 days)**: Seasonal trends, comprehensive analysis

### 3. Hypothesis Prioritization

Sort hypotheses by combined score:

```csharp
var prioritized = hypotheses
    .OrderByDescending(h => h.ExpectedImpact * h.Confidence)
    .ThenBy(h => h.Priority == "critical" ? 0 :
                 h.Priority == "high" ? 1 :
                 h.Priority == "medium" ? 2 : 3)
    .ToList();
```

### 4. Pattern Thresholds

Adjust frequency thresholds based on use case:

```csharp
// Strict: Only very common patterns
var report = await reflectionEngine.GenerateReportAsync(
    promptId, startDate, endDate,
    minRunsRequired: 50  // Need more data for confidence
);

// Sensitive: Catch rare but impactful issues
var analyzer = new PatternAnalyzer(connectionString);
var patterns = await analyzer.DetectFailurePatternsAsync(
    promptId, startDate, endDate,
    minFrequency: 0.02  // 2% threshold instead of default 5%
);
```

### 5. LLM Selection

- **GPT-4 / Claude Sonnet**: Best hypothesis quality, higher cost
- **GPT-3.5 / Claude Haiku**: Faster, cheaper, good for high-frequency analysis
- **Temperature**: Use 0.3-0.4 for focused, consistent analysis

### 6. Handling Insufficient Data

```csharp
try
{
    var report = await reflectionEngine.GenerateReportAsync(
        promptId, startDate, endDate,
        minRunsRequired: 10
    );
}
catch (InvalidOperationException ex) when (ex.Message.Contains("Insufficient"))
{
    // Not enough data yet - wait for more evaluations
    Console.WriteLine($"Need more evaluation runs. {ex.Message}");
    return;
}
```

## Advanced Features

### Custom Pattern Analysis

Combine built-in pattern detection with custom logic:

```csharp
// Get standard patterns
var failurePatterns = await patternAnalyzer.DetectFailurePatternsAsync(
    promptId, startDate, endDate
);

// Add custom pattern detection
var customPattern = new FailurePatternSummary
{
    PatternId = "custom-domain-specific",
    Description = "Medical terminology errors",
    Frequency = 0.08,
    Occurrences = 12,
    Examples = FindMedicalErrors(evaluationResults),
    RootCause = "Lack of medical knowledge base",
    AffectedMetrics = new List<string> { "Accuracy", "Relevance" },
    Impact = 0.15
};

failurePatterns.Add(customPattern);
```

### Hypothesis Filtering

Filter hypotheses before consumption:

```csharp
// Only high-confidence, high-impact hypotheses
var actionable = report.ImprovementHypotheses
    .Where(h => h.Confidence >= 0.7)
    .Where(h => h.ExpectedImpact >= 0.1)
    .Where(h => h.Priority == "high" || h.Priority == "critical")
    .ToList();

// Only hypotheses addressing specific metrics
var accuracyFocused = report.ImprovementHypotheses
    .Where(h => h.TargetMetrics.Contains("Accuracy"))
    .ToList();
```

### Comparative Analysis

Compare reflections across prompts:

```csharp
var prompts = new[] { "prompt-a", "prompt-b", "prompt-c" };
var reports = new Dictionary<string, ReflectionReport>();

foreach (var promptId in prompts)
{
    var report = await reflectionStore.GetLatestReportAsync(promptId);
    if (report != null)
    {
        reports[promptId] = report;
    }
}

// Find best-performing prompt
var best = reports.OrderByDescending(r => r.Value.SuccessRate).First();
Console.WriteLine($"Best prompt: {best.Key} ({best.Value.SuccessRate:P})");

// Cross-pollinate successful patterns
var allSuccessPatterns = reports.Values
    .SelectMany(r => r.SuccessPatterns)
    .GroupBy(p => p.Description)
    .Where(g => g.Count() >= 2)  // Patterns appearing in multiple prompts
    .ToList();
```

## Troubleshooting

### Issue: No hypotheses generated

**Possible causes**:
- No failure patterns detected (prompt performing well!)
- LLM response parsing failed
- Insufficient pattern data

**Solutions**:
```csharp
// Check pattern detection
var patterns = await patternAnalyzer.DetectFailurePatternsAsync(
    promptId, startDate, endDate,
    minFrequency: 0.05
);

if (!patterns.Any())
{
    // Lower threshold
    patterns = await patternAnalyzer.DetectFailurePatternsAsync(
        promptId, startDate, endDate,
        minFrequency: 0.01  // 1% instead of 5%
    );
}

// Check LLM response
var rawResponse = await llmClient.CompleteAsync(request, cancellationToken);
Console.WriteLine(rawResponse.Choices[0].Message.Content);
```

### Issue: Hypotheses are too generic

**Solution**: Use targeted analysis for specific patterns:

```csharp
// Instead of full report
var report = await reflectionEngine.GenerateReportAsync(...);

// Use targeted analysis
var specificHypotheses = await reflectionEngine.AnalyzePatternsAsync(
    promptId,
    new List<string> { "low-accuracy-123" }  // Focus on one pattern
);
```

### Issue: High LLM costs

**Solutions**:
1. Use cheaper models for initial analysis
2. Increase time between reflections
3. Batch multiple prompts in single LLM call (custom implementation)
4. Cache and reuse hypotheses for similar patterns

```csharp
// Use Haiku instead of Sonnet for cost savings
var llmClient = new LLMClient("claude-haiku-3-5");
var reflectionEngine = new ReflectionEngine(
    patternAnalyzer,
    promptStore,
    reflectionStore,
    llmClient
);
```

## Performance Considerations

### Database Queries

Reflection reports use JSONB extensively. Index properly:

```sql
-- Already included in migration
CREATE INDEX idx_reflection_reports_prompt_id ON reflection_reports(prompt_id);
CREATE INDEX idx_reflection_reports_created_at ON reflection_reports(created_at DESC);

-- Additional indexes for JSONB queries
CREATE INDEX idx_reflection_hypotheses_priority
ON reflection_reports USING GIN ((improvement_hypotheses -> 'priority'));
```

### Memory Usage

Large reports (100+ patterns, 50+ hypotheses) can consume significant memory:

```csharp
// Limit pattern count in report generation
var failurePatterns = (await patternAnalyzer.DetectFailurePatternsAsync(...))
    .OrderByDescending(p => p.Impact)
    .Take(10)  // Top 10 most impactful
    .ToList();

// Or paginate when retrieving history
var page1 = await reflectionStore.GetReportHistoryAsync(promptId, limit: 10);
var page2 = await reflectionStore.GetReportHistoryAsync(promptId, limit: 10, offset: 10);
```

### LLM Latency

Hypothesis generation involves LLM calls (~2-5 seconds):

```csharp
// Run reflections asynchronously in background
var reflectionTask = Task.Run(async () =>
{
    return await reflectionEngine.GenerateReportAsync(...);
});

// Continue other work
// ...

// Get result when needed
var report = await reflectionTask;
```

## API Reference

See interface documentation:
- `IReflectionEngine.cs` - Core reflection interface
- `IReflectionStore.cs` - Storage interface
- `ReflectionEngine.cs` - Main implementation
- `PostgresReflectionStore.cs` - PostgreSQL storage

## Examples

Complete working examples:
- `Examples/ReflectionUsage.cs` - Basic usage
- `Examples/CustomPatternAnalysis.cs` - Advanced pattern detection
- `Examples/HypothesisFiltering.cs` - Filtering and prioritization

## Next Steps

After reflection, hypotheses flow into:
- **Sprint 5: Prompt Rewriter** - Applies hypotheses to generate improved prompts
- **Sprint 6: Safety Coordinator** - Validates proposed changes
- **Sprint 7: Approval Workflow** - Human review before deployment

---

**Last Updated**: 2026-01-06
**Related Documentation**:
- Evaluation Pipeline README
- Dashboard README
- Pattern Analyzer interface
