using System.ComponentModel.DataAnnotations;

namespace Hazina.Security.Core.Validation;

/// <summary>
/// Interface for input validation to prevent injection attacks and ensure data integrity
/// </summary>
public interface IInputValidator
{
    /// <summary>
    /// Validates input string against common injection patterns
    /// </summary>
    /// <param name="input">Input to validate</param>
    /// <param name="validationType">Type of validation to perform</param>
    /// <returns>Validation result</returns>
    ValidationResult ValidateInput(string? input, InputValidationType validationType = InputValidationType.General);

    /// <summary>
    /// Sanitizes input by removing/escaping potentially dangerous characters
    /// </summary>
    /// <param name="input">Input to sanitize</param>
    /// <param name="sanitizationType">Type of sanitization to perform</param>
    /// <returns>Sanitized input</returns>
    string SanitizeInput(string? input, SanitizationType sanitizationType = SanitizationType.General);

    /// <summary>
    /// Validates an object using data annotations
    /// </summary>
    /// <param name="obj">Object to validate</param>
    /// <returns>Collection of validation results</returns>
    IEnumerable<ValidationResult> ValidateObject(object obj);

    /// <summary>
    /// Validates that input is safe SQL (prevents SQL injection)
    /// </summary>
    bool IsSafeSql(string? input);

    /// <summary>
    /// Validates that input is safe for command execution
    /// </summary>
    bool IsSafeCommand(string? input);

    /// <summary>
    /// Validates that input is safe HTML (prevents XSS)
    /// </summary>
    bool IsSafeHtml(string? input);

    /// <summary>
    /// Validates file path is safe and within allowed directories
    /// </summary>
    bool IsSafeFilePath(string? path, string? basePath = null);
}

/// <summary>
/// Types of input validation
/// </summary>
public enum InputValidationType
{
    General,
    Sql,
    Command,
    Html,
    FilePath,
    Email,
    Url,
    Json,
    Xml
}

/// <summary>
/// Types of input sanitization
/// </summary>
public enum SanitizationType
{
    General,
    Html,
    Sql,
    Command,
    FilePath,
    Json
}
