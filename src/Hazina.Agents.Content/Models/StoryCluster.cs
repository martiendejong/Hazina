namespace Hazina.Agents.Content.Models;

/// <summary>
/// Represents a cluster of articles about the same event/story.
/// </summary>
public class StoryCluster
{
    /// <summary>
    /// Unique identifier for the event cluster.
    /// </summary>
    public string EventId { get; set; } = string.Empty;

    /// <summary>
    /// IDs of all articles in this cluster.
    /// </summary>
    public List<string> ArticleIds { get; set; } = new();

    /// <summary>
    /// Geographic regions covered by articles in this cluster.
    /// </summary>
    public List<string> RegionsCovered { get; set; } = new();

    /// <summary>
    /// Timestamp of the earliest article in the cluster.
    /// </summary>
    public DateTime FirstDetected { get; set; }

    /// <summary>
    /// Representative label for the cluster, derived from the first or most central article.
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Number of articles in this cluster.
    /// </summary>
    public int ArticleCount => ArticleIds.Count;
}
