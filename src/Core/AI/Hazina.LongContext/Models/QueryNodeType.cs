namespace Hazina.LongContext.Models;

/// <summary>
/// Type of query node in the recursive query tree
/// </summary>
public enum QueryNodeType
{
    /// <summary>
    /// Root node representing the original user question
    /// </summary>
    Root,

    /// <summary>
    /// Node that decomposes a question into sub-questions
    /// </summary>
    Decomposition,

    /// <summary>
    /// Node that retrieves relevant shards for a specific question
    /// </summary>
    Retrieval,

    /// <summary>
    /// Node that summarizes retrieved content or child results
    /// </summary>
    Summarization,

    /// <summary>
    /// Node that aggregates answers from multiple child nodes
    /// </summary>
    Aggregation
}
