namespace Hazina.Tools.Services.Social.Models;

/// <summary>
/// Result of content analysis
/// </summary>
public class ContentAnalysis
{
    public string ContentId { get; set; } = "";
    public DateTime AnalyzedAt { get; set; }

    // Style Analysis
    public string Tone { get; set; } = ""; // professional, casual, technical, friendly
    public int AvgSentenceLength { get; set; }
    public string VocabularyLevel { get; set; } = ""; // basic, intermediate, advanced
    public List<string> CommonPhrases { get; set; } = new();
    public string EmotionalTone { get; set; } = ""; // positive, negative, neutral

    // Topic Analysis
    public List<string> MainTopics { get; set; } = new();
    public List<string> Subtopics { get; set; } = new();
    public List<string> Keywords { get; set; } = new();
    public Dictionary<string, double> TopicScores { get; set; } = new(); // topic -> confidence

    // Sentiment Analysis
    public string Sentiment { get; set; } = ""; // positive, neutral, negative
    public double SentimentScore { get; set; } // -1.0 to 1.0

    // Engagement Prediction
    public double PredictedEngagementRate { get; set; }
    public List<string> SuggestedImprovements { get; set; } = new();
}
