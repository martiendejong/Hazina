using Azure;
using Azure.AI.OpenAI;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Handles retrieval of relevant experiences from vector database
/// Uses similarity search to find analogous past situations
/// </summary>
public class ExperienceRetrieval
{
    private readonly QdrantClient _qdrant;
    private readonly OpenAIClient _openai;
    private readonly ExperienceStorage _storage;
    private const string COLLECTION_NAME = "hazinacoder_experiences";
    private const string EMBEDDING_MODEL = "text-embedding-3-small";
    private const float SIMILARITY_THRESHOLD = 0.7f;

    public ExperienceRetrieval(
        QdrantClient qdrantClient,
        OpenAIClient openaiClient,
        ExperienceStorage storage)
    {
        _qdrant = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
        _openai = openaiClient ?? throw new ArgumentNullException(nameof(openaiClient));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    /// <summary>
    /// Find similar experiences based on query text
    /// </summary>
    public async Task<List<Experience>> FindSimilarExperiencesAsync(
        string query,
        int topK = 5,
        ExperienceType? filterType = null,
        float? minSimilarity = null)
    {
        try
        {
            // Generate query embedding
            var queryEmbedding = await GenerateEmbeddingAsync(query);

            // Build filter if type specified
            Filter? filter = null;
            if (filterType.HasValue)
            {
                filter = new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = "type",
                                Match = new Match { Keyword = filterType.Value.ToString() }
                            }
                        }
                    }
                };
            }

            // Search in Qdrant
            var searchResults = await _qdrant.SearchAsync(
                collectionName: COLLECTION_NAME,
                vector: queryEmbedding,
                limit: (ulong)topK,
                filter: filter,
                scoreThreshold: minSimilarity ?? SIMILARITY_THRESHOLD
            );

            // Convert results to Experience objects
            var experiences = new List<Experience>();
            foreach (var result in searchResults)
            {
                var experience = MapToExperience(result);
                experiences.Add(experience);

                // Update retrieval metadata
                experience.Metadata.RetrievalCount++;
                experience.Metadata.LastRetrieved = DateTime.UtcNow;
                await _storage.UpdateMetadataAsync(experience);
            }

            return experiences;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not retrieve experiences: {ex.Message}");
            return new List<Experience>();
        }
    }

    /// <summary>
    /// Get user preferences relevant to current context
    /// </summary>
    public async Task<List<Experience>> GetUserPreferencesAsync(string context, int topK = 10)
    {
        return await FindSimilarExperiencesAsync(
            query: context,
            topK: topK,
            filterType: ExperienceType.UserPreference
        );
    }

    /// <summary>
    /// Find similar code patterns
    /// </summary>
    public async Task<List<Experience>> FindSimilarPatternsAsync(string problemDescription, int topK = 5)
    {
        return await FindSimilarExperiencesAsync(
            query: problemDescription,
            topK: topK,
            filterType: ExperienceType.CodePattern
        );
    }

    /// <summary>
    /// Find similar error resolutions
    /// </summary>
    public async Task<List<Experience>> FindSimilarErrorResolutionsAsync(string errorDescription, int topK = 5)
    {
        return await FindSimilarExperiencesAsync(
            query: errorDescription,
            topK: topK,
            filterType: ExperienceType.ErrorResolution
        );
    }

    /// <summary>
    /// Get project-specific context
    /// </summary>
    public async Task<List<Experience>> GetProjectContextAsync(string projectName, int topK = 10)
    {
        try
        {
            // Search with project filter
            var queryEmbedding = await GenerateEmbeddingAsync($"project context for {projectName}");

            var filter = new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key = "context_project",
                            Match = new Match { Keyword = projectName }
                        }
                    }
                }
            };

            var results = await _qdrant.SearchAsync(
                collectionName: COLLECTION_NAME,
                vector: queryEmbedding,
                limit: (ulong)topK,
                filter: filter
            );

            return results.Select(MapToExperience).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not retrieve project context: {ex.Message}");
            return new List<Experience>();
        }
    }

    /// <summary>
    /// Get most important experiences (high importance score)
    /// </summary>
    public async Task<List<Experience>> GetMostImportantExperiencesAsync(int topK = 20)
    {
        try
        {
            // Use search with a dummy query and filter by importance
            // This is a simplified version for POC 1
            var dummyEmbedding = await GenerateEmbeddingAsync("important experiences");

            var results = await _qdrant.SearchAsync(
                collectionName: COLLECTION_NAME,
                vector: dummyEmbedding,
                limit: (ulong)topK
            );

            var experiences = results
                .Select(MapToExperience)
                .OrderByDescending(e => e.Metadata.Importance)
                .Take(topK)
                .ToList();

            return experiences;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not retrieve important experiences: {ex.Message}");
            return new List<Experience>();
        }
    }

    /// <summary>
    /// Generate embedding for query text
    /// </summary>
    private async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            var embeddingsOptions = new EmbeddingsOptions(
                deploymentName: EMBEDDING_MODEL,
                input: new List<string> { text }
            );

            Response<Embeddings> response = await _openai.GetEmbeddingsAsync(embeddingsOptions);
            return response.Value.Data[0].Embedding.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not generate embedding: {ex.Message}");
            return new float[1536]; // Return zero vector as fallback
        }
    }

    /// <summary>
    /// Map Qdrant search result to Experience object
    /// </summary>
    private Experience MapToExperience(ScoredPoint scoredPoint)
    {
        var payload = scoredPoint.Payload;

        var experience = new Experience
        {
            Id = Guid.Parse(GetPayloadString(payload, "id")), // Retrieve Guid from payload
            Timestamp = DateTime.Parse(GetPayloadString(payload, "timestamp")),
            Type = Enum.Parse<ExperienceType>(GetPayloadString(payload, "type")),
            Content = GetPayloadString(payload, "content"),
            Description = GetPayloadString(payload, "description"),
            Tags = GetPayloadString(payload, "tags").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),

            Context = new ExperienceContext
            {
                ProjectName = GetPayloadString(payload, "context_project"),
                FileName = GetPayloadString(payload, "context_file"),
                FunctionName = GetPayloadString(payload, "context_function"),
                Language = GetPayloadString(payload, "context_language"),
                Framework = GetPayloadString(payload, "context_framework"),
                Mode = Enum.Parse<State.WorkflowMode>(GetPayloadString(payload, "context_mode"))
            },

            Metadata = new ExperienceMetadata
            {
                Importance = GetPayloadDouble(payload, "importance"),
                EmotionalIntensity = GetPayloadDouble(payload, "emotional_intensity"),
                Novelty = GetPayloadDouble(payload, "novelty"),
                RetrievalCount = GetPayloadInt(payload, "retrieval_count"),
                Confidence = GetPayloadDouble(payload, "confidence")
            }
        };

        // Map outcome if present
        var outcomeSuccessful = GetPayloadBool(payload, "outcome_successful");
        var outcomeResult = GetPayloadString(payload, "outcome_result");
        if (!string.IsNullOrEmpty(outcomeResult))
        {
            experience.Outcome = new ExperienceOutcome
            {
                Successful = outcomeSuccessful,
                Result = outcomeResult,
                Satisfaction = GetPayloadDouble(payload, "outcome_satisfaction")
            };
        }

        return experience;
    }


    // Helper methods for safe payload extraction
    private string GetPayloadString(IDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) ? value.StringValue : string.Empty;
    }

    private double GetPayloadDouble(IDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) ? value.DoubleValue : 0.0;
    }

    private int GetPayloadInt(IDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) ? (int)value.IntegerValue : 0;
    }

    private bool GetPayloadBool(IDictionary<string, Value> payload, string key)
    {
        return payload.TryGetValue(key, out var value) && value.BoolValue;
    }
}
