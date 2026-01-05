using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using System.Web;

namespace Hazina.Security.Core.Validation;

/// <summary>
/// Input validator to prevent injection attacks and ensure data integrity
/// </summary>
public partial class InputValidator : IInputValidator
{
    private readonly ILogger<InputValidator> _logger;
    private readonly InputValidatorOptions _options;

    // Dangerous patterns
    private static readonly string[] SqlInjectionPatterns = new[]
    {
        @"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE)\b)",
        @"(--|;|\/\*|\*\/|xp_|sp_)",
        @"(\bUNION\b.*\bSELECT\b)",
        @"(\bOR\b.*=.*)",
        @"('|"")\s*(OR|AND)\s*\1\s*=\s*\1"
    };

    private static readonly string[] CommandInjectionPatterns = new[]
    {
        @"[;&|`$()]",
        @"\.\./",
        @"\\\.\\",
        @"(>|<|>>|<<)",
        @"(\bnc\b|\bnetcat\b|\bcurl\b|\bwget\b|\bpowershell\b|\bcmd\b|\bbash\b|\bsh\b)"
    };

    private static readonly string[] XssPatterns = new[]
    {
        @"<script[^>]*>.*?</script>",
        @"javascript:",
        @"on\w+\s*=",
        @"<iframe",
        @"<object",
        @"<embed",
        @"<applet"
    };

    public InputValidator(ILogger<InputValidator> logger, InputValidatorOptions? options = null)
    {
        _logger = logger;
        _options = options ?? new InputValidatorOptions();
    }

    public ValidationResult ValidateInput(string? input, InputValidationType validationType = InputValidationType.General)
    {
        if (string.IsNullOrEmpty(input))
        {
            return validationType == InputValidationType.General
                ? ValidationResult.Success!
                : new ValidationResult("Input cannot be empty");
        }

        // Length check
        if (input.Length > _options.MaxInputLength)
        {
            return new ValidationResult($"Input exceeds maximum length of {_options.MaxInputLength} characters");
        }

        try
        {
            return validationType switch
            {
                InputValidationType.Sql => IsSafeSql(input)
                    ? ValidationResult.Success!
                    : new ValidationResult("Input contains potentially dangerous SQL patterns"),

                InputValidationType.Command => IsSafeCommand(input)
                    ? ValidationResult.Success!
                    : new ValidationResult("Input contains potentially dangerous command patterns"),

                InputValidationType.Html => IsSafeHtml(input)
                    ? ValidationResult.Success!
                    : new ValidationResult("Input contains potentially dangerous HTML/script patterns"),

                InputValidationType.FilePath => IsSafeFilePath(input)
                    ? ValidationResult.Success!
                    : new ValidationResult("File path contains potentially dangerous patterns"),

                InputValidationType.Email => EmailRegex().IsMatch(input)
                    ? ValidationResult.Success!
                    : new ValidationResult("Invalid email format"),

                InputValidationType.Url => Uri.TryCreate(input, UriKind.Absolute, out _)
                    ? ValidationResult.Success!
                    : new ValidationResult("Invalid URL format"),

                _ => ValidationResult.Success!
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[INPUT VALIDATOR] Validation failed for type: {Type}", validationType);
            return new ValidationResult("Validation failed due to an error");
        }
    }

    public string SanitizeInput(string? input, SanitizationType sanitizationType = SanitizationType.General)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        try
        {
            return sanitizationType switch
            {
                SanitizationType.Html => HttpUtility.HtmlEncode(input),
                SanitizationType.Sql => input.Replace("'", "''").Replace(";", ""),
                SanitizationType.Command => Regex.Replace(input, @"[;&|`$()<>]", ""),
                SanitizationType.FilePath => SanitizeFilePath(input),
                SanitizationType.Json => input.Replace("\"", "\\\"").Replace("\\", "\\\\"),
                _ => input.Trim()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[INPUT VALIDATOR] Sanitization failed for type: {Type}", sanitizationType);
            return string.Empty;
        }
    }

    public IEnumerable<ValidationResult> ValidateObject(object obj)
    {
        var context = new ValidationContext(obj);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(obj, context, results, validateAllProperties: true);
        return results;
    }

    public bool IsSafeSql(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return true;

        foreach (var pattern in SqlInjectionPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("[INPUT VALIDATOR] Potential SQL injection detected: {Pattern}", pattern);
                return false;
            }
        }

        return true;
    }

    public bool IsSafeCommand(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return true;

        foreach (var pattern in CommandInjectionPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("[INPUT VALIDATOR] Potential command injection detected: {Pattern}", pattern);
                return false;
            }
        }

        return true;
    }

    public bool IsSafeHtml(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return true;

        foreach (var pattern in XssPatterns)
        {
            if (Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase))
            {
                _logger.LogWarning("[INPUT VALIDATOR] Potential XSS detected: {Pattern}", pattern);
                return false;
            }
        }

        return true;
    }

    public bool IsSafeFilePath(string? path, string? basePath = null)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        try
        {
            // Normalize path
            var normalizedPath = Path.GetFullPath(path);

            // Check for directory traversal
            if (normalizedPath.Contains("..") || normalizedPath.Contains("~"))
            {
                _logger.LogWarning("[INPUT VALIDATOR] Directory traversal detected in path: {Path}", path);
                return false;
            }

            // If base path is provided, ensure file is within it
            if (!string.IsNullOrEmpty(basePath))
            {
                var normalizedBase = Path.GetFullPath(basePath);
                if (!normalizedPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("[INPUT VALIDATOR] Path outside base directory: {Path}", path);
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[INPUT VALIDATOR] File path validation failed: {Path}", path);
            return false;
        }
    }

    private static string SanitizeFilePath(string input)
    {
        // Remove dangerous characters
        var invalidChars = Path.GetInvalidFileNameChars();
        return new string(input.Where(ch => !invalidChars.Contains(ch)).ToArray());
    }

    [GeneratedRegex(@"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailRegex();
}

/// <summary>
/// Configuration options for InputValidator
/// </summary>
public class InputValidatorOptions
{
    /// <summary>
    /// Maximum length for input strings
    /// </summary>
    public int MaxInputLength { get; set; } = 1048576; // 1MB

    /// <summary>
    /// Whether to log validation failures
    /// </summary>
    public bool LogValidationFailures { get; set; } = true;

    /// <summary>
    /// Whether to throw exceptions on validation failure
    /// </summary>
    public bool ThrowOnValidationFailure { get; set; } = false;
}
