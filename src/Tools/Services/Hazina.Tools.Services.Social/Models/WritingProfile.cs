namespace Hazina.Tools.Services.Social.Models;

/// <summary>
/// Project-wide writing profile (brand voice)
/// </summary>
public class WritingProfile
{
    public string ProjectId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public int SampleCount { get; set; }

    // Dominant Characteristics
    public string DominantTone { get; set; } = "";
    public int AvgContentLength { get; set; }
    public int AvgSentenceLength { get; set; }
    public string VocabularyLevel { get; set; } = "";

    // Topics & Keywords
    public List<string> TopTopics { get; set; } = new();
    public List<string> BrandKeywords { get; set; } = new();
    public Dictionary<string, int> KeywordFrequency { get; set; } = new();

    // Style Patterns
    public List<string> CommonPhrases { get; set; } = new();
    public List<string> OpeningSentences { get; set; } = new();
    public List<string> ClosingSentences { get; set; } = new();

    // AI-Generated Summary
    public string StyleSummary { get; set; } = "";
}
