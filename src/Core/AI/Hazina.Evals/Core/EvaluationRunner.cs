using System.Diagnostics;
using Hazina.AI.RAG.Interfaces;
using Hazina.Evals.Metrics;
using Hazina.Evals.Models;

namespace Hazina.Evals.Core;

/// <summary>
/// Runs evaluation test cases against a retrieval pipeline.
/// </summary>
public class EvaluationRunner
{
    private readonly IRetrievalPipeline _pipeline;
    private readonly EvaluationOptions _options;

    public EvaluationRunner(IRetrievalPipeline pipeline, EvaluationOptions? options = null)
    {
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _options = options ?? new EvaluationOptions();
    }

    /// <summary>
    /// Run evaluation on a set of test cases
    /// </summary>
    public async Task<EvalRun> RunEvaluationAsync(
        string runId,
        List<EvalCase> testCases,
        CancellationToken cancellationToken = default)
    {
        var run = new EvalRun
        {
            RunId = runId,
            Retriever = _pipeline.GetType().GetProperty("_retriever",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_pipeline) as IRetriever ?? null!,
            Reranker = _pipeline.GetType().GetProperty("_reranker",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.GetValue(_pipeline) as IReranker ?? null!,
            Description = _options.Description
        };

        foreach (var testCase in testCases)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var caseResult = await EvaluateCaseAsync(testCase, cancellationToken);
            run.CaseResults.Add(caseResult);

            if (_options.Verbose)
            {
                Console.WriteLine($"[{testCase.Id}] MRR: {caseResult.Metrics.MRR:F4}, " +
                                  $"Hit@{_options.K}: {caseResult.Metrics.HitAtK:F4}");
            }
        }

        run.AggregateMetrics = CalculateAggregateMetrics(run.CaseResults);

        return run;
    }

    private async Task<EvalCaseResult> EvaluateCaseAsync(
        EvalCase testCase,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        var retrieved = await _pipeline.RetrieveAsync(
            testCase.Query,
            retrievalTopK: _options.RetrievalTopK,
            rerankTopN: _options.K,
            cancellationToken: cancellationToken
        );

        sw.Stop();

        var metrics = RetrievalMetrics.CalculateAllMetrics(retrieved, testCase, _options.K);
        metrics.AvgRetrievalTime = sw.Elapsed;

        return new EvalCaseResult
        {
            CaseId = testCase.Id,
            Query = testCase.Query,
            RetrievedCandidates = retrieved,
            Metrics = metrics,
            Duration = sw.Elapsed
        };
    }

    private EvalMetrics CalculateAggregateMetrics(List<EvalCaseResult> caseResults)
    {
        if (caseResults.Count == 0)
            return new EvalMetrics();

        var aggregate = new EvalMetrics
        {
            HitAtK = caseResults.Average(r => r.Metrics.HitAtK ?? 0.0),
            MRR = caseResults.Average(r => r.Metrics.MRR ?? 0.0),
            NDCG = caseResults.Average(r => r.Metrics.NDCG ?? 0.0),
            PrecisionAtK = caseResults.Average(r => r.Metrics.PrecisionAtK ?? 0.0),
            RecallAtK = caseResults.Average(r => r.Metrics.RecallAtK ?? 0.0),
            AvgRetrievalTime = TimeSpan.FromMilliseconds(
                caseResults.Average(r => r.Duration.TotalMilliseconds)
            )
        };

        return aggregate;
    }
}

/// <summary>
/// Configuration options for evaluation runs
/// </summary>
public class EvaluationOptions
{
    /// <summary>
    /// Number of candidates to retrieve
    /// </summary>
    public int RetrievalTopK { get; set; } = 20;

    /// <summary>
    /// K for metrics calculation (Hit@K, Precision@K, etc.)
    /// </summary>
    public int K { get; set; } = 5;

    /// <summary>
    /// Print detailed results during evaluation
    /// </summary>
    public bool Verbose { get; set; } = false;

    /// <summary>
    /// Optional description for this evaluation configuration
    /// </summary>
    public string? Description { get; set; }
}
