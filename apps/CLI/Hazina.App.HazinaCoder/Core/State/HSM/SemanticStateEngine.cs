using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Hazina.App.HazinaCoder.Core.State.HSM;

/// <summary>
/// Semantic State Engine - AI-native state storage using vector embeddings
///
/// Encodes state as high-dimensional vectors (embeddings) for:
/// - **Semantic search**: Find similar states by meaning, not just keywords
/// - **State interpolation**: Smoothly blend two states
/// - **Anomaly detection**: Detect impossible/invalid states
/// - **Clustering**: Group related states together
///
/// Uses simplified embedding model (production would use OpenAI/Anthropic embeddings)
/// </summary>
public sealed class SemanticStateEngine
{
    private readonly int _embeddingDimensions;
    private readonly Dictionary<StateId, StateEmbedding> _embeddingCache = new();
    private readonly Random _random = new(42); // Deterministic for testing

    public SemanticStateEngine(int embeddingDimensions = 384)
    {
        _embeddingDimensions = embeddingDimensions;
    }

    /// <summary>
    /// Encode state as vector embedding
    /// </summary>
    public async Task<StateEmbedding> EncodeStateAsync(ImmutableStateSnapshot state)
    {
        // Check cache first
        if (_embeddingCache.TryGetValue(state.Id, out var cached))
        {
            return cached;
        }

        // Generate embedding from state features
        var embedding = GenerateEmbedding(state);

        // Cache it
        _embeddingCache[state.Id] = embedding;

        return await Task.FromResult(embedding);
    }

    /// <summary>
    /// Generate embedding vector from state features
    /// (Simplified version - production would use LLM embeddings)
    /// </summary>
    private StateEmbedding GenerateEmbedding(ImmutableStateSnapshot state)
    {
        var features = ExtractFeatures(state);
        var vector = FeaturesToVector(features);

        return new StateEmbedding
        {
            StateId = state.Id,
            Vector = vector,
            Timestamp = state.Timestamp,
            Features = features
        };
    }

    /// <summary>
    /// Extract semantic features from state
    /// </summary>
    private Dictionary<string, double> ExtractFeatures(ImmutableStateSnapshot state)
    {
        var features = new Dictionary<string, double>();

        // Feature 1: Workflow mode (one-hot encoding)
        features[$"mode_{state.Mode}"] = 1.0;

        // Feature 2: Cognitive load (normalized 0-1)
        features["cognitive_load"] = (int)state.Load / 4.0;

        // Feature 3: Goal count (normalized)
        features["goal_count"] = Math.Min(1.0, state.Goals.Count / 10.0);

        // Feature 4: Working memory utilization (normalized)
        features["memory_utilization"] = Math.Min(1.0, state.WorkingMemory.Count / 9.0);

        // Feature 5: Decision frequency (normalized)
        var recentDecisions = state.Decisions.Count(d =>
            d.Timestamp > DateTime.UtcNow.AddMinutes(-10));
        features["decision_frequency"] = Math.Min(1.0, recentDecisions / 10.0);

        // Feature 6: Focus intensity (normalized)
        features["focus_intensity"] = state.Focus.PriorityPercent / 100.0;

        // Feature 7: Session duration (normalized to hours)
        var sessionDuration = (DateTime.UtcNow - state.Session.StartTime).TotalHours;
        features["session_duration"] = Math.Min(1.0, sessionDuration / 8.0);

        // Feature 8-N: Text embeddings from focus area (simplified bag-of-words)
        var focusWords = state.Focus.Area.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in focusWords.Take(10))
        {
            var wordHash = Math.Abs(word.GetHashCode() % 1000);
            features[$"word_{wordHash}"] = 0.5; // Simplified word embedding
        }

        return features;
    }

    /// <summary>
    /// Convert features to dense vector
    /// </summary>
    private float[] FeaturesToVector(Dictionary<string, double> features)
    {
        var vector = new float[_embeddingDimensions];

        // Hash features into fixed-size vector space
        var featureIndex = 0;
        foreach (var kvp in features)
        {
            var hash = Math.Abs(kvp.Key.GetHashCode());
            var index = hash % _embeddingDimensions;

            // Use hashing trick to distribute features
            vector[index] += (float)kvp.Value;

            // Add some randomness for better separation (simplified)
            var secondaryIndex = (hash * 31) % _embeddingDimensions;
            vector[secondaryIndex] += (float)(kvp.Value * 0.5);

            featureIndex++;
        }

        // Normalize vector (L2 normalization)
        var magnitude = Math.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0)
        {
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] /= (float)magnitude;
            }
        }

        return vector;
    }

    /// <summary>
    /// Find similar states using cosine similarity
    /// </summary>
    public async Task<List<SimilarityMatch>> FindSimilarAsync(
        ImmutableStateSnapshot referenceState,
        int topK = 5,
        double minSimilarity = 0.0)
    {
        var referenceEmbedding = await EncodeStateAsync(referenceState);
        var matches = new List<SimilarityMatch>();

        foreach (var kvp in _embeddingCache)
        {
            if (kvp.Key == referenceState.Id)
                continue; // Skip self

            var similarity = CosineSimilarity(referenceEmbedding.Vector, kvp.Value.Vector);

            if (similarity >= minSimilarity)
            {
                matches.Add(new SimilarityMatch
                {
                    StateId = kvp.Key,
                    Embedding = kvp.Value,
                    Similarity = similarity
                });
            }
        }

        return matches
            .OrderByDescending(m => m.Similarity)
            .Take(topK)
            .ToList();
    }

    /// <summary>
    /// Interpolate between two states (smooth transition)
    /// </summary>
    public async Task<List<StateEmbedding>> InterpolateAsync(
        ImmutableStateSnapshot stateA,
        ImmutableStateSnapshot stateB,
        int steps = 10)
    {
        var embeddingA = await EncodeStateAsync(stateA);
        var embeddingB = await EncodeStateAsync(stateB);

        var interpolated = new List<StateEmbedding>();

        for (int step = 1; step < steps; step++)
        {
            var alpha = step / (double)steps;
            var blendedVector = BlendVectors(embeddingA.Vector, embeddingB.Vector, alpha);

            interpolated.Add(new StateEmbedding
            {
                StateId = new StateId(Guid.NewGuid(), 0),
                Vector = blendedVector,
                Timestamp = DateTime.UtcNow,
                Features = new Dictionary<string, double>() // Interpolated features
            });
        }

        return interpolated;
    }

    /// <summary>
    /// Detect anomalous states (outliers)
    /// </summary>
    public async Task<AnomalyDetectionResult> DetectAnomaliesAsync(
        ImmutableStateSnapshot state,
        double threshold = 0.3)
    {
        var stateEmbedding = await EncodeStateAsync(state);

        // Calculate average similarity to all other states
        var similarities = new List<double>();

        foreach (var kvp in _embeddingCache)
        {
            if (kvp.Key == state.Id)
                continue;

            var similarity = CosineSimilarity(stateEmbedding.Vector, kvp.Value.Vector);
            similarities.Add(similarity);
        }

        var avgSimilarity = similarities.Any() ? similarities.Average() : 1.0;
        var isAnomalous = avgSimilarity < threshold;

        return new AnomalyDetectionResult
        {
            IsAnomalous = isAnomalous,
            AnomalyScore = 1.0 - avgSimilarity,
            AverageSimilarity = avgSimilarity,
            Reason = isAnomalous
                ? $"State is significantly different from typical states (avg similarity: {avgSimilarity:F2})"
                : "State is within normal bounds"
        };
    }

    /// <summary>
    /// Cluster states using K-means
    /// </summary>
    public async Task<List<StateCluster>> ClusterStatesAsync(int k = 5, int maxIterations = 100)
    {
        if (_embeddingCache.Count < k)
        {
            k = _embeddingCache.Count;
        }

        var embeddings = _embeddingCache.Values.ToList();

        // Initialize centroids randomly
        var centroids = embeddings.OrderBy(_ => _random.Next()).Take(k).ToList();

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            // Assign each state to nearest centroid
            var clusters = new Dictionary<int, List<StateEmbedding>>();
            for (int i = 0; i < k; i++)
            {
                clusters[i] = new List<StateEmbedding>();
            }

            foreach (var embedding in embeddings)
            {
                var nearestCentroid = 0;
                var minDistance = double.MaxValue;

                for (int i = 0; i < centroids.Count; i++)
                {
                    var distance = EuclideanDistance(embedding.Vector, centroids[i].Vector);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestCentroid = i;
                    }
                }

                clusters[nearestCentroid].Add(embedding);
            }

            // Recalculate centroids
            var newCentroids = new List<StateEmbedding>();
            foreach (var cluster in clusters.Values)
            {
                if (cluster.Count == 0)
                {
                    newCentroids.Add(centroids[newCentroids.Count]);
                    continue;
                }

                var centroidVector = new float[_embeddingDimensions];
                foreach (var embedding in cluster)
                {
                    for (int i = 0; i < _embeddingDimensions; i++)
                    {
                        centroidVector[i] += embedding.Vector[i];
                    }
                }

                for (int i = 0; i < _embeddingDimensions; i++)
                {
                    centroidVector[i] /= cluster.Count;
                }

                newCentroids.Add(new StateEmbedding
                {
                    StateId = new StateId(Guid.NewGuid(), 0),
                    Vector = centroidVector,
                    Timestamp = DateTime.UtcNow,
                    Features = new Dictionary<string, double>()
                });
            }

            // Check convergence
            var converged = true;
            for (int i = 0; i < centroids.Count; i++)
            {
                var distance = EuclideanDistance(centroids[i].Vector, newCentroids[i].Vector);
                if (distance > 0.001)
                {
                    converged = false;
                    break;
                }
            }

            centroids = newCentroids;

            if (converged)
                break;
        }

        // Build final clusters
        var finalClusters = new List<StateCluster>();
        for (int i = 0; i < k; i++)
        {
            var clusterStates = embeddings
                .Where(e =>
                {
                    var nearestCentroid = 0;
                    var minDistance = double.MaxValue;

                    for (int j = 0; j < centroids.Count; j++)
                    {
                        var distance = EuclideanDistance(e.Vector, centroids[j].Vector);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestCentroid = j;
                        }
                    }

                    return nearestCentroid == i;
                })
                .ToList();

            finalClusters.Add(new StateCluster
            {
                ClusterId = i,
                Centroid = centroids[i],
                States = clusterStates,
                Size = clusterStates.Count
            });
        }

        return await Task.FromResult(finalClusters);
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors
    /// </summary>
    private double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same dimensions");

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        magnitudeA = Math.Sqrt(magnitudeA);
        magnitudeB = Math.Sqrt(magnitudeB);

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>
    /// Calculate Euclidean distance between two vectors
    /// </summary>
    private double EuclideanDistance(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same dimensions");

        double sum = 0;
        for (int i = 0; i < a.Length; i++)
        {
            var diff = a[i] - b[i];
            sum += diff * diff;
        }

        return Math.Sqrt(sum);
    }

    /// <summary>
    /// Blend two vectors (linear interpolation)
    /// </summary>
    private float[] BlendVectors(float[] a, float[] b, double alpha)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vectors must have same dimensions");

        var blended = new float[a.Length];
        for (int i = 0; i < a.Length; i++)
        {
            blended[i] = (float)((1 - alpha) * a[i] + alpha * b[i]);
        }

        return blended;
    }

    /// <summary>
    /// Get statistics
    /// </summary>
    public SemanticEngineStatistics GetStatistics()
    {
        return new SemanticEngineStatistics
        {
            TotalEmbeddings = _embeddingCache.Count,
            EmbeddingDimensions = _embeddingDimensions,
            MemoryUsageBytes = _embeddingCache.Count * _embeddingDimensions * sizeof(float)
        };
    }
}

/// <summary>
/// State embedding (vector representation)
/// </summary>
public sealed class StateEmbedding
{
    public required StateId StateId { get; init; }
    public required float[] Vector { get; init; }
    public required DateTime Timestamp { get; init; }
    public required Dictionary<string, double> Features { get; init; }
}

/// <summary>
/// Similarity match
/// </summary>
public sealed class SimilarityMatch
{
    public required StateId StateId { get; init; }
    public required StateEmbedding Embedding { get; init; }
    public required double Similarity { get; init; }
}

/// <summary>
/// Anomaly detection result
/// </summary>
public sealed class AnomalyDetectionResult
{
    public bool IsAnomalous { get; init; }
    public double AnomalyScore { get; init; }
    public double AverageSimilarity { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// State cluster (from K-means)
/// </summary>
public sealed class StateCluster
{
    public int ClusterId { get; init; }
    public required StateEmbedding Centroid { get; init; }
    public required List<StateEmbedding> States { get; init; }
    public int Size { get; init; }
}

/// <summary>
/// Semantic engine statistics
/// </summary>
public sealed class SemanticEngineStatistics
{
    public int TotalEmbeddings { get; init; }
    public int EmbeddingDimensions { get; init; }
    public long MemoryUsageBytes { get; init; }

    public string MemoryUsageMB => $"{MemoryUsageBytes / 1024.0 / 1024.0:F2} MB";
}
