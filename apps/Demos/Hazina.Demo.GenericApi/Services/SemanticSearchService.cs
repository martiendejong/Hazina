using Hazina.Demo.GenericApi.Data;
using Hazina.Demo.GenericApi.Entities;
using Hazina.Store.EmbeddingStore;
using Microsoft.EntityFrameworkCore;

namespace Hazina.Demo.GenericApi.Services;

/// <summary>
/// Semantic search service that uses vector embeddings for similarity search.
/// </summary>
public class SemanticSearchService : ISemanticSearchService
{
    private readonly DemoDbContext _dbContext;
    private readonly IEmbeddingGenerator _embeddingGenerator;
    private readonly ILogger<SemanticSearchService> _logger;

    public SemanticSearchService(
        DemoDbContext dbContext,
        IEmbeddingGenerator embeddingGenerator,
        ILogger<SemanticSearchService> logger)
    {
        _dbContext = dbContext;
        _embeddingGenerator = embeddingGenerator;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<List<SearchHit>> SearchAsync(string query, int topK = 10, float minScore = 0.5f)
    {
        // Generate embedding for the query
        var queryEmbedding = await GetEmbeddingAsync(query);

        // Get all documents that have embeddings
        var documents = await _dbContext.Documents
            .Where(d => !d.IsDeleted && d.Embedding != null)
            .ToListAsync().ConfigureAwait(false);

        if (documents.Count == 0)
        {
            _logger.LogWarning("No documents with embeddings found. Run embed-all first.");
            return new List<SearchHit>();
        }

        // Calculate similarity scores
        var results = documents
            .Select(doc => new SearchHit
            {
                Document = doc,
                Score = CosineSimilarity(queryEmbedding, doc.Embedding!)
            })
            .Where(hit => hit.Score >= minScore)
            .OrderByDescending(hit => hit.Score)
            .Take(topK)
            .ToList();

        _logger.LogInformation(
            "Semantic search for '{Query}' found {Count} results above {MinScore}",
            query, results.Count, minScore);

        return results;
    }

    /// <inheritdoc/>
    public async Task EmbedDocumentAsync(Document document)
    {
        var text = document.GetSearchableText();
        var embedding = await _embeddingGenerator.GenerateAsync(text).ConfigureAwait(false);

        // Convert from Embedding (List<double>) to float[]
        document.Embedding = embedding.Select(d => (float)d).ToArray();
        document.EmbeddingComputedAt = DateTime.UtcNow;

        _logger.LogInformation("Generated embedding for document {Id} ({Dimensions} dimensions)",
            document.Id, embedding.Count);
    }

    /// <inheritdoc/>
    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        var embedding = await _embeddingGenerator.GenerateAsync(text).ConfigureAwait(false);
        return embedding.Select(d => (float)d).ToArray();
    }

    /// <summary>
    /// Calculate cosine similarity between two vectors.
    /// Returns value between -1 and 1, where 1 means identical.
    /// </summary>
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
        {
            throw new ArgumentException("Vectors must have the same length", nameof(b));
        }

        float dotProduct = 0;
        float normA = 0;
        float normB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        if (denominator == 0)
        {
            return 0;
        }

        return (float)(dotProduct / denominator);
    }
}

/// <summary>
/// Mock embedding generator for demo/testing.
/// Generates deterministic embeddings based on text content.
/// For production, use OpenAIEmbeddingGenerator.
/// </summary>
public class MockEmbeddingGenerator : IEmbeddingGenerator
{
    private const int EmbeddingDimensions = 1536; // Same as OpenAI text-embedding-ada-002

    public int Dimensions => EmbeddingDimensions;

    public Task<Embedding> GenerateAsync(string text, CancellationToken cancellationToken = default)
    {
        // Generate a deterministic "embedding" based on text characteristics
        // This is just for demo - real embeddings capture semantic meaning
        var vector = new double[EmbeddingDimensions];

        // Use hash code for reproducibility
        var hash = text.GetHashCode();
        var random = new Random(hash);

        // Create normalized random vector
        double sum = 0;
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            vector[i] = random.NextDouble() * 2 - 1;
            sum += vector[i] * vector[i];
        }

        // Normalize to unit vector
        var norm = Math.Sqrt(sum);
        for (int i = 0; i < EmbeddingDimensions; i++)
        {
            vector[i] /= norm;
        }

        return Task.FromResult(new Embedding(vector));
    }

    public Task<List<Embedding>> GenerateBatchAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        var results = new List<Embedding>();
        foreach (var text in texts)
        {
            results.Add(GenerateAsync(text, cancellationToken).GetAwaiter().GetResult());
        }

        return Task.FromResult(results);
    }
}
