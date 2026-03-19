namespace Hazina.AgentFactory.Configuration.Validation;

/// <summary>
/// Represents a diagnostic message from configuration validation.
/// </summary>
public class ValidationDiagnostic
{
    /// <summary>
    /// Severity level of the diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; set; }

    /// <summary>
    /// Field or section that the diagnostic relates to.
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable diagnostic message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional diagnostic code for programmatic handling.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Optional line number in source file (for parser diagnostics).
    /// </summary>
    public int? LineNumber { get; set; }

    /// <summary>
    /// Optional column number in source file (for parser diagnostics).
    /// </summary>
    public int? ColumnNumber { get; set; }

    public override string ToString()
    {
        var location = LineNumber.HasValue ? $"(Line {LineNumber}, Col {ColumnNumber}) " : "";
        return $"[{Severity}] {location}{Field}: {Message}" + (Code != null ? $" ({Code})" : "");
    }
}

/// <summary>
/// Severity levels for validation diagnostics.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Informational message.
    /// </summary>
    Info,

    /// <summary>
    /// Warning that doesn't prevent usage but indicates potential issues.
    /// </summary>
    Warning,

    /// <summary>
    /// Error that prevents configuration from being used safely.
    /// </summary>
    Error
}
