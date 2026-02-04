using System.Security.Cryptography;
using System.Text;
using Spectre.Console;
using static Hazina.App.HazinaCoder.Core.Security.SecretPatterns;

namespace Hazina.App.HazinaCoder.Core.Security;

/// <summary>
/// Scans code for accidentally committed secrets
/// </summary>
public class SecretScanner
{
    private readonly List<SecretPattern> _patterns;
    private readonly double _entropyThreshold;

    public SecretScanner(double entropyThreshold = 4.5)
    {
        _patterns = SecretPatterns.AllPatterns;
        _entropyThreshold = entropyThreshold;
    }

    /// <summary>
    /// Scan a file for secrets
    /// </summary>
    public ScanResult ScanFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return new ScanResult(filePath, Array.Empty<SecretFinding>());
        }

        var content = File.ReadAllText(filePath);
        return ScanContent(filePath, content);
    }

    /// <summary>
    /// Scan content for secrets
    /// </summary>
    public ScanResult ScanContent(string source, string content)
    {
        var findings = new List<SecretFinding>();
        var lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var lineNumber = i + 1;

            // Skip comments (basic detection)
            if (IsComment(line))
                continue;

            // Pattern matching
            foreach (var pattern in _patterns)
            {
                var matches = pattern.Pattern.Matches(line);
                foreach (Match match in matches)
                {
                    // Check whitelist
                    if (IsWhitelisted(match.Value))
                        continue;

                    findings.Add(new SecretFinding(
                        Source: source,
                        LineNumber: lineNumber,
                        Column: match.Index + 1,
                        SecretType: pattern.Name,
                        Description: pattern.Description,
                        Severity: pattern.Severity,
                        MatchedText: MaskSecret(match.Value),
                        FullLine: line.Trim(),
                        Remediation: pattern.Remediation
                    ));
                }
            }

            // Entropy-based detection (for unknown secrets)
            var highEntropyStrings = FindHighEntropyStrings(line);
            foreach (var (str, entropy) in highEntropyStrings)
            {
                // Skip if already detected by pattern
                if (findings.Any(f => f.FullLine.Contains(str)))
                    continue;

                // Skip whitelisted
                if (IsWhitelisted(str))
                    continue;

                findings.Add(new SecretFinding(
                    Source: source,
                    LineNumber: lineNumber,
                    Column: line.IndexOf(str) + 1,
                    SecretType: "High Entropy String",
                    Description: $"Possible secret (entropy: {entropy:F2})",
                    Severity: SecretSeverity.Low,
                    MatchedText: MaskSecret(str),
                    FullLine: line.Trim(),
                    Remediation: "If this is a secret, use environment variables or secure storage."
                ));
            }
        }

        return new ScanResult(source, findings.ToArray());
    }

    /// <summary>
    /// Scan multiple files
    /// </summary>
    public List<ScanResult> ScanFiles(IEnumerable<string> filePaths)
    {
        return filePaths.Select(ScanFile).Where(r => r.HasFindings).ToList();
    }

    /// <summary>
    /// Scan directory recursively
    /// </summary>
    public List<ScanResult> ScanDirectory(string directoryPath, string[]? excludePatterns = null)
    {
        excludePatterns ??= new[] { "node_modules", ".git", "bin", "obj", ".vs" };

        var files = Directory.EnumerateFiles(directoryPath, "*.*", SearchOption.AllDirectories)
            .Where(f => !excludePatterns.Any(p => f.Contains(p)))
            .Where(f => IsTextFile(f))
            .ToList();

        return ScanFiles(files);
    }

    /// <summary>
    /// Display findings in console
    /// </summary>
    public void DisplayFindings(List<ScanResult> results)
    {
        if (!results.Any())
        {
            AnsiConsole.MarkupLine("[green]✓ No secrets detected[/]");
            return;
        }

        var totalFindings = results.Sum(r => r.Findings.Length);
        var criticalCount = results.SelectMany(r => r.Findings).Count(f => f.Severity == SecretSeverity.Critical);
        var highCount = results.SelectMany(r => r.Findings).Count(f => f.Severity == SecretSeverity.High);

        AnsiConsole.MarkupLine($"[red]⚠ {totalFindings} potential secret(s) detected[/]");
        AnsiConsole.MarkupLine($"  Critical: [red]{criticalCount}[/]  High: [yellow]{highCount}[/]");
        AnsiConsole.WriteLine();

        foreach (var result in results.OrderByDescending(r => r.Findings.Max(f => (int)f.Severity)))
        {
            AnsiConsole.MarkupLine($"[bold]{result.Source}[/]");

            var table = new Table()
                .Border(TableBorder.Rounded)
                .AddColumn("Line")
                .AddColumn("Severity")
                .AddColumn("Type")
                .AddColumn("Finding")
                .AddColumn("Remediation");

            foreach (var finding in result.Findings.OrderBy(f => f.LineNumber))
            {
                var severityColor = finding.Severity switch
                {
                    SecretSeverity.Critical => "red",
                    SecretSeverity.High => "yellow",
                    SecretSeverity.Medium => "orange1",
                    _ => "blue"
                };

                table.AddRow(
                    finding.LineNumber.ToString(),
                    $"[{severityColor}]{finding.Severity}[/]",
                    finding.SecretType,
                    $"[dim]{finding.MatchedText}[/]",
                    $"[dim]{finding.Remediation[..Math.Min(50, finding.Remediation.Length)]}...[/]"
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // Summary recommendations
        if (criticalCount > 0)
        {
            AnsiConsole.MarkupLine("[red bold]⚠ CRITICAL: Do not commit these files! Revoke exposed credentials immediately.[/]");
        }
    }

    /// <summary>
    /// Calculate Shannon entropy of a string
    /// </summary>
    private double CalculateEntropy(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        var charCounts = text.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        var length = text.Length;

        double entropy = 0;
        foreach (var count in charCounts.Values)
        {
            var probability = (double)count / length;
            entropy -= probability * Math.Log2(probability);
        }

        return entropy;
    }

    /// <summary>
    /// Find strings with high entropy (likely secrets)
    /// </summary>
    private List<(string Text, double Entropy)> FindHighEntropyStrings(string line)
    {
        var results = new List<(string, double)>();

        // Find quoted strings
        var stringPattern = new System.Text.RegularExpressions.Regex(@"['\"]([^'\"]{20,})['\"]");
        var matches = stringPattern.Matches(line);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var text = match.Groups[1].Value;

            // Skip if it's a URL, path, or common string
            if (text.Contains("http://") || text.Contains("https://") ||
                text.Contains("\\") || text.Contains("/") ||
                text.Contains(" ") || text.Length < 20)
                continue;

            var entropy = CalculateEntropy(text);

            if (entropy >= _entropyThreshold)
            {
                results.Add((text, entropy));
            }
        }

        return results;
    }

    /// <summary>
    /// Mask secret for display
    /// </summary>
    private string MaskSecret(string secret)
    {
        if (secret.Length <= 8)
            return new string('*', secret.Length);

        return $"{secret[..4]}{'*'.Repeat(Math.Min(20, secret.Length - 8))}{secret[^4..]}";
    }

    /// <summary>
    /// Check if line is a comment
    /// </summary>
    private bool IsComment(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith("//") ||
               trimmed.StartsWith("#") ||
               trimmed.StartsWith("/*") ||
               trimmed.StartsWith("*") ||
               trimmed.StartsWith("<!--");
    }

    /// <summary>
    /// Check if string is whitelisted
    /// </summary>
    private bool IsWhitelisted(string text)
    {
        return SecretPatterns.WhitelistPatterns.Any(p => p.IsMatch(text));
    }

    /// <summary>
    /// Check if file is likely a text file
    /// </summary>
    private bool IsTextFile(string filePath)
    {
        var textExtensions = new[] { ".cs", ".ts", ".js", ".tsx", ".jsx", ".py", ".go", ".java", ".cpp", ".h", ".txt", ".json", ".yml", ".yaml", ".md", ".xml", ".config", ".env" };
        return textExtensions.Any(ext => filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Result of secret scan
/// </summary>
public record ScanResult(string Source, SecretFinding[] Findings)
{
    public bool HasFindings => Findings.Length > 0;
    public bool HasCriticalFindings => Findings.Any(f => f.Severity == SecretSeverity.Critical);
    public bool HasHighFindings => Findings.Any(f => f.Severity >= SecretSeverity.High);
}

/// <summary>
/// Individual secret finding
/// </summary>
public record SecretFinding(
    string Source,
    int LineNumber,
    int Column,
    string SecretType,
    string Description,
    SecretSeverity Severity,
    string MatchedText,
    string FullLine,
    string Remediation
);

/// <summary>
/// String extension helper
/// </summary>
internal static class StringExtensions
{
    public static string Repeat(this char c, int count)
    {
        return new string(c, count);
    }
}
