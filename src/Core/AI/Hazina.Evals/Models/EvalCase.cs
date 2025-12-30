namespace Hazina.Evals.Models;

/// <summary>
/// Represents a single evaluation test case for retrieval quality assessment.
/// </summary>
public class EvalCase
{
    /// <summary>
    /// Unique identifier for the evaluation case
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The query to evaluate
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Expected chunk IDs that should be retrieved (ground truth)
    /// </summary>
    public List<string>? ExpectedChunkIds { get; init; }

    /// <summary>
    /// Expected answer fragments or keywords that should appear in the response
    /// </summary>
    public List<string>? ExpectedAnswerFragments { get; init; }

    /// <summary>
    /// Optional metadata for categorization or analysis
    /// </summary>
    public Dictionary<string, object>? Metadata { get; init; }

    /// <summary>
    /// Optional relevance judgments (chunk ID → relevance score)
    /// Used for nDCG calculation
    /// </summary>
    public Dictionary<string, int>? RelevanceJudgments { get; init; }
}
