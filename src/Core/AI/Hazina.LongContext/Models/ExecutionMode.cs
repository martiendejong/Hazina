namespace Hazina.LongContext.Models;

/// <summary>
/// Execution mode for query nodes
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// Sequential execution (one node at a time)
    /// </summary>
    Sequential,

    /// <summary>
    /// Parallel execution (multiple nodes concurrently)
    /// </summary>
    Parallel,

    /// <summary>
    /// Limited parallel execution (controlled degree of parallelism)
    /// </summary>
    LimitedParallel
}
