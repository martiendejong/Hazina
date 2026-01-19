using Microsoft.Extensions.Logging;

namespace Hazina.AI.Guardrails;

/// <summary>
/// Interface for executing multiple guardrails in sequence
/// </summary>
public interface IGuardrailPipeline
{
    /// <summary>
    /// Execute guardrails for the given content and stage
    /// </summary>
    Task<GuardrailResult> ExecuteAsync(
        string content,
        List<string> guardrailNames,
        GuardrailStage stage,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Pipeline for executing multiple guardrails in sequence
/// Stops at first failure
/// </summary>
public class GuardrailPipeline : IGuardrailPipeline
{
    private readonly Dictionary<string, IGuardrail> _guardrails;
    private readonly ILogger<GuardrailPipeline> _logger;

    public GuardrailPipeline(
        IEnumerable<IGuardrail> guardrails,
        ILogger<GuardrailPipeline> logger)
    {
        _guardrails = guardrails.ToDictionary(g => g.Name, StringComparer.OrdinalIgnoreCase);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute all specified guardrails for the given stage
    /// Returns first failure or success if all pass
    /// </summary>
    public async Task<GuardrailResult> ExecuteAsync(
        string content,
        List<string> guardrailNames,
        GuardrailStage stage,
        CancellationToken cancellationToken = default)
    {
        if (guardrailNames == null || !guardrailNames.Any())
        {
            return new GuardrailResult { Passed = true };
        }

        foreach (var name in guardrailNames)
        {
            if (!_guardrails.TryGetValue(name, out var guardrail))
            {
                _logger.LogWarning("Guardrail not found: {GuardrailName}. Skipping.", name);
                continue;
            }

            // Skip if guardrail doesn't apply to this stage
            if (guardrail.Stage != stage)
            {
                _logger.LogDebug("Guardrail {GuardrailName} skipped (stage mismatch: {GuardrailStage} != {ExecutionStage})",
                    name, guardrail.Stage, stage);
                continue;
            }

            _logger.LogDebug("Executing guardrail: {GuardrailName} at {Stage}", name, stage);

            var context = new GuardrailContext();
            var result = await guardrail.ValidateAsync(content, context, cancellationToken);

            if (!result.Passed)
            {
                _logger.LogWarning("Guardrail {GuardrailName} failed: {Reason}", name, result.FailureReason);
                return result;
            }

            _logger.LogDebug("Guardrail {GuardrailName} passed", name);
        }

        return new GuardrailResult { Passed = true };
    }
}
