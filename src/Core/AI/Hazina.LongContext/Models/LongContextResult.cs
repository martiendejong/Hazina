namespace Hazina.LongContext.Models;

/// <summary>
/// Result of a long-context query execution
/// </summary>
public class LongContextResult
{
    /// <summary>
    /// The final synthesized answer
    /// </summary>
    public string FinalAnswer { get; init; } = string.Empty;

    /// <summary>
    /// The query tree that was executed
    /// </summary>
    public QueryNode QueryTree { get; init; } = new();

    /// <summary>
    /// All shards that were used across the entire query
    /// </summary>
    public IReadOnlyList<ContextShard> UsedShards { get; init; } = Array.Empty<ContextShard>();

    /// <summary>
    /// Total tokens consumed across all nodes
    /// </summary>
    public int TotalTokensUsed { get; init; }

    /// <summary>
    /// Total execution time
    /// </summary>
    public TimeSpan TotalExecutionTime { get; init; }

    /// <summary>
    /// Whether the query was successful
    /// </summary>
    public bool Success { get; init; } = true;

    /// <summary>
    /// Error message if query failed
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Session ID for tracking multi-turn conversations
    /// </summary>
    public LongContextSessionId? SessionId { get; init; }

    /// <summary>
    /// Statistics about query execution
    /// </summary>
    public QueryStatistics Statistics { get; init; } = new();

    /// <summary>
    /// Execution mode used (parallel or sequential)
    /// </summary>
    public ExecutionMode ExecutionMode { get; init; }

    /// <summary>
    /// Create a result from a root node execution
    /// </summary>
    public static LongContextResult FromRootNode(QueryNodeResult rootResult, QueryNode tree)
    {
        return new LongContextResult
        {
            FinalAnswer = rootResult.Answer,
            QueryTree = tree,
            UsedShards = rootResult.UsedShards,
            TotalTokensUsed = rootResult.TokensUsed,
            TotalExecutionTime = rootResult.ExecutionTime,
            Success = rootResult.Success,
            Error = rootResult.ErrorMessage,
            Statistics = CalculateStatistics(tree, rootResult)
        };
    }

    private static QueryStatistics CalculateStatistics(QueryNode tree, QueryNodeResult result)
    {
        var stats = new QueryStatistics();
        CountNodesRecursive(tree, stats);
        stats.TotalShards = result.UsedShards.Count;
        return stats;
    }

    /// <summary>
    /// Create result with execution mode specified
    /// </summary>
    public static LongContextResult FromRootNode(
        QueryNodeResult rootResult,
        QueryNode tree,
        ExecutionMode executionMode)
    {
        var result = FromRootNode(rootResult, tree);
        return new LongContextResult
        {
            FinalAnswer = result.FinalAnswer,
            QueryTree = result.QueryTree,
            UsedShards = result.UsedShards,
            TotalTokensUsed = result.TotalTokensUsed,
            TotalExecutionTime = result.TotalExecutionTime,
            Success = result.Success,
            Error = result.Error,
            Statistics = result.Statistics,
            SessionId = result.SessionId,
            ExecutionMode = executionMode
        };
    }

    private static void CountNodesRecursive(QueryNode node, QueryStatistics stats)
    {
        stats.TotalNodes++;

        switch (node.Type)
        {
            case QueryNodeType.Retrieval:
                stats.RetrievalNodes++;
                break;
            case QueryNodeType.Decomposition:
                stats.DecompositionNodes++;
                break;
            case QueryNodeType.Summarization:
                stats.SummarizationNodes++;
                break;
            case QueryNodeType.Aggregation:
                stats.AggregationNodes++;
                break;
        }

        if (node.Depth > stats.MaxDepth)
            stats.MaxDepth = node.Depth;

        foreach (var child in node.Children)
        {
            CountNodesRecursive(child, stats);
        }
    }
}

/// <summary>
/// Statistics about query execution
/// </summary>
public class QueryStatistics
{
    public int TotalNodes { get; set; }
    public int RetrievalNodes { get; set; }
    public int DecompositionNodes { get; set; }
    public int SummarizationNodes { get; set; }
    public int AggregationNodes { get; set; }
    public int MaxDepth { get; set; }
    public int TotalShards { get; set; }
}
