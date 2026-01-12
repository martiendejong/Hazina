using Hazina.LongContext.Models;

namespace Hazina.LongContext.Interfaces;

/// <summary>
/// Strategy for executing long-context queries.
/// Can be implemented as single-shot (backwards compatible) or recursive.
/// </summary>
public interface ILongContextStrategy
{
    /// <summary>
    /// Execute a long-context query
    /// </summary>
    /// <param name="request">The query request with configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Query result with answer and execution details</returns>
    Task<LongContextResult> ExecuteAsync(
        LongContextRequest request,
        CancellationToken ct = default);
}
