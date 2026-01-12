using Hazina.LongContext.Models;

namespace Hazina.LongContext.Interfaces;

/// <summary>
/// Plans query execution by decomposing queries into a tree of sub-queries.
/// </summary>
public interface IQueryPlanner
{
    /// <summary>
    /// Create an execution plan for a long-context query
    /// </summary>
    /// <param name="request">The query request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Root node of the query tree</returns>
    Task<QueryNode> PlanAsync(
        LongContextRequest request,
        CancellationToken ct = default);
}
