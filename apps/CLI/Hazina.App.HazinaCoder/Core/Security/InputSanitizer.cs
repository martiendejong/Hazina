using System.Text.RegularExpressions;
using System.Security;

namespace Hazina.App.HazinaCoder.Core.Security;

/// <summary>
/// Input sanitization for XSS, injection attacks
/// Iteration 34: Security hardening
/// </summary>
public class InputSanitizer
{
    private static readonly Regex SqlInjectionPattern = new(@"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE)\b)|([';])", RegexOptions.IgnoreCase);
    private static readonly Regex CommandInjectionPattern = new(@"[;&|`$()]", RegexOptions.None);
    private static readonly Regex PathTraversalPattern = new(@"\.\.|[<>:""/\\|?*]", RegexOptions.None);

    public string SanitizeForSql(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        if (SqlInjectionPattern.IsMatch(input))
            throw new SecurityException("Potential SQL injection detected");

        return input.Replace("'", "''");
    }

    public string SanitizeForCommand(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        if (CommandInjectionPattern.IsMatch(input))
            throw new SecurityException("Potential command injection detected");

        return input;
    }

    public string SanitizeFilePath(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        if (PathTraversalPattern.IsMatch(input))
            throw new SecurityException("Potential path traversal detected");

        return Path.GetFullPath(input);
    }

    public string SanitizeForHtml(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }

    public bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// Security audit logger
/// Iteration 35: Audit logging
/// </summary>
public class SecurityAuditLogger
{
    private readonly string _logPath;

    public SecurityAuditLogger(string logPath = ".hazinacoder/security.log")
    {
        _logPath = logPath;
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
    }

    public void LogSecurityEvent(string eventType, string description, string? user = null)
    {
        var entry = $"{DateTime.UtcNow:O}|{eventType}|{user ?? "system"}|{description}";
        File.AppendAllLines(_logPath, new[] { entry });
    }

    public void LogFailedAuthentication(string user, string reason)
    {
        LogSecurityEvent("AUTH_FAILED", $"User: {user}, Reason: {reason}", user);
    }

    public void LogSecretAccess(string secretType, string operation)
    {
        LogSecurityEvent("SECRET_ACCESS", $"Type: {secretType}, Op: {operation}");
    }

    public void LogSuspiciousActivity(string activity, string details)
    {
        LogSecurityEvent("SUSPICIOUS", $"{activity}: {details}");
    }
}
