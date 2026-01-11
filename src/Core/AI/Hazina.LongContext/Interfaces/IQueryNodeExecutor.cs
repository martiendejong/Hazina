using Hazina.LongContext.Models;

namespace Hazina.LongContext.Interfaces;

/// <summary>
/// Executes individual nodes in the query tree.
/// Different node types (Retrieval, Summarization, Aggregation) are handled differently.
/// </summary>
public interface IQueryNodeExecutor
{
    /// <summary>
    /// Execute a query node and return its result
    /// </summary>
    /// <param name="node">The node to execute</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Execution result including answer and used shards</returns>
    Task<QueryNodeResult> ExecuteNodeAsync(
        QueryNode node,
        CancellationToken ct = default);
}
