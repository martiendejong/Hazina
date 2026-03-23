using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Hazina.Observability.Core.Logging;

/// <summary>
/// Provides utilities for redacting sensitive data from logs
/// </summary>
public static class SensitiveDataRedactor
{
    private static readonly Regex ApiKeyPattern = new(@"(api[_-]?key|apikey|token|bearer)\s*[:=]\s*['""""]?([a-zA-Z0-9_\-]{20,})['""""]?", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", RegexOptions.Compiled);
    private static readonly Regex CreditCardPattern = new(@"\b\d{4}[\s-]?\d{4}[\s-]?\d{4}[\s-]?\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex SsnPattern = new(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"\b\d{3}[-.]?\d{3}[-.]?\d{4}\b", RegexOptions.Compiled);
    private static readonly Regex IpPattern = new(@"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", RegexOptions.Compiled);

    /// <summary>
    /// Redacts all known sensitive data patterns from text
    /// </summary>
    public static string RedactSensitiveData(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var redacted = text;

        // Redact API keys and tokens
        redacted = ApiKeyPattern.Replace(redacted, m => $"{m.Groups[1].Value}=[REDACTED]");

        // Redact email addresses
        redacted = EmailPattern.Replace(redacted, "[EMAIL]");

        // Redact credit card numbers
        redacted = CreditCardPattern.Replace(redacted, "[CARD]");

        // Redact SSNs
        redacted = SsnPattern.Replace(redacted, "[SSN]");

        // Redact phone numbers
        redacted = PhonePattern.Replace(redacted, "[PHONE]");

        // Redact IP addresses
        redacted = IpPattern.Replace(redacted, "[IP]");

        return redacted;
    }

    /// <summary>
    /// Redacts specific field values from a dictionary
    /// </summary>
    public static Dictionary<string, object?> RedactFields(Dictionary<string, object?> data, HashSet<string> sensitiveFields)
    {
        var redacted = new Dictionary<string, object?>(data.Count);

        foreach (var kvp in data)
        {
            if (sensitiveFields.Contains(kvp.Key.ToLowerInvariant()))
            {
                redacted[kvp.Key] = "[REDACTED]";
            }
            else if (kvp.Value is string strValue)
            {
                redacted[kvp.Key] = RedactSensitiveData(strValue);
            }
            else
            {
                redacted[kvp.Key] = kvp.Value;
            }
        }

        return redacted;
    }

    /// <summary>
    /// Default set of field names that should be redacted
    /// </summary>
    public static readonly HashSet<string> DefaultSensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "apikey",
        "api_key",
        "token",
        "secret",
        "authorization",
        "bearer",
        "creditcard",
        "credit_card",
        "ssn",
        "social_security",
        "private_key",
        "privatekey"
    };

    /// <summary>
    /// Redacts LLM prompt/response for safe logging
    /// </summary>
    public static string RedactForLogging(string? text, int maxLength = 500)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        var redacted = RedactSensitiveData(text);

        // Truncate if too long
        if (redacted.Length > maxLength)
        {
            redacted = redacted.Substring(0, maxLength) + $"... [truncated {redacted.Length - maxLength} chars]";
        }

        return redacted;
    }
}
