using System.Text.RegularExpressions;

namespace Hazina.AI.Guardrails.Implementations;

/// <summary>
/// Guardrail that detects personally identifiable information (PII) in content
/// Checks for: SSN, email, phone, credit card
/// </summary>
public class PIIDetectionGuardrail : IGuardrail
{
    public string Name => "no-pii";
    public GuardrailStage Stage => GuardrailStage.PostExecution;

    private static readonly Regex[] PII_PATTERNS = new[]
    {
        new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled),           // SSN
        new Regex(@"\b[\w\.-]+@[\w\.-]+\.\w{2,4}\b", RegexOptions.Compiled),  // Email
        new Regex(@"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b", RegexOptions.Compiled),   // Phone
        new Regex(@"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", RegexOptions.Compiled) // Credit card
    };

    private static readonly string[] PII_PATTERN_NAMES = new[]
    {
        "SSN",
        "Email",
        "Phone",
        "Credit Card"
    };

    public Task<GuardrailResult> ValidateAsync(
        string content,
        GuardrailContext context,
        CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < PII_PATTERNS.Length; i++)
        {
            var pattern = PII_PATTERNS[i];
            var match = pattern.Match(content);

            if (match.Success)
            {
                var preview = match.Value.Length > 10
                    ? match.Value.Substring(0, 10) + "..."
                    : match.Value;

                return Task.FromResult(new GuardrailResult
                {
                    Passed = false,
                    FailureReason = $"Potential PII detected ({PII_PATTERN_NAMES[i]}): {preview}",
                    Metadata = new Dictionary<string, object>
                    {
                        ["piiType"] = PII_PATTERN_NAMES[i],
                        ["matchPosition"] = match.Index
                    }
                });
            }
        }

        return Task.FromResult(new GuardrailResult { Passed = true });
    }
}
