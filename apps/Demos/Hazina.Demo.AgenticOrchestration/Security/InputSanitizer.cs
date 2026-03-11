using System.Text.RegularExpressions;

namespace Hazina.Demo.AgenticOrchestration.Security;

/// <summary>
/// Input sanitization utilities to prevent command injection and other security vulnerabilities.
/// </summary>
public static class InputSanitizer
{
    private static readonly Regex SessionIdPattern = new(@"^[a-zA-Z0-9\-]{1,100}$", RegexOptions.Compiled);
    private static readonly Regex SafeCommandPattern = new(@"^[a-zA-Z0-9\s\-_./:=@\[\]\\]+$", RegexOptions.Compiled);

    // Characters that should never appear in session IDs or commands
    private static readonly char[] DangerousChars = { ';', '|', '&', '`', '$', '(', ')', '<', '>', '\n', '\r', '\0' };

    /// <summary>
    /// Validates and sanitizes a session ID.
    /// </summary>
    /// <param name="sessionId">The session ID to validate</param>
    /// <param name="sanitized">The sanitized session ID</param>
    /// <returns>True if valid and sanitized successfully</returns>
    public static bool TryValidateSessionId(string? sessionId, out string sanitized)
    {
        sanitized = string.Empty;

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        // Trim and limit length
        var trimmed = sessionId.Trim();
        if (trimmed.Length > 100)
        {
            return false;
        }

        // Check for dangerous characters
        if (trimmed.IndexOfAny(DangerousChars) >= 0)
        {
            return false;
        }

        // Validate pattern (alphanumeric and hyphens only)
        if (!SessionIdPattern.IsMatch(trimmed))
        {
            return false;
        }

        sanitized = trimmed;
        return true;
    }

    /// <summary>
    /// Sanitizes a command string for terminal execution.
    /// IMPORTANT: This does NOT make arbitrary user input safe for shell execution.
    /// Use only for validated, limited command sets.
    /// </summary>
    /// <param name="command">The command to sanitize</param>
    /// <param name="maxLength">Maximum allowed command length</param>
    /// <param name="sanitized">The sanitized command</param>
    /// <returns>True if sanitization succeeded</returns>
    public static bool TrySanitizeCommand(string? command, int maxLength, out string sanitized)
    {
        sanitized = string.Empty;

        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmed = command.Trim();

        // Enforce length limit
        if (trimmed.Length > maxLength)
        {
            return false;
        }

        // Remove control characters except newlines (which will be handled separately)
        var cleaned = Regex.Replace(trimmed, @"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]", "");

        // Check for command injection attempts
        if (cleaned.IndexOfAny(DangerousChars) >= 0)
        {
            return false;
        }

        // Additional validation for safe characters only
        if (!SafeCommandPattern.IsMatch(cleaned))
        {
            return false;
        }

        sanitized = cleaned;
        return true;
    }

    /// <summary>
    /// Sanitizes a general string parameter (for archive IDs, titles, etc.)
    /// </summary>
    /// <param name="input">The input string</param>
    /// <param name="maxLength">Maximum allowed length</param>
    /// <param name="sanitized">The sanitized output</param>
    /// <returns>True if sanitization succeeded</returns>
    public static bool TrySanitizeString(string? input, int maxLength, out string sanitized)
    {
        sanitized = string.Empty;

        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var trimmed = input.Trim();

        // Enforce length limit
        if (trimmed.Length > maxLength)
        {
            trimmed = trimmed.Substring(0, maxLength);
        }

        // Remove null bytes and control characters
        sanitized = Regex.Replace(trimmed, @"[\x00-\x1F\x7F]", "");

        return !string.IsNullOrWhiteSpace(sanitized);
    }

    /// <summary>
    /// Validates an integer parameter is within safe bounds.
    /// </summary>
    public static bool TryValidateInteger(string? value, int min, int max, out int result)
    {
        result = 0;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        if (!int.TryParse(value, out result))
        {
            return false;
        }

        return result >= min && result <= max;
    }

    /// <summary>
    /// Escapes a string for safe logging (prevents log injection).
    /// </summary>
    public static string EscapeForLogging(string? input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        // Replace newlines and carriage returns to prevent log injection
        return input
            .Replace("\r", "\\r")
            .Replace("\n", "\\n")
            .Replace("\t", "\\t");
    }
}
