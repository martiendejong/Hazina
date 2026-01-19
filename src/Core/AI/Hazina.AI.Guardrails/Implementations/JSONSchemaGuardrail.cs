using System.Text.Json;

namespace Hazina.AI.Guardrails.Implementations;

/// <summary>
/// Guardrail that validates JSON output structure
/// Currently performs basic JSON validation
/// TODO: Add JSON Schema validation support
/// </summary>
public class JSONSchemaGuardrail : IGuardrail
{
    public string Name => "json-schema";
    public GuardrailStage Stage => GuardrailStage.PostExecution;

    public Task<GuardrailResult> ValidateAsync(
        string content,
        GuardrailContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic JSON validation
            using var doc = JsonDocument.Parse(content);

            // TODO (Phase 2): Add actual JSON Schema validation
            // For now, just verify it's valid JSON

            return Task.FromResult(new GuardrailResult
            {
                Passed = true,
                Metadata = new Dictionary<string, object>
                {
                    ["jsonValid"] = true,
                    ["rootElementType"] = doc.RootElement.ValueKind.ToString()
                }
            });
        }
        catch (JsonException ex)
        {
            return Task.FromResult(new GuardrailResult
            {
                Passed = false,
                FailureReason = $"Invalid JSON: {ex.Message}",
                Metadata = new Dictionary<string, object>
                {
                    ["jsonValid"] = false,
                    ["errorMessage"] = ex.Message,
                    ["errorLine"] = ex.LineNumber ?? 0,
                    ["errorPosition"] = ex.BytePositionInLine ?? 0
                }
            });
        }
    }
}
