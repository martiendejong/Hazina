using System.Text.RegularExpressions;
using Hazina.App.HazinaCoder.Core.State;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Automatically captures experiences from user interactions
/// Detects preferences, patterns, and outcomes
/// </summary>
public class ExperienceCapture
{
    private readonly ExperienceStorage _storage;
    private readonly List<Experience> _recentExperiences = new();

    public ExperienceCapture(ExperienceStorage storage)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Analyze user interaction and capture relevant experiences
    /// </summary>
    public async Task CaptureFromInteractionAsync(UserInteraction interaction)
    {
        // Detect and capture different types of experiences
        await TryCapturePreference(interaction);
        await TryCaptureSuccessPattern(interaction);
        await TryCaptureErrorResolution(interaction);
        await TryCaptureUserInsight(interaction);
    }

    /// <summary>
    /// Detect if user stated a preference and capture it
    /// </summary>
    private async Task TryCapturePreference(UserInteraction interaction)
    {
        var message = interaction.Message;

        // Preference patterns
        var preferencePatterns = new[]
        {
            // "I prefer X"
            new Regex(@"I prefer (?<preference>.+?)(?:\.|$|,|\s+because|\s+over)", RegexOptions.IgnoreCase),

            // "I like X"
            new Regex(@"I like (?<preference>.+?)(?:\.|$|,|\s+because|\s+better)", RegexOptions.IgnoreCase),

            // "Always use X"
            new Regex(@"always use (?<preference>.+?)(?:\.|$|,)", RegexOptions.IgnoreCase),

            // "Never use X"
            new Regex(@"never use (?<preference>.+?)(?:\.|$|,)", RegexOptions.IgnoreCase),

            // "Use X instead of Y"
            new Regex(@"use (?<preference>.+?) instead of (?<alternative>.+?)(?:\.|$|,)", RegexOptions.IgnoreCase),

            // "Don't use X"
            new Regex(@"don't use (?<preference>.+?)(?:\.|$|,)", RegexOptions.IgnoreCase),

            // "I want X"
            new Regex(@"I want (?<preference>.+?)(?:\.|$|,)", RegexOptions.IgnoreCase)
        };

        foreach (var pattern in preferencePatterns)
        {
            var match = pattern.Match(message);
            if (match.Success)
            {
                var preference = match.Groups["preference"].Value.Trim();
                var isNegative = message.Contains("never", StringComparison.OrdinalIgnoreCase) ||
                                message.Contains("don't", StringComparison.OrdinalIgnoreCase);

                var experience = new Experience
                {
                    Type = ExperienceType.UserPreference,
                    Content = preference,
                    Description = isNegative
                        ? $"User prefers to avoid: {preference}"
                        : $"User prefers: {preference}",
                    Context = interaction.Context,
                    Tags = new List<string> { "preference", "user-stated", "explicit" },
                    Metadata = new ExperienceMetadata
                    {
                        Importance = 0.9, // Explicit preferences are highly important
                        EmotionalIntensity = 0.5,
                        Novelty = await CalculateNovelty(preference),
                        Confidence = 1.0 // User explicitly stated
                    }
                };

                await _storage.StoreExperienceAsync(experience);
                _recentExperiences.Add(experience);

                Console.WriteLine($"📝 Captured preference: {experience.Description}");
                break; // Only capture first matching pattern
            }
        }
    }

    /// <summary>
    /// Detect successful patterns from interaction outcomes
    /// </summary>
    private async Task TryCaptureSuccessPattern(UserInteraction interaction)
    {
        if (string.IsNullOrEmpty(interaction.Response))
            return;

        // Success indicators
        var successPatterns = new[]
        {
            "worked",
            "success",
            "fixed",
            "solved",
            "perfect",
            "exactly what",
            "that's it",
            "thank you",
            "great"
        };

        var hasSuccessIndicator = successPatterns.Any(pattern =>
            interaction.Response.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        if (hasSuccessIndicator && !string.IsNullOrEmpty(interaction.Message))
        {
            var experience = new Experience
            {
                Type = ExperienceType.CodePattern,
                Content = interaction.Message,
                Description = $"Successful approach: {TruncateForDescription(interaction.Message)}",
                Context = interaction.Context,
                Tags = new List<string> { "pattern", "successful", "verified" },
                Metadata = new ExperienceMetadata
                {
                    Importance = 0.8,
                    EmotionalIntensity = 0.7, // Success feels good
                    Novelty = await CalculateNovelty(interaction.Message),
                    Confidence = 0.8
                },
                Outcome = new ExperienceOutcome
                {
                    Successful = true,
                    Result = interaction.Response,
                    Satisfaction = 0.9
                }
            };

            await _storage.StoreExperienceAsync(experience);
            _recentExperiences.Add(experience);

            Console.WriteLine($"✅ Captured successful pattern");
        }
    }

    /// <summary>
    /// Detect error resolution patterns
    /// </summary>
    private async Task TryCaptureErrorResolution(UserInteraction interaction)
    {
        // Error indicators in message
        var errorPatterns = new[]
        {
            "error",
            "exception",
            "failed",
            "bug",
            "issue",
            "problem",
            "doesn't work",
            "not working"
        };

        var hasErrorIndicator = errorPatterns.Any(pattern =>
            interaction.Message.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        // Resolution indicators in response
        var resolutionPatterns = new[]
        {
            "fix",
            "solve",
            "resolve",
            "here's how",
            "try this",
            "solution"
        };

        var hasResolutionIndicator = !string.IsNullOrEmpty(interaction.Response) &&
            resolutionPatterns.Any(pattern =>
                interaction.Response.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        if (hasErrorIndicator && hasResolutionIndicator)
        {
            var experience = new Experience
            {
                Type = ExperienceType.ErrorResolution,
                Content = interaction.Response ?? "",
                Description = $"Error resolution: {TruncateForDescription(interaction.Message)}",
                Context = interaction.Context,
                Tags = new List<string> { "error", "resolution", "debugging" },
                Metadata = new ExperienceMetadata
                {
                    Importance = 0.85,
                    EmotionalIntensity = 0.6, // Error + resolution = relief
                    Novelty = await CalculateNovelty(interaction.Message),
                    Confidence = 0.7
                },
                Outcome = new ExperienceOutcome
                {
                    Successful = true,
                    Result = "Error resolved",
                    Satisfaction = 0.8
                }
            };

            await _storage.StoreExperienceAsync(experience);
            _recentExperiences.Add(experience);

            Console.WriteLine($"🔧 Captured error resolution");
        }
    }

    /// <summary>
    /// Capture insights about the user (communication style, expertise)
    /// </summary>
    private async Task TryCaptureUserInsight(UserInteraction interaction)
    {
        // Detect user expertise level from technical depth
        var technicalTerms = new[] { "async", "await", "lambda", "generic", "interface", "dependency injection", "LINQ" };
        var technicalTermCount = technicalTerms.Count(term =>
            interaction.Message.Contains(term, StringComparison.OrdinalIgnoreCase));

        if (technicalTermCount >= 3)
        {
            var experience = new Experience
            {
                Type = ExperienceType.UserInsight,
                Content = $"User demonstrates advanced technical knowledge ({technicalTermCount} technical terms)",
                Description = "User has advanced technical expertise",
                Context = interaction.Context,
                Tags = new List<string> { "user-insight", "expertise", "communication" },
                Metadata = new ExperienceMetadata
                {
                    Importance = 0.6,
                    EmotionalIntensity = 0.2,
                    Novelty = 0.5,
                    Confidence = 0.6
                }
            };

            await _storage.StoreExperienceAsync(experience);
        }
    }

    /// <summary>
    /// Calculate novelty of experience based on similar past experiences
    /// </summary>
    private async Task<double> CalculateNovelty(string content)
    {
        // Simple novelty: 1.0 if no similar recent experiences, decreases with repeats
        var similarCount = _recentExperiences.Count(e =>
            e.Content.Contains(content, StringComparison.OrdinalIgnoreCase) ||
            content.Contains(e.Content, StringComparison.OrdinalIgnoreCase));

        return similarCount == 0 ? 1.0 : 1.0 / (1.0 + Math.Log(similarCount + 1));
    }

    /// <summary>
    /// Truncate text for description (keep it concise)
    /// </summary>
    private string TruncateForDescription(string text, int maxLength = 100)
    {
        if (text.Length <= maxLength)
            return text;

        return text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Get recent experiences captured in this session
    /// </summary>
    public List<Experience> GetRecentExperiences(int count = 10)
    {
        return _recentExperiences
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Clear recent experiences cache
    /// </summary>
    public void ClearRecentExperiences()
    {
        _recentExperiences.Clear();
    }
}
