using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.Consciousness;

/// <summary>
/// Assumption Tracker - Log and validate all assumptions made during reasoning.
/// Prevents failures from wrong assumptions by making them explicit and verifiable.
///
/// Improvement #4: Assumption Tracker (Value: 8/10, Effort: 4/10, Ratio: 2.00)
/// </summary>
public class AssumptionTracker
{
    private readonly string _logPath;
    private readonly List<Assumption> _currentSessionAssumptions;

    public AssumptionTracker(string basePath)
    {
        var logDir = Path.Combine(basePath, "data", "assumptions");
        if (!Directory.Exists(logDir))
            Directory.CreateDirectory(logDir);

        _logPath = Path.Combine(logDir, "assumptions.log.md");
        _currentSessionAssumptions = new List<Assumption>();
    }

    /// <summary>
    /// Record a new assumption
    /// </summary>
    public Assumption RecordAssumption(
        string description,
        AssumptionCategory category,
        AssumptionConfidence confidence,
        string? rationale = null,
        Dictionary<string, object>? evidence = null)
    {
        var assumption = new Assumption
        {
            Id = Guid.NewGuid().ToString(),
            Timestamp = DateTime.UtcNow,
            Description = description,
            Category = category,
            Confidence = confidence,
            Rationale = rationale,
            Evidence = evidence ?? new Dictionary<string, object>(),
            Status = AssumptionStatus.Unverified
        };

        _currentSessionAssumptions.Add(assumption);

        // Log to console
        var confidenceSymbol = confidence switch
        {
            AssumptionConfidence.Low => "❓",
            AssumptionConfidence.Medium => "🤔",
            AssumptionConfidence.High => "✓",
            AssumptionConfidence.Certain => "✅",
            _ => "?"
        };

        Console.WriteLine($"{confidenceSymbol} ASSUMPTION [{category}]: {description}");
        if (confidence == AssumptionConfidence.Low)
        {
            Console.WriteLine($"   ⚠️ Low confidence - should verify before acting");
        }

        return assumption;
    }

    /// <summary>
    /// Validate an assumption (mark as correct or incorrect)
    /// </summary>
    public void ValidateAssumption(string assumptionId, bool wasCorrect, string? validationNotes = null)
    {
        var assumption = _currentSessionAssumptions.FirstOrDefault(a => a.Id == assumptionId);
        if (assumption == null)
        {
            Console.WriteLine($"⚠️ Assumption {assumptionId} not found");
            return;
        }

        assumption.Status = wasCorrect ? AssumptionStatus.Verified : AssumptionStatus.Incorrect;
        assumption.ValidatedAt = DateTime.UtcNow;
        assumption.ValidationNotes = validationNotes;

        var symbol = wasCorrect ? "✅" : "❌";
        Console.WriteLine($"{symbol} Assumption validated: {assumption.Description} - {(wasCorrect ? "CORRECT" : "INCORRECT")}");

        if (!wasCorrect)
        {
            Console.WriteLine($"   🔴 LEARNING OPPORTUNITY: Assumption was wrong - update mental model");
        }
    }

    /// <summary>
    /// Check for unverified assumptions before taking action
    /// </summary>
    public AssumptionCheckResult CheckUnverifiedAssumptions()
    {
        var unverified = _currentSessionAssumptions
            .Where(a => a.Status == AssumptionStatus.Unverified)
            .ToList();

        var lowConfidence = unverified
            .Where(a => a.Confidence == AssumptionConfidence.Low)
            .ToList();

        var result = new AssumptionCheckResult
        {
            HasUnverified = unverified.Any(),
            UnverifiedCount = unverified.Count,
            LowConfidenceCount = lowConfidence.Count,
            UnverifiedAssumptions = unverified
        };

        if (result.HasUnverified)
        {
            Console.WriteLine($"⚠️ WARNING: {result.UnverifiedCount} unverified assumptions");
            if (result.LowConfidenceCount > 0)
            {
                Console.WriteLine($"   🚨 {result.LowConfidenceCount} have LOW CONFIDENCE - high risk of failure");
                Console.WriteLine("   Recommendation: VERIFY before proceeding");
            }
        }

        return result;
    }

    /// <summary>
    /// Get assumptions by category
    /// </summary>
    public List<Assumption> GetAssumptionsByCategory(AssumptionCategory category)
    {
        return _currentSessionAssumptions
            .Where(a => a.Category == category)
            .ToList();
    }

    /// <summary>
    /// Get incorrect assumptions (learning opportunities)
    /// </summary>
    public List<Assumption> GetIncorrectAssumptions()
    {
        return _currentSessionAssumptions
            .Where(a => a.Status == AssumptionStatus.Incorrect)
            .ToList();
    }

    /// <summary>
    /// Save assumptions to log file (at session end)
    /// </summary>
    public async Task SaveSessionAssumptionsAsync()
    {
        if (!_currentSessionAssumptions.Any())
            return;

        var content = FormatAssumptionsLog();
        await File.AppendAllTextAsync(_logPath, content);

        Console.WriteLine($"💾 Saved {_currentSessionAssumptions.Count} assumptions to log");

        // Calculate accuracy
        var validated = _currentSessionAssumptions.Where(a => a.Status != AssumptionStatus.Unverified).ToList();
        if (validated.Any())
        {
            var correct = validated.Count(a => a.Status == AssumptionStatus.Verified);
            var accuracy = (double)correct / validated.Count * 100;
            Console.WriteLine($"   Assumption accuracy: {accuracy:F1}% ({correct}/{validated.Count} correct)");
        }
    }

    /// <summary>
    /// Format assumptions for log
    /// </summary>
    private string FormatAssumptionsLog()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"\n## Session: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}\n");

        // Group by category
        var byCategory = _currentSessionAssumptions
            .GroupBy(a => a.Category)
            .OrderBy(g => g.Key.ToString());

        foreach (var group in byCategory)
        {
            sb.AppendLine($"### {group.Key}\n");

            foreach (var assumption in group)
            {
                var statusSymbol = assumption.Status switch
                {
                    AssumptionStatus.Verified => "✅",
                    AssumptionStatus.Incorrect => "❌",
                    AssumptionStatus.Unverified => "❓",
                    _ => "?"
                };

                var confidenceText = assumption.Confidence switch
                {
                    AssumptionConfidence.Low => "Low",
                    AssumptionConfidence.Medium => "Medium",
                    AssumptionConfidence.High => "High",
                    AssumptionConfidence.Certain => "Certain",
                    _ => "Unknown"
                };

                sb.AppendLine($"{statusSymbol} **{assumption.Description}**");
                sb.AppendLine($"   - Confidence: {confidenceText}");
                sb.AppendLine($"   - Status: {assumption.Status}");

                if (!string.IsNullOrEmpty(assumption.Rationale))
                {
                    sb.AppendLine($"   - Rationale: {assumption.Rationale}");
                }

                if (!string.IsNullOrEmpty(assumption.ValidationNotes))
                {
                    sb.AppendLine($"   - Validation: {assumption.ValidationNotes}");
                }

                sb.AppendLine();
            }
        }

        sb.AppendLine("---\n");
        return sb.ToString();
    }

    /// <summary>
    /// Generate assumption analysis report
    /// </summary>
    public AssumptionAnalysis AnalyzeAssumptions()
    {
        var total = _currentSessionAssumptions.Count;
        var verified = _currentSessionAssumptions.Count(a => a.Status == AssumptionStatus.Verified);
        var incorrect = _currentSessionAssumptions.Count(a => a.Status == AssumptionStatus.Incorrect);
        var unverified = _currentSessionAssumptions.Count(a => a.Status == AssumptionStatus.Unverified);

        // Calculate accuracy (only for validated assumptions)
        var validated = verified + incorrect;
        var accuracy = validated > 0 ? (double)verified / validated * 100 : 0;

        // Identify risky patterns
        var riskyPatterns = new List<string>();

        // Pattern 1: Low confidence assumptions that were incorrect
        var lowConfidenceIncorrect = _currentSessionAssumptions
            .Where(a => a.Confidence == AssumptionConfidence.Low && a.Status == AssumptionStatus.Incorrect)
            .ToList();

        if (lowConfidenceIncorrect.Any())
        {
            riskyPatterns.Add($"{lowConfidenceIncorrect.Count} low-confidence assumptions were incorrect");
        }

        // Pattern 2: High confidence assumptions that were incorrect (overconfidence)
        var overconfident = _currentSessionAssumptions
            .Where(a => a.Confidence >= AssumptionConfidence.High && a.Status == AssumptionStatus.Incorrect)
            .ToList();

        if (overconfident.Any())
        {
            riskyPatterns.Add($"{overconfident.Count} high-confidence assumptions were WRONG (overconfidence)");
        }

        // Pattern 3: Many unverified assumptions (operating blindly)
        if (unverified > 5)
        {
            riskyPatterns.Add($"{unverified} unverified assumptions - operating with high uncertainty");
        }

        return new AssumptionAnalysis
        {
            TotalAssumptions = total,
            VerifiedCorrect = verified,
            VerifiedIncorrect = incorrect,
            Unverified = unverified,
            AccuracyPercentage = accuracy,
            RiskyPatterns = riskyPatterns,
            MostCommonCategory = _currentSessionAssumptions
                .GroupBy(a => a.Category)
                .OrderByDescending(g => g.Count())
                .FirstOrDefault()?.Key ?? AssumptionCategory.Unknown
        };
    }
}

/// <summary>
/// Assumption record
/// </summary>
public class Assumption
{
    public string Id { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Description { get; set; } = string.Empty;
    public AssumptionCategory Category { get; set; }
    public AssumptionConfidence Confidence { get; set; }
    public string? Rationale { get; set; }
    public Dictionary<string, object> Evidence { get; set; } = new();
    public AssumptionStatus Status { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public string? ValidationNotes { get; set; }
}

/// <summary>
/// Assumption categories
/// </summary>
public enum AssumptionCategory
{
    Unknown,
    UserIntent,          // What user wants
    CodeBehavior,        // How code works
    FileLocation,        // Where files are
    Environment,         // System state, configuration
    Dependencies,        // External systems, APIs
    Constraints,         // Limitations, requirements
    Impact,              // Effect of changes
    Architecture         // System design, patterns
}

/// <summary>
/// Confidence level in assumption
/// </summary>
public enum AssumptionConfidence
{
    Low,        // 0-40% confident - high risk
    Medium,     // 40-70% confident - should verify
    High,       // 70-90% confident - likely correct
    Certain     // 90-100% confident - verified by evidence
}

/// <summary>
/// Assumption validation status
/// </summary>
public enum AssumptionStatus
{
    Unverified,   // Not yet checked
    Verified,     // Confirmed correct
    Incorrect     // Proven wrong
}

/// <summary>
/// Result of assumption check
/// </summary>
public class AssumptionCheckResult
{
    public bool HasUnverified { get; set; }
    public int UnverifiedCount { get; set; }
    public int LowConfidenceCount { get; set; }
    public List<Assumption> UnverifiedAssumptions { get; set; } = new();
}

/// <summary>
/// Assumption analysis
/// </summary>
public class AssumptionAnalysis
{
    public int TotalAssumptions { get; set; }
    public int VerifiedCorrect { get; set; }
    public int VerifiedIncorrect { get; set; }
    public int Unverified { get; set; }
    public double AccuracyPercentage { get; set; }
    public List<string> RiskyPatterns { get; set; } = new();
    public AssumptionCategory MostCommonCategory { get; set; }
}
