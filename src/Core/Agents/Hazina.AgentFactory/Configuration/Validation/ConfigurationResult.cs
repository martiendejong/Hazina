namespace Hazina.AgentFactory.Configuration.Validation;

/// <summary>
/// Result of configuration parsing and validation operations.
/// Separates parsing from construction to allow detailed diagnostics.
/// </summary>
public class ConfigurationResult<T>
{
    /// <summary>
    /// Indicates whether the configuration is valid and can be used safely.
    /// </summary>
    public bool IsValid => !Errors.Any();

    /// <summary>
    /// The parsed configuration object. May be null if parsing failed critically.
    /// </summary>
    public T? Value { get; set; }

    /// <summary>
    /// Collection of validation errors that prevent configuration from being used.
    /// </summary>
    public List<ValidationDiagnostic> Errors { get; set; } = new();

    /// <summary>
    /// Collection of validation warnings that don't prevent usage but indicate potential issues.
    /// </summary>
    public List<ValidationDiagnostic> Warnings { get; set; } = new();

    /// <summary>
    /// Collection of informational messages about the configuration.
    /// </summary>
    public List<ValidationDiagnostic> Infos { get; set; } = new();

    /// <summary>
    /// Creates a successful result with the parsed value.
    /// </summary>
    public static ConfigurationResult<T> Success(T value)
    {
        return new ConfigurationResult<T> { Value = value };
    }

    /// <summary>
    /// Creates a failed result with error diagnostics.
    /// </summary>
    public static ConfigurationResult<T> Failure(params ValidationDiagnostic[] errors)
    {
        return new ConfigurationResult<T>
        {
            Errors = errors.ToList()
        };
    }

    /// <summary>
    /// Creates a result with warnings but still usable value.
    /// </summary>
    public static ConfigurationResult<T> SuccessWithWarnings(T value, params ValidationDiagnostic[] warnings)
    {
        return new ConfigurationResult<T>
        {
            Value = value,
            Warnings = warnings.ToList()
        };
    }

    /// <summary>
    /// Adds an error diagnostic.
    /// </summary>
    public void AddError(string field, string message, string? code = null)
    {
        Errors.Add(new ValidationDiagnostic
        {
            Severity = DiagnosticSeverity.Error,
            Field = field,
            Message = message,
            Code = code
        });
    }

    /// <summary>
    /// Adds a warning diagnostic.
    /// </summary>
    public void AddWarning(string field, string message, string? code = null)
    {
        Warnings.Add(new ValidationDiagnostic
        {
            Severity = DiagnosticSeverity.Warning,
            Field = field,
            Message = message,
            Code = code
        });
    }

    /// <summary>
    /// Adds an informational diagnostic.
    /// </summary>
    public void AddInfo(string field, string message, string? code = null)
    {
        Infos.Add(new ValidationDiagnostic
        {
            Severity = DiagnosticSeverity.Info,
            Field = field,
            Message = message,
            Code = code
        });
    }

    /// <summary>
    /// Gets all diagnostics regardless of severity.
    /// </summary>
    public IEnumerable<ValidationDiagnostic> GetAllDiagnostics()
    {
        return Errors.Concat(Warnings).Concat(Infos).OrderBy(d => d.Field);
    }

    /// <summary>
    /// Formats all diagnostics as a human-readable string.
    /// </summary>
    public string FormatDiagnostics()
    {
        var lines = new List<string>();

        if (Errors.Any())
        {
            lines.Add("ERRORS:");
            foreach (var error in Errors)
            {
                lines.Add($"  [{error.Code ?? "ERROR"}] {error.Field}: {error.Message}");
            }
        }

        if (Warnings.Any())
        {
            lines.Add("WARNINGS:");
            foreach (var warning in Warnings)
            {
                lines.Add($"  [{warning.Code ?? "WARN"}] {warning.Field}: {warning.Message}");
            }
        }

        if (Infos.Any())
        {
            lines.Add("INFO:");
            foreach (var info in Infos)
            {
                lines.Add($"  [{info.Code ?? "INFO"}] {info.Field}: {info.Message}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
