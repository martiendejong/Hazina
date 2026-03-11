using System.Diagnostics;
using Hazina.AI.CognitivePipeline.Stages;
using Microsoft.Extensions.Logging;

namespace Hazina.AI.CognitivePipeline.Core;

/// <summary>
/// Default implementation of the SCP cognitive pipeline.
/// Executes stages sequentially (S→O→L→F→B→M) with support for
/// GroundTruth short-circuiting and per-stage timeout/failure handling.
/// </summary>
public class CognitivePipeline : ICognitivePipeline
{
    private readonly IEnumerable<ISCPStage> _stages;
    private readonly ILogger<CognitivePipeline> _logger;

    public CognitivePipeline(
        IEnumerable<ISCPStage> stages,
        ILogger<CognitivePipeline> logger)
    {
        _stages = stages ?? throw new ArgumentNullException(nameof(stages));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<CognitivePipelineResult> ExecuteAsync(
        SCPContext context,
        CancellationToken cancellationToken = default)
    {
        var pipelineStopwatch = Stopwatch.StartNew();
        var result = new CognitivePipelineResult
        {
            ExecutionId = context.ExecutionId,
            TopicId = context.TopicId
        };

        var orderedStages = _stages.OrderBy(s => s.Order).ToList();
        _logger.LogInformation(
            "SCP Pipeline starting for topic {TopicId} with {StageCount} stages",
            context.TopicId, orderedStages.Count);

        try
        {
            using var pipelineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            pipelineCts.CancelAfter(context.Config.PipelineTimeoutMs);

            foreach (var stage in orderedStages)
            {
                if (context.GroundTruthShortCircuit && stage.StageType is not (SCPStageType.Decision or SCPStageType.Memory))
                {
                    var skipped = new SCPStageResult
                    {
                        StageType = stage.StageType,
                        Skipped = true,
                        SkipReason = "GroundTruth short-circuit active"
                    };
                    context.StageResults[stage.StageType] = skipped;
                    result.StageResults.Add(skipped);
                    result.LlmCallsAvoided++;
                    _logger.LogInformation("Stage {StageName} skipped (GroundTruth short-circuit)", stage.Name);
                    continue;
                }

                var stageResult = await ExecuteStageAsync(stage, context, pipelineCts.Token);
                context.StageResults[stage.StageType] = stageResult;
                result.StageResults.Add(stageResult);

                if (!stageResult.Success && !context.Config.ContinueOnStageFailure)
                {
                    _logger.LogWarning("Stage {StageName} failed and ContinueOnStageFailure is disabled. Aborting pipeline.", stage.Name);
                    result.Success = false;
                    result.Error = $"Stage {stage.Name} failed: {stageResult.Error}";
                    break;
                }
            }

            // Aggregate metrics
            result.Findings = context.Findings.ToList();
            result.Warnings = context.Warnings.ToList();
            result.TotalLlmCalls = result.StageResults.Sum(s => s.LlmCallCount);
            result.TotalEstimatedCost = result.StageResults.Sum(s => s.EstimatedCost);
            result.ShortCircuited = context.GroundTruthShortCircuit;

            // Final confidence from Decision stage, or average of completed stages
            var decisionResult = result.StageResults
                .FirstOrDefault(s => s.StageType == SCPStageType.Decision && s.Success && !s.Skipped);
            result.FinalConfidence = decisionResult?.Confidence
                ?? result.StageResults.Where(s => s.Success && !s.Skipped).Select(s => s.Confidence).DefaultIfEmpty(0).Average();

            // GroundTruth metrics
            var totalQueries = context.GroundTruthHits.Count;
            var hits = context.GroundTruthHits.Count(h => h.Value.Found);
            result.GroundTruthHitRate = totalQueries > 0 ? (double)hits / totalQueries : 0;

            // Noise reduction (compare raw vs filtered data counts if available)
            var rawCount = context.RawData.Count;
            var filteredCount = context.FilteredData.Count;
            result.NoiseReductionRate = rawCount > 0 ? 1.0 - ((double)filteredCount / rawCount) : 0;

            result.Success = result.Error == null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result.Success = false;
            result.Error = $"Pipeline timed out after {context.Config.PipelineTimeoutMs}ms";
            _logger.LogWarning("SCP Pipeline timed out for topic {TopicId}", context.TopicId);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            _logger.LogError(ex, "SCP Pipeline failed for topic {TopicId}", context.TopicId);
        }

        pipelineStopwatch.Stop();
        result.TotalDurationMs = pipelineStopwatch.ElapsedMilliseconds;

        _logger.LogInformation(
            "SCP Pipeline completed for topic {TopicId}: Confidence={Confidence:F2}, Duration={Duration}ms, LLMCalls={LlmCalls}, ShortCircuit={ShortCircuit}",
            context.TopicId, result.FinalConfidence, result.TotalDurationMs, result.TotalLlmCalls, result.ShortCircuited);

        return result;
    }

    private async Task<SCPStageResult> ExecuteStageAsync(
        ISCPStage stage,
        SCPContext context,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Executing stage {StageName} (Order={Order})", stage.Name, stage.Order);

        try
        {
            using var stageCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            stageCts.CancelAfter(context.Config.StageTimeoutMs);

            var stageResult = await stage.ExecuteAsync(context, stageCts.Token);
            sw.Stop();
            stageResult.DurationMs = sw.ElapsedMilliseconds;

            // Propagate findings/warnings to context
            context.Findings.AddRange(stageResult.Findings);
            context.Warnings.AddRange(stageResult.Warnings);

            _logger.LogInformation(
                "Stage {StageName} completed: Confidence={Confidence:F2}, Duration={Duration}ms",
                stage.Name, stageResult.Confidence, stageResult.DurationMs);

            return stageResult;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            _logger.LogWarning("Stage {StageName} timed out after {Timeout}ms", stage.Name, context.Config.StageTimeoutMs);
            return new SCPStageResult
            {
                StageType = stage.StageType,
                Success = false,
                Error = $"Stage timed out after {context.Config.StageTimeoutMs}ms",
                DurationMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Stage {StageName} failed", stage.Name);
            return new SCPStageResult
            {
                StageType = stage.StageType,
                Success = false,
                Error = ex.Message,
                DurationMs = sw.ElapsedMilliseconds
            };
        }
    }
}
