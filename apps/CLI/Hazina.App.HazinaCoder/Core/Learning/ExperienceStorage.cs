using Azure;
using Azure.AI.OpenAI;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace Hazina.App.HazinaCoder.Core.Learning;

/// <summary>
/// Handles storage of experiences in Qdrant vector database
/// Generates embeddings and persists experiences for future retrieval
/// </summary>
public class ExperienceStorage
{
    private readonly QdrantClient _qdrant;
    private readonly OpenAIClient _openai;
    private const string COLLECTION_NAME = "hazinacoder_experiences";
    private const string EMBEDDING_MODEL = "text-embedding-3-small";
    private const int EMBEDDING_DIMENSION = 1536;

    public ExperienceStorage(QdrantClient qdrantClient, OpenAIClient openaiClient)
    {
        _qdrant = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
        _openai = openaiClient ?? throw new ArgumentNullException(nameof(openaiClient));
    }

    /// <summary>
    /// Initialize the Qdrant collection for experiences
    /// Call this once at startup
    /// </summary>
    public async Task InitializeCollectionAsync()
    {
        try
        {
            // Check if collection exists
            var collections = await _qdrant.ListCollectionsAsync();
            var collectionExists = collections.Any(c => c.Name == COLLECTION_NAME);

            if (!collectionExists)
            {
                Console.WriteLine($"Creating Qdrant collection: {COLLECTION_NAME}");

                // Create collection with cosine distance for embeddings
                await _qdrant.CreateCollectionAsync(
                    collectionName: COLLECTION_NAME,
                    vectorsConfig: new VectorParams
                    {
                        Size = EMBEDDING_DIMENSION,
                        Distance = Distance.Cosine
                    }
                );

                Console.WriteLine($"✅ Collection '{COLLECTION_NAME}' created successfully");
            }
            else
            {
                Console.WriteLine($"✅ Collection '{COLLECTION_NAME}' already exists");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not initialize Qdrant collection: {ex.Message}");
            Console.WriteLine("   Learning system will continue without vector storage");
        }
    }

    /// <summary>
    /// Store an experience in the vector database
    /// </summary>
    public async Task StoreExperienceAsync(Experience experience)
    {
        try
        {
            // Generate embedding for description + content
            var textToEmbed = $"{experience.Description}\n{experience.Content}";
            experience.Embedding = await GenerateEmbeddingAsync(textToEmbed);

            // Prepare payload for Qdrant
            var payload = new Dictionary<string, Value>
            {
                ["timestamp"] = experience.Timestamp.ToString("o"),
                ["type"] = experience.Type.ToString(),
                ["content"] = experience.Content,
                ["description"] = experience.Description,
                ["tags"] = string.Join(",", experience.Tags),

                // Context
                ["context_project"] = experience.Context.ProjectName ?? "",
                ["context_file"] = experience.Context.FileName ?? "",
                ["context_function"] = experience.Context.FunctionName ?? "",
                ["context_language"] = experience.Context.Language ?? "",
                ["context_framework"] = experience.Context.Framework ?? "",
                ["context_mode"] = experience.Context.Mode.ToString(),

                // Metadata
                ["importance"] = experience.Metadata.Importance,
                ["emotional_intensity"] = experience.Metadata.EmotionalIntensity,
                ["novelty"] = experience.Metadata.Novelty,
                ["retrieval_count"] = experience.Metadata.RetrievalCount,
                ["confidence"] = experience.Metadata.Confidence,

                // Outcome (if present)
                ["outcome_successful"] = experience.Outcome?.Successful ?? false,
                ["outcome_result"] = experience.Outcome?.Result ?? "",
                ["outcome_satisfaction"] = experience.Outcome?.Satisfaction ?? 0.0
            };

            // Store in Qdrant
            await _qdrant.UpsertAsync(
                collectionName: COLLECTION_NAME,
                points: new List<PointStruct>
                {
                    new()
                    {
                        Id = new PointId { Uuid = experience.Id.ToString() },
                        Vectors = experience.Embedding,
                        Payload = { payload }
                    }
                }
            );

            Console.WriteLine($"✅ Stored experience: {experience.Description}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not store experience: {ex.Message}");
            // Don't throw - gracefully degrade if storage fails
        }
    }

    /// <summary>
    /// Generate embedding vector for text using OpenAI
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

            // Return zero vector as fallback
            return new float[EMBEDDING_DIMENSION];
        }
    }

    /// <summary>
    /// Update metadata for an experience (e.g., increment retrieval count)
    /// </summary>
    public async Task UpdateMetadataAsync(Experience experience)
    {
        try
        {
            var payload = new Dictionary<string, Value>
            {
                ["retrieval_count"] = experience.Metadata.RetrievalCount,
                ["last_retrieved"] = experience.Metadata.LastRetrieved?.ToString("o") ?? ""
            };

            await _qdrant.SetPayloadAsync(
                collectionName: COLLECTION_NAME,
                payload: payload,
                pointsSelector: new PointsSelector
                {
                    Points = { new PointId { Uuid = experience.Id.ToString() } }
                }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not update metadata: {ex.Message}");
        }
    }

    /// <summary>
    /// Get total count of stored experiences
    /// </summary>
    public async Task<ulong> GetExperienceCountAsync()
    {
        try
        {
            var info = await _qdrant.GetCollectionInfoAsync(COLLECTION_NAME);
            return info.PointsCount ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Delete an experience by ID
    /// </summary>
    public async Task DeleteExperienceAsync(Guid id)
    {
        try
        {
            await _qdrant.DeleteAsync(
                collectionName: COLLECTION_NAME,
                pointsSelector: new PointsSelector
                {
                    Points = { new PointId { Uuid = id.ToString() } }
                }
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️  Warning: Could not delete experience: {ex.Message}");
        }
    }
}
