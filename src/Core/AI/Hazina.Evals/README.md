# Hazina.Evals - Retrieval Evaluation Harness

Comprehensive evaluation framework for assessing retrieval and RAG pipeline quality.

## Purpose

Prevent silent quality regressions and enable systematic comparison of retrieval configurations through automated testing and metrics.

## Features

### Metrics Supported

1. **Hit@K** - Fraction of queries where at least one relevant document appears in top K results
2. **MRR (Mean Reciprocal Rank)** - Average of 1/rank of first relevant document
3. **nDCG (Normalized Discounted Cumulative Gain)** - Relevance-weighted ranking quality
4. **Precision@K** - Fraction of top K results that are relevant
5. **Recall@K** - Fraction of all relevant documents retrieved in top K

### Key Components

#### EvalCase
Defines a single test case with:
- Query text
- Expected chunk IDs (ground truth)
- Optional relevance judgments for nDCG
- Metadata for categorization

#### EvalRun
Represents a complete evaluation run:
- Pipeline configuration (retriever + reranker)
- Test case results
- Aggregate metrics across all cases
- Timestamp and metadata

#### EvaluationRunner
Executes test cases against a pipeline:
- Runs queries through retrieval pipeline
- Calculates metrics per case
- Aggregates results
- Measures retrieval latency

#### RegressionComparison
Compares two evaluation runs:
- Detects metric regressions (>5% drop)
- Identifies latency regressions (>500ms increase)
- Generates console summaries
- Exports JSONL reports

## Usage

### Creating Test Cases

```csharp
var testCases = new List<EvalCase>
{
    new EvalCase
    {
        Id = "test-001",
        Query = "What is the capital of France?",
        ExpectedChunkIds = new List<string> { "doc-france-chunk-0", "doc-europe-chunk-5" },
        RelevanceJudgments = new Dictionary<string, int>
        {
            ["doc-france-chunk-0"] = 3,  // Highly relevant
            ["doc-europe-chunk-5"] = 2,  // Moderately relevant
            ["doc-geography-chunk-2"] = 1 // Slightly relevant
        }
    }
};
```

### Running Evaluation

```csharp
var pipeline = serviceProvider.GetRequiredService<IRetrievalPipeline>();
var runner = new EvaluationRunner(pipeline, new EvaluationOptions
{
    RetrievalTopK = 20,
    K = 5,
    Verbose = true
});

var run = await runner.RunEvaluationAsync("baseline-v1", testCases);

Console.WriteLine($"MRR: {run.AggregateMetrics.MRR:F4}");
Console.WriteLine($"Hit@5: {run.AggregateMetrics.HitAtK:F4}");
```

### Regression Comparison

```csharp
// Run baseline
var baselineRun = await runner.RunEvaluationAsync("baseline", testCases);

// Run candidate with new reranker
var candidateRun = await runner.RunEvaluationAsync("candidate", testCases);

// Compare
var report = RegressionComparison.Compare(baselineRun, candidateRun);
Console.WriteLine(RegressionComparison.GenerateConsoleSummary(report));

// Export to JSONL
RegressionComparison.ExportToJsonL(report, "regression-results.jsonl");
```

## CI Integration

Evaluation is designed for headless execution:

```bash
dotnet run --project YourEvalApp -- --baseline baseline.json --candidate candidate.json
```

Exit codes:
- 0: No regression detected
- 1: Regression detected (metrics dropped >5% or latency increased >500ms)

## Best Practices

1. **Build a diverse test set** - Cover common queries, edge cases, and domain-specific scenarios
2. **Use relevance judgments** - Binary relevant/not-relevant is limiting; use graded relevance (0-3) for nDCG
3. **Track over time** - Store evaluation runs in JSONL for historical analysis
4. **Set thresholds** - Define acceptable regression thresholds based on your use case
5. **Run regularly** - Integrate into CI/CD to catch regressions before deployment

## Example Workflow

1. Create golden test set with relevance judgments
2. Run baseline evaluation and save results
3. Implement new reranker or retrieval strategy
4. Run evaluation with new configuration
5. Compare against baseline
6. If no regression, deploy; otherwise investigate

## File Format

### JSONL Output

Each line is a complete JSON object representing either an EvalRun or ComparisonReport. This format allows:
- Streaming evaluation results
- Easy appending without parsing full file
- Line-by-line processing for large datasets
- Git-friendly (line-based diffs)
