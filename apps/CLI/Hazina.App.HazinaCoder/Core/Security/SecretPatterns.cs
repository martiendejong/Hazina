using System.Text.RegularExpressions;

namespace Hazina.App.HazinaCoder.Core.Security;

/// <summary>
/// Pattern definitions for detecting secrets in code
/// </summary>
public static class SecretPatterns
{
    public record SecretPattern(
        string Name,
        string Description,
        Regex Pattern,
        SecretSeverity Severity,
        string Remediation
    );

    public enum SecretSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// All secret patterns to check against
    /// </summary>
    public static readonly List<SecretPattern> AllPatterns = new()
    {
        // AWS Credentials
        new SecretPattern(
            Name: "AWS Access Key ID",
            Description: "AWS Access Key ID detected",
            Pattern: new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled),
            Severity: SecretSeverity.Critical,
            Remediation: "Use AWS IAM roles or environment variables. Never commit AWS keys."
        ),
        new SecretPattern(
            Name: "AWS Secret Access Key",
            Description: "AWS Secret Access Key detected",
            Pattern: new Regex(@"aws_secret_access_key\s*=\s*['\"]([A-Za-z0-9/+=]{40})['\"]", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            Severity: SecretSeverity.Critical,
            Remediation: "Use AWS Secrets Manager or environment variables."
        ),

        // GitHub Tokens
        new SecretPattern(
            Name: "GitHub Personal Access Token",
            Description: "GitHub PAT detected",
            Pattern: new Regex(@"ghp_[0-9a-zA-Z]{36}", RegexOptions.Compiled),
            Severity: SecretSeverity.Critical,
            Remediation: "Revoke token at github.com/settings/tokens. Use GitHub App authentication."
        ),
        new SecretPattern(
            Name: "GitHub OAuth Token",
            Description: "GitHub OAuth token detected",
            Pattern: new Regex(@"gho_[0-9a-zA-Z]{36}", RegexOptions.Compiled),
            Severity: SecretSeverity.Critical,
            Remediation: "Revoke token and rotate credentials."
        ),
        new SecretPattern(
            Name: "GitHub App Token",
            Description: "GitHub App token detected",
            Pattern: new Regex(@"(ghu|ghs)_[0-9a-zA-Z]{36}", RegexOptions.Compiled),
            Severity: SecretSeverity.Critical,
            Remediation: "Revoke token and use secure credential storage."
        ),

        // Slack Tokens
        new SecretPattern(
            Name: "Slack Bot Token",
            Description: "Slack bot token detected",
            Pattern: new Regex(@"xoxb-[0-9]{10,13}-[0-9]{10,13}-[0-9a-zA-Z]{24}", RegexOptions.Compiled),
            Severity: SecretSeverity.High,
            Remediation: "Revoke at api.slack.com. Use environment variables."
        ),
        new SecretPattern(
            Name: "Slack Webhook",
            Description: "Slack webhook URL detected",
            Pattern: new Regex(@"https://hooks\.slack\.com/services/T[a-zA-Z0-9_]+/B[a-zA-Z0-9_]+/[a-zA-Z0-9_]+", RegexOptions.Compiled),
            Severity: SecretSeverity.High,
            Remediation: "Regenerate webhook URL and store securely."
        ),

        // OpenAI / Anthropic
        new SecretPattern(
            Name: "OpenAI API Key",
            Description: "OpenAI API key detected",
            Pattern: new Regex(@"sk-[a-zA-Z0-9]{48}", RegexOptions.Compiled),
            Severity: SecretSeverity.Critical,
            Remediation: "Revoke at platform.openai.com/api-keys. Use environment variables."
        ),
        new SecretPattern(
            Name: "Anthropic API Key",
            Description: "Anthropic API key detected",
            Pattern: new Regex(@"sk-ant-api03-[a-zA-Z0-9-_]{95}", RegexOptions.Compiled),
            Severity: SecretSeverity.Critical,
            Remediation: "Revoke at console.anthropic.com. Use environment variables."
        ),

        // Generic Patterns
        new SecretPattern(
            Name: "Generic API Key",
            Description: "Generic API key pattern detected",
            Pattern: new Regex(@"[Aa]pi[_-]?[Kk]ey\s*[:=]\s*['\"]([0-9a-zA-Z_\-]{32,})['\"]", RegexOptions.Compiled),
            Severity: SecretSeverity.High,
            Remediation: "Use environment variables or secure credential storage."
        ),
        new SecretPattern(
            Name: "Generic Secret",
            Description: "Generic secret pattern detected",
            Pattern: new Regex(@"[Ss]ecret\s*[:=]\s*['\"]([0-9a-zA-Z_\-]{16,})['\"]", RegexOptions.Compiled),
            Severity: SecretSeverity.Medium,
            Remediation: "Use environment variables or configuration files excluded from git."
        ),
        new SecretPattern(
            Name: "Password in Code",
            Description: "Password literal detected",
            Pattern: new Regex(@"[Pp]assword\s*[:=]\s*['\"]([^'\"]{8,})['\"]", RegexOptions.Compiled),
            Severity: SecretSeverity.High,
            Remediation: "Never hardcode passwords. Use secure credential storage."
        ),

        // Connection Strings
        new SecretPattern(
            Name: "SQL Connection String with Password",
            Description: "SQL connection string with password detected",
            Pattern: new Regex(@"(?:Password|pwd)\s*=\s*[^;]{8,}", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            Severity: SecretSeverity.Critical,
            Remediation: "Use integrated authentication or Azure Key Vault."
        ),

        // Private Keys
        new SecretPattern(
            Name: "RSA Private Key",
            Description: "RSA private key detected",
            Pattern: new Regex(@"-----BEGIN RSA PRIVATE KEY-----", RegexOptions.Compiled),
            Severity: SecretSeverity.Critical,
            Remediation: "Never commit private keys. Regenerate and store securely."
        ),
        new SecretPattern(
            Name: "SSH Private Key",
            Description: "SSH private key detected",
            Pattern: new Regex(@"-----BEGIN OPENSSH PRIVATE KEY-----", RegexOptions.Compiled),
            Severity: SecretSeverity.Critical,
            Remediation: "Never commit SSH keys. Regenerate and use SSH agent."
        ),
        new SecretPattern(
            Name: "PGP Private Key",
            Description: "PGP private key detected",
            Pattern: new Regex(@"-----BEGIN PGP PRIVATE KEY BLOCK-----", RegexOptions.Compiled),
            Severity: SecretSeverity.Critical,
            Remediation: "Never commit PGP keys. Store securely offline."
        ),

        // Azure
        new SecretPattern(
            Name: "Azure Storage Account Key",
            Description: "Azure storage account key detected",
            Pattern: new Regex(@"AccountKey\s*=\s*[A-Za-z0-9+/]{88}==", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            Severity: SecretSeverity.Critical,
            Remediation: "Use Azure Key Vault or managed identities."
        ),

        // Google Cloud
        new SecretPattern(
            Name: "Google API Key",
            Description: "Google API key detected",
            Pattern: new Regex(@"AIza[0-9A-Za-z_-]{35}", RegexOptions.Compiled),
            Severity: SecretSeverity.High,
            Remediation: "Regenerate key and restrict by IP/domain."
        ),

        // JWT Tokens
        new SecretPattern(
            Name: "JWT Token",
            Description: "JWT token detected",
            Pattern: new Regex(@"eyJ[a-zA-Z0-9_-]*\.eyJ[a-zA-Z0-9_-]*\.[a-zA-Z0-9_-]+", RegexOptions.Compiled),
            Severity: SecretSeverity.Medium,
            Remediation: "JWTs should not be committed. Use short-lived tokens."
        ),

        // Stripe
        new SecretPattern(
            Name: "Stripe API Key",
            Description: "Stripe API key detected",
            Pattern: new Regex(@"(sk|rk)_live_[0-9a-zA-Z]{24,}", RegexOptions.Compiled),
            Severity: SecretSeverity.Critical,
            Remediation: "Revoke at dashboard.stripe.com. Use environment variables."
        ),

        // Twilio
        new SecretPattern(
            Name: "Twilio API Key",
            Description: "Twilio API key detected",
            Pattern: new Regex(@"SK[a-z0-9]{32}", RegexOptions.Compiled),
            Severity: SecretSeverity.High,
            Remediation: "Revoke at twilio.com/console. Use environment variables."
        ),

        // Generic High-Entropy Strings (catches unknown patterns)
        new SecretPattern(
            Name: "High Entropy String",
            Description: "High entropy string detected (possible secret)",
            Pattern: new Regex(@"['\"]([a-zA-Z0-9+/=_-]{40,})['\"]", RegexOptions.Compiled),
            Severity: SecretSeverity.Low,
            Remediation: "If this is a secret, use environment variables or secure storage."
        )
    };

    /// <summary>
    /// Whitelist patterns - these are NOT secrets
    /// </summary>
    public static readonly List<Regex> WhitelistPatterns = new()
    {
        // Example values
        new Regex(@"example\.com", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"your[-_]?key[-_]?here", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"REPLACE[-_]?ME", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"TODO:?", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"FIXME:?", RegexOptions.Compiled | RegexOptions.IgnoreCase),

        // Test data
        new Regex(@"test[-_]?(key|secret|password)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"fake[-_]?(key|secret|password)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"dummy[-_]?(key|secret|password)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new Regex(@"mock[-_]?(key|secret|password)", RegexOptions.Compiled | RegexOptions.IgnoreCase),

        // Base64 encoded common strings (not secrets)
        new Regex(@"VGVzdA==", RegexOptions.Compiled), // "Test"
        new Regex(@"ZXhhbXBsZQ==", RegexOptions.Compiled), // "example"
    };
}
