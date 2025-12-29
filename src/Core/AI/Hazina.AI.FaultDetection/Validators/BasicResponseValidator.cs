using Hazina.AI.FaultDetection.Core;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hazina.AI.FaultDetection.Validators;

/// <summary>
/// Basic response validator with common validation rules
/// </summary>
public class BasicResponseValidator : IResponseValidator
{
    public async Task<ValidationResult> ValidateAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult { IsValid = true, ConfidenceScore = 1.0 };

        // 1. Check if response is empty
        if (string.IsNullOrWhiteSpace(response))
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Description = "Response is empty",
                Severity = IssueSeverity.Critical,
                Category = IssueCategory.MissingInformation
            });
            return result;
        }

        // 2. Validate based on expected response type
        switch (context.ResponseType)
        {
            case ResponseType.Json:
                ValidateJson(response, result);
                break;
            case ResponseType.Code:
                ValidateCode(response, result);
                break;
            case ResponseType.Xml:
                ValidateXml(response, result);
                break;
        }

        // 3. Apply custom validation rules
        foreach (var rule in context.Rules)
        {
            try
            {
                var isValid = await rule.Validator(response);
                if (!isValid)
                {
                    result.IsValid = false;
                    result.Issues.Add(new ValidationIssue
                    {
                        Description = $"Rule '{rule.Name}' failed: {rule.Description}",
                        Severity = rule.SeverityIfFailed,
                        Category = IssueCategory.General
                    });
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add(new ValidationIssue
                {
                    Description = $"Rule '{rule.Name}' threw exception: {ex.Message}",
                    Severity = IssueSeverity.Warning,
                    Category = IssueCategory.General
                });
            }
        }

        // 4. Check for common error indicators
        CheckForErrorIndicators(response, result);

        // 5. Validate against ground truth if available
        if (context.GroundTruth.Any())
        {
            ValidateAgainstGroundTruth(response, context.GroundTruth, result);
        }

        // Calculate overall confidence
        result.ConfidenceScore = CalculateConfidence(result);

        return result;
    }

    public async Task<ValidationResult> ValidateAndCorrectAsync(
        string response,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = await ValidateAsync(response, context, cancellationToken);

        if (!result.IsValid && context.ResponseType == ResponseType.Json)
        {
            // Attempt to fix JSON
            var corrected = AttemptJsonCorrection(response);
            if (corrected != null)
            {
                result.CorrectedResponse = corrected;
            }
        }

        return result;
    }

    private void ValidateJson(string response, ValidationResult result)
    {
        try
        {
            JsonDocument.Parse(response);
        }
        catch (JsonException ex)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Description = $"Invalid JSON: {ex.Message}",
                Severity = IssueSeverity.Error,
                Category = IssueCategory.FormatError,
                LineNumber = (int?)ex.LineNumber,
                SuggestedFix = "Ensure response is valid JSON"
            });
        }
    }

    private void ValidateCode(string response, ValidationResult result)
    {
        // Basic code validation
        if (response.Contains("```"))
        {
            // Extract code from markdown code blocks
            var codeBlockPattern = @"```(?:\w+)?\s*\n(.*?)\n```";
            var matches = Regex.Matches(response, codeBlockPattern, RegexOptions.Singleline);

            if (!matches.Any())
            {
                result.Issues.Add(new ValidationIssue
                {
                    Description = "Code block markers found but no valid code extracted",
                    Severity = IssueSeverity.Warning,
                    Category = IssueCategory.FormatError
                });
            }
        }
    }

    private void ValidateXml(string response, ValidationResult result)
    {
        try
        {
            System.Xml.Linq.XDocument.Parse(response);
        }
        catch (System.Xml.XmlException ex)
        {
            result.IsValid = false;
            result.Issues.Add(new ValidationIssue
            {
                Description = $"Invalid XML: {ex.Message}",
                Severity = IssueSeverity.Error,
                Category = IssueCategory.FormatError,
                LineNumber = ex.LineNumber
            });
        }
    }

    private void CheckForErrorIndicators(string response, ValidationResult result)
    {
        var errorIndicators = new[]
        {
            "I don't know",
            "I cannot",
            "I'm not sure",
            "As an AI",
            "I apologize",
            "error occurred",
            "exception",
            "failed to"
        };

        foreach (var indicator in errorIndicators)
        {
            if (response.Contains(indicator, StringComparison.OrdinalIgnoreCase))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Description = $"Response contains error indicator: '{indicator}'",
                    Severity = IssueSeverity.Warning,
                    Category = IssueCategory.General
                });
                result.ConfidenceScore -= 0.1;
            }
        }
    }

    private void ValidateAgainstGroundTruth(
        string response,
        Dictionary<string, string> groundTruth,
        ValidationResult result)
    {
        foreach (var (key, expectedValue) in groundTruth)
        {
            if (!response.Contains(expectedValue, StringComparison.OrdinalIgnoreCase))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Description = $"Response missing expected value for '{key}': '{expectedValue}'",
                    Severity = IssueSeverity.Error,
                    Category = IssueCategory.MissingInformation
                });
                result.ConfidenceScore -= 0.2;
            }
        }
    }

    private double CalculateConfidence(ValidationResult result)
    {
        var confidence = 1.0;

        // Decrease confidence based on issues
        foreach (var issue in result.Issues)
        {
            confidence -= issue.Severity switch
            {
                IssueSeverity.Info => 0.05,
                IssueSeverity.Warning => 0.1,
                IssueSeverity.Error => 0.2,
                IssueSeverity.Critical => 0.4,
                _ => 0.1
            };
        }

        return Math.Max(0, Math.Min(1, confidence));
    }

    private string? AttemptJsonCorrection(string json)
    {
        try
        {
            // Try to extract JSON from markdown code blocks
            var codeBlockPattern = @"```(?:json)?\s*\n(.*?)\n```";
            var match = Regex.Match(json, codeBlockPattern, RegexOptions.Singleline);

            if (match.Success)
            {
                var extracted = match.Groups[1].Value;
                JsonDocument.Parse(extracted); // Validate
                return extracted;
            }

            // Try to remove common prefixes/suffixes
            var cleaned = json.Trim();
            if (cleaned.StartsWith("```") && cleaned.EndsWith("```"))
            {
                cleaned = Regex.Replace(cleaned, @"^```(?:json)?\s*\n|\n```$", "").Trim();
                JsonDocument.Parse(cleaned); // Validate
                return cleaned;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}
