namespace Hazina.AI.Guardrails.Implementations;

/// <summary>
/// Guardrail that enforces maximum token limits on outputs
/// Prevents excessively long responses
/// </summary>
public class TokenLimitGuardrail : IGuardrail
{
    public string Name => "token-limit";
    public GuardrailStage Stage => GuardrailStage.PostExecution;

    private const int DEFAULT_MAX_TOKENS = 2000;

    public Task<GuardrailResult> ValidateAsync(
        string content,
        GuardrailContext context,
        CancellationToken cancellationToken = default)
    {
        // Get max tokens from context parameters or use default
        var maxTokens = context.Parameters.TryGetValue("maxTokens", out var max)
            ? Convert.ToInt32(max)
            : DEFAULT_MAX_TOKENS;

        // Rough token estimation (~4 characters per token)
        var estimatedTokens = content.Length / 4;

        if (estimatedTokens > maxTokens)
        {
            return Task.FromResult(new GuardrailResult
            {
                Passed = false,
                FailureReason = $"Output exceeds token limit: {estimatedTokens} tokens > {maxTokens} tokens limit",
                Metadata = new Dictionary<string, object>
                {
                    ["estimatedTokens"] = estimatedTokens,
                    ["maxTokens"] = maxTokens,
                    ["contentLength"] = content.Length
                }
            });
        }

        return Task.FromResult(new GuardrailResult
        {
            Passed = true,
            Metadata = new Dictionary<string, object>
            {
                ["estimatedTokens"] = estimatedTokens,
                ["maxTokens"] = maxTokens
            }
        });
    }
}
