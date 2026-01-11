using Hazina.LongContext.Configuration;
using Hazina.LongContext.Interfaces;
using Hazina.LongContext.Models;
using System.Diagnostics;

namespace Hazina.LongContext.Strategies;

/// <summary>
/// Single-shot strategy (non-recursive) for backwards compatibility.
/// Creates a simple retrieval plan and executes it once.
/// </summary>
public class SingleShotStrategy : ILongContextStrategy
{
    private readonly IQueryPlanner _planner;
    private readonly IQueryNodeExecutor _executor;
    private readonly LongContextOptions _options;

    public SingleShotStrategy(
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
            // Plan (single-shot plan = single retrieval node)
            var queryTree = await _planner.PlanAsync(request, ct);

            // Execute
            var rootResult = await _executor.ExecuteNodeAsync(queryTree, ct);

            sw.Stop();

            // Determine execution mode
            var executionMode = _options.EnableParallelExecution
                ? ExecutionMode.Parallel
                : ExecutionMode.Sequential;

            // Build result
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
                Error = ex.Message,
                TotalExecutionTime = sw.Elapsed,
                SessionId = request.SessionId
            };
        }
    }
}
