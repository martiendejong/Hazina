using Hazina.Agents.Content.Models;

namespace Hazina.Agents.Content;

/// <summary>
/// Agent that clusters articles into story groups based on embedding similarity.
/// Uses a DBSCAN-inspired algorithm with cosine similarity threshold.
/// </summary>
public class StoryClusterAgent
{
    private readonly ArticleEmbeddingAgent _embeddingAgent;
    private readonly StoryClusterOptions _options;

    public StoryClusterAgent(ArticleEmbeddingAgent embeddingAgent, StoryClusterOptions? options = null)
    {
        _embeddingAgent = embeddingAgent ?? throw new ArgumentNullException(nameof(embeddingAgent));
        _options = options ?? new StoryClusterOptions();
    }

    /// <summary>
    /// Clusters a set of articles into story groups.
    /// Articles with cosine similarity >= threshold are grouped together.
    /// </summary>
    /// <param name="articles">Articles to cluster (must already be embedded)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of story clusters</returns>
    public async Task<List<StoryCluster>> ClusterAsync(
        IReadOnlyList<Article> articles,
        CancellationToken cancellationToken = default)
    {
        if (articles == null) throw new ArgumentNullException(nameof(articles));
        if (articles.Count == 0) return new List<StoryCluster>();

        // Build similarity matrix
        var similarities = await BuildSimilarityMatrixAsync(articles, cancellationToken);

        // Run clustering
        var assignments = RunDbscan(articles.Count, similarities);

        // Build cluster objects
        return BuildClusters(articles, assignments);
    }

    /// <summary>
    /// Embeds articles and then clusters them in one operation.
    /// Convenience method that combines EmbedBatchAsync + ClusterAsync.
    /// </summary>
    /// <param name="articles">Articles to embed and cluster</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of story clusters</returns>
    public async Task<List<StoryCluster>> EmbedAndClusterAsync(
        IReadOnlyList<Article> articles,
        CancellationToken cancellationToken = default)
    {
        if (articles == null) throw new ArgumentNullException(nameof(articles));

        // Embed all articles first
        await _embeddingAgent.EmbedBatchAsync(articles, cancellationToken);

        // Then cluster
        return await ClusterAsync(articles, cancellationToken);
    }

    /// <summary>
    /// Builds an NxN cosine similarity matrix for the given articles.
    /// </summary>
    private async Task<double[,]> BuildSimilarityMatrixAsync(
        IReadOnlyList<Article> articles,
        CancellationToken cancellationToken)
    {
        var n = articles.Count;
        var matrix = new double[n, n];

        // Load all embeddings
        var embeddings = new EmbeddingInfo?[n];
        for (int i = 0; i < n; i++)
        {
            embeddings[i] = await _embeddingAgent.GetEmbeddingAsync(articles[i].Id, cancellationToken);
        }

        // Calculate pairwise similarities
        for (int i = 0; i < n; i++)
        {
            matrix[i, i] = 1.0; // Self-similarity

            for (int j = i + 1; j < n; j++)
            {
                double similarity = 0.0;

                if (embeddings[i]?.Data != null && embeddings[j]?.Data != null)
                {
                    similarity = embeddings[i]!.Data.CosineSimilarity(embeddings[j]!.Data);
                }

                matrix[i, j] = similarity;
                matrix[j, i] = similarity;
            }
        }

        return matrix;
    }

    /// <summary>
    /// DBSCAN-inspired clustering using cosine similarity threshold.
    /// Returns cluster assignment for each article index (-1 = noise/unclustered).
    /// </summary>
    private int[] RunDbscan(int n, double[,] similarities)
    {
        var assignments = new int[n];
        Array.Fill(assignments, -1); // Unassigned

        int clusterId = 0;

        for (int i = 0; i < n; i++)
        {
            if (assignments[i] != -1) continue; // Already assigned

            // Find neighbors (articles similar enough to this one)
            var neighbors = GetNeighbors(i, n, similarities);

            if (neighbors.Count < _options.MinClusterSize - 1)
            {
                // Not enough neighbors - will be assigned later or remain noise
                continue;
            }

            // Start a new cluster
            assignments[i] = clusterId;

            // Expand cluster
            var queue = new Queue<int>(neighbors);
            while (queue.Count > 0)
            {
                var j = queue.Dequeue();
                if (assignments[j] != -1) continue;

                assignments[j] = clusterId;

                // Check this point's neighbors too
                var jNeighbors = GetNeighbors(j, n, similarities);
                if (jNeighbors.Count >= _options.MinClusterSize - 1)
                {
                    foreach (var k in jNeighbors)
                    {
                        if (assignments[k] == -1)
                            queue.Enqueue(k);
                    }
                }
            }

            clusterId++;
        }

        // Assign remaining noise points as singleton clusters
        for (int i = 0; i < n; i++)
        {
            if (assignments[i] == -1)
            {
                assignments[i] = clusterId++;
            }
        }

        return assignments;
    }

    /// <summary>
    /// Gets indices of articles that are similar enough to article at index i.
    /// </summary>
    private List<int> GetNeighbors(int i, int n, double[,] similarities)
    {
        var neighbors = new List<int>();
        for (int j = 0; j < n; j++)
        {
            if (j != i && similarities[i, j] >= _options.SimilarityThreshold)
            {
                neighbors.Add(j);
            }
        }
        return neighbors;
    }

    /// <summary>
    /// Builds StoryCluster objects from the cluster assignments.
    /// </summary>
    private List<StoryCluster> BuildClusters(IReadOnlyList<Article> articles, int[] assignments)
    {
        var clusterMap = new Dictionary<int, List<int>>();

        for (int i = 0; i < assignments.Length; i++)
        {
            if (!clusterMap.ContainsKey(assignments[i]))
                clusterMap[assignments[i]] = new List<int>();
            clusterMap[assignments[i]].Add(i);
        }

        var clusters = new List<StoryCluster>();

        foreach (var (clusterId, indices) in clusterMap)
        {
            var clusterArticles = indices.Select(i => articles[i]).ToList();

            var cluster = new StoryCluster
            {
                EventId = $"event-{Guid.NewGuid():N}",
                ArticleIds = clusterArticles.Select(a => a.Id).ToList(),
                RegionsCovered = clusterArticles
                    .Select(a => a.Region)
                    .Where(r => !string.IsNullOrEmpty(r))
                    .Distinct()
                    .ToList(),
                FirstDetected = clusterArticles.Min(a => a.PublishedAt),
                Label = clusterArticles
                    .OrderBy(a => a.PublishedAt)
                    .First()
                    .Title
            };

            clusters.Add(cluster);
        }

        return clusters.OrderByDescending(c => c.ArticleCount).ToList();
    }
}

/// <summary>
/// Configuration options for the StoryClusterAgent.
/// </summary>
public class StoryClusterOptions
{
    /// <summary>
    /// Cosine similarity threshold for considering two articles as the same event.
    /// Default: 0.75 (per task spec).
    /// </summary>
    public double SimilarityThreshold { get; set; } = 0.75;

    /// <summary>
    /// Minimum number of articles to form a cluster.
    /// Default: 1 (even a single article forms its own cluster).
    /// </summary>
    public int MinClusterSize { get; set; } = 1;
}
