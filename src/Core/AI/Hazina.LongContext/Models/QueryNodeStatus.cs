namespace Hazina.LongContext.Models;

/// <summary>
/// Execution status of a query node
/// </summary>
public enum QueryNodeStatus
{
    /// <summary>
    /// Node has been created but not yet executed
    /// </summary>
    Pending,

    /// <summary>
    /// Node is currently being executed
    /// </summary>
    InProgress,

    /// <summary>
    /// Node execution completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Node execution failed
    /// </summary>
    Failed
}
