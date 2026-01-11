using Hazina.LongContext.Configuration;
using Hazina.LongContext.Interfaces;
using Hazina.LongContext.Models;
using System.Diagnostics;

namespace Hazina.LongContext.Strategies;

/// <summary>
/// Recursive strategy that decomposes queries into sub-queries and aggregates results.
/// Uses RecursiveQueryPlanner for intelligent question decomposition.
/// </summary>
public class RecursiveLongContextStrategy : ILongContextStrategy
{
    private readonly IQueryPlanner _planner;
    private readonly IQueryNodeExecutor _executor;
    private readonly LongContextOptions _options;

    public RecursiveLongContextStrategy(
        IQueryPlanner planner,
        IQueryNodeExecutor executor,
        LongContextOptions options)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<LongContextResult> ExecuteAsync(
        LongContextRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Plan query tree (will decompose if recursion enabled)
            var queryTree = await _planner.PlanAsync(request, ct);

            // Execute the query tree
            var rootResult = await _executor.ExecuteNodeAsync(queryTree, ct);

            sw.Stop();

            // Determine execution mode
            var executionMode = DetermineExecutionMode();

            // Build final result
            var result = LongContextResult.FromRootNode(rootResult, queryTree, executionMode);
            return new LongContextResult
            {
                FinalAnswer = result.FinalAnswer,
                QueryTree = result.QueryTree,
                UsedShards = result.UsedShards,
                TotalTokensUsed = result.TotalTokensUsed,
                Success = result.Success,
                Error = result.Error,
                Statistics = result.Statistics,
                SessionId = request.SessionId,
                TotalExecutionTime = sw.Elapsed,
                ExecutionMode = result.ExecutionMode
            };
        }
        catch (Exception ex)
        {
            sw.Stop();

            return new LongContextResult
            {
                Success = false,
                Error = $"Recursive query execution failed: {ex.Message}",
                TotalExecutionTime = sw.Elapsed,
                SessionId = request.SessionId,
                ExecutionMode = DetermineExecutionMode()
            };
        }
    }

    private ExecutionMode DetermineExecutionMode()
    {
        if (!_options.EnableParallelExecution)
            return ExecutionMode.Sequential;

        if (_options.MaxDegreeOfParallelism == 1)
            return ExecutionMode.Sequential;

        if (_options.MaxDegreeOfParallelism > 1)
            return ExecutionMode.LimitedParallel;

        return ExecutionMode.Parallel;
    }
}
