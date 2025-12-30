using Hazina.AI.RAG.Interfaces;

namespace Hazina.Evals.Models;

/// <summary>
/// Represents a single evaluation run with configuration and results.
/// </summary>
public class EvalRun
{
    /// <summary>
    /// Unique identifier for this evaluation run
    /// </summary>
    public required string RunId { get; init; }

    /// <summary>
    /// Timestamp when the evaluation started
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Retrieval configuration used
    /// </summary>
    public required IRetriever Retriever { get; init; }

    /// <summary>
    /// Reranker configuration used
    /// </summary>
    public required IReranker Reranker { get; init; }

    /// <summary>
    /// Optional description or notes about this run
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Evaluation cases executed
    /// </summary>
    public List<EvalCaseResult> CaseResults { get; init; } = new();

    /// <summary>
    /// Aggregate metrics for the entire run
    /// </summary>
    public EvalMetrics? AggregateMetrics { get; set; }
}

/// <summary>
/// Result of evaluating a single EvalCase
/// </summary>
public class EvalCaseResult
{
    /// <summary>
    /// Reference to the evaluation case
    /// </summary>
    public required string CaseId { get; init; }

    /// <summary>
    /// Query that was executed
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Candidates retrieved
    /// </summary>
    public required List<IRetrievalCandidate> RetrievedCandidates { get; init; }

    /// <summary>
    /// Metrics for this specific case
    /// </summary>
    public required EvalMetrics Metrics { get; init; }

    /// <summary>
    /// Duration of retrieval + reranking
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Evaluation metrics
/// </summary>
public class EvalMetrics
{
    /// <summary>
    /// Hit@K - Fraction of queries where at least one relevant document is in top K
    /// </summary>
    public double? HitAtK { get; set; }

    /// <summary>
    /// Mean Reciprocal Rank - Average of 1/rank of first relevant document
    /// </summary>
    public double? MRR { get; set; }

    /// <summary>
    /// Normalized Discounted Cumulative Gain - Relevance-weighted ranking quality
    /// </summary>
    public double? NDCG { get; set; }

    /// <summary>
    /// Precision@K - Fraction of retrieved documents that are relevant
    /// </summary>
    public double? PrecisionAtK { get; set; }

    /// <summary>
    /// Recall@K - Fraction of relevant documents that were retrieved
    /// </summary>
    public double? RecallAtK { get; set; }

    /// <summary>
    /// Average retrieval time
    /// </summary>
    public TimeSpan? AvgRetrievalTime { get; set; }

    /// <summary>
    /// Additional custom metrics
    /// </summary>
    public Dictionary<string, double>? CustomMetrics { get; init; }
}
