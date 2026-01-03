using Hazina.AI.Providers.Core;
using Hazina.Neurochain.Core;
using System.Text;
using System.Text.RegularExpressions;

namespace Hazina.AI.RAG.Core;

/// <summary>
/// Retrieval-Augmented Generation (RAG) engine
/// Combines vector search with AI generation for context-aware responses.
/// Supports metadata-first search with optional embeddings.
/// Supports composite scoring with tag relevance, recency, and position weighting.
/// </summary>
public class RAGEngine
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly IVectorStore? _vectorStore;
    private readonly IQueryableMetadataStore? _metadataStore;
    private readonly NeuroChainOrchestrator? _neurochain;
    private readonly RAGConfig _config;

    // Composite scoring components (optional, backwards compatible)
    private readonly ITagScoringService? _tagScoringService;
    private readonly ICompositeScorer? _compositeScorer;

    /// <summary>
    /// Create RAGEngine with vector store (backwards compatible).
    /// </summary>
    public RAGEngine(
        IProviderOrchestrator orchestrator,
        IVectorStore vectorStore,
        NeuroChainOrchestrator? neurochain = null,
        RAGConfig? config = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _neurochain = neurochain;
        _config = config ?? new RAGConfig();
    }

    /// <summary>
    /// Create RAGEngine with metadata store for metadata-first search.
    /// Vector store is optional when using metadata-only search.
    /// </summary>
    public RAGEngine(
        IProviderOrchestrator orchestrator,
        IQueryableMetadataStore metadataStore,
        IVectorStore? vectorStore = null,
        NeuroChainOrchestrator? neurochain = null,
        RAGConfig? config = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        _vectorStore = vectorStore;
        _neurochain = neurochain;
        _config = config ?? new RAGConfig();
    }

    /// <summary>
    /// Create RAGEngine with composite scoring support.
    /// This enables query-adaptive tag relevance scoring.
    /// </summary>
    public RAGEngine(
        IProviderOrchestrator orchestrator,
        IQueryableMetadataStore metadataStore,
        IVectorStore? vectorStore,
        ITagScoringService? tagScoringService,
        ICompositeScorer? compositeScorer,
        NeuroChainOrchestrator? neurochain = null,
        RAGConfig? config = null)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _metadataStore = metadataStore ?? throw new ArgumentNullException(nameof(metadataStore));
        _vectorStore = vectorStore;
        _tagScoringService = tagScoringService;
        _compositeScorer = compositeScorer ?? (tagScoringService != null ? new DefaultCompositeScorer() : null);
        _neurochain = neurochain;
        _config = config ?? new RAGConfig();
    }

    /// <summary>
    /// Query using RAG - retrieve relevant context and generate response.
    /// Supports both embedding-based and metadata-only search.
    /// </summary>
    public async Task<RAGResponse> QueryAsync(
        string query,
        RAGQueryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new RAGQueryOptions();

        var response = new RAGResponse
        {
            Query = query,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            // Determine search strategy
            if (options.UseEmbeddings && _vectorStore != null)
            {
                // Embedding-based search (original behavior)
                response.RetrievedDocuments = await RetrieveWithEmbeddingsAsync(
                    query, options, cancellationToken);
            }
            else if (_metadataStore != null)
            {
                // Metadata-only search (new capability)
                response.RetrievedDocuments = await RetrieveWithMetadataAsync(
                    query, options, cancellationToken);
            }
            else
            {
                throw new InvalidOperationException(
                    "No search backend available. Provide a vector store (for embeddings) " +
                    "or a queryable metadata store (for metadata search).");
            }

            // Apply composite scoring if enabled and available
            if (options.UseCompositeScoring && _compositeScorer != null)
            {
                response.RetrievedDocuments = await ApplyCompositeScoringAsync(
                    response.RetrievedDocuments, query, options, cancellationToken);
            }

            // Build context from retrieved documents
            var context = BuildContext(response.RetrievedDocuments, options.MaxContextLength);
            response.ContextUsed = context;

            // Generate response with context
            response.Answer = await GenerateResponseAsync(
                query,
                context,
                options,
                cancellationToken
            );

            response.Success = true;
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = ex.Message;
        }

        return response;
    }

    /// <summary>
    /// Retrieve documents using embedding-based vector search.
    /// </summary>
    private async Task<List<RetrievedDocument>> RetrieveWithEmbeddingsAsync(
        string query,
        RAGQueryOptions options,
        CancellationToken cancellationToken)
    {
        var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);
        return await RetrieveDocumentsAsync(
            queryEmbedding,
            options.TopK,
            options.MinSimilarity,
            cancellationToken
        );
    }

    /// <summary>
    /// Retrieve documents using metadata filtering and keyword search.
    /// No embeddings required.
    /// </summary>
    private async Task<List<RetrievedDocument>> RetrieveWithMetadataAsync(
        string query,
        RAGQueryOptions options,
        CancellationToken cancellationToken)
    {
        if (_metadataStore == null)
            throw new InvalidOperationException("Metadata store not configured");

        var filter = options.MetadataFilter ?? new MetadataFilter { Limit = options.TopK };
        var searchText = options.KeywordSearchText ?? query;

        // Use full-text search with optional metadata filter
        var results = await _metadataStore.SearchTextAsync(
            searchText,
            filter,
            options.TopK,
            cancellationToken
        );

        // Convert to RetrievedDocument format
        return results.Select((meta, index) => new RetrievedDocument
        {
            Id = meta.Id,
            Content = meta.SearchableText ?? meta.Summary ?? "",
            Similarity = CalculateKeywordRelevance(searchText, meta, index, results.Count),
            Metadata = new Dictionary<string, object>
            {
                ["source"] = meta.OriginalPath,
                ["mimeType"] = meta.MimeType,
                ["tags"] = meta.Tags,
                ["customMetadata"] = meta.CustomMetadata
            }
        }).ToList();
    }

    /// <summary>
    /// Calculate a relevance score for keyword search results.
    /// Uses position-based ranking since results are already ordered by relevance.
    /// </summary>
    private double CalculateKeywordRelevance(string searchText, DocumentMetadata meta, int position, int total)
    {
        // Base score from position (first result = highest score)
        double positionScore = 1.0 - (position / (double)Math.Max(total, 1));

        // Boost if search text appears in summary or searchable text
        var content = meta.SearchableText ?? meta.Summary ?? "";
        var matchCount = Regex.Matches(content, Regex.Escape(searchText), RegexOptions.IgnoreCase).Count;
        double matchBoost = Math.Min(matchCount * 0.1, 0.3);

        return Math.Min(positionScore + matchBoost, 1.0);
    }

    /// <summary>
    /// Index documents for retrieval
    /// </summary>
    public async Task<IndexingResult> IndexDocumentsAsync(
        List<Document> documents,
        CancellationToken cancellationToken = default)
    {
        var result = new IndexingResult
        {
            TotalDocuments = documents.Count,
            StartTime = DateTime.UtcNow
        };

        foreach (var doc in documents)
        {
            try
            {
                // Generate embedding
                var embedding = await GenerateEmbeddingAsync(doc.Content, cancellationToken);

                // Store in vector store
                await _vectorStore.AddAsync(
                    doc.Id,
                    embedding,
                    new Dictionary<string, object>
                    {
                        ["content"] = doc.Content,
                        ["metadata"] = doc.Metadata,
                        ["timestamp"] = DateTime.UtcNow
                    },
                    cancellationToken
                );

                result.IndexedDocuments++;
            }
            catch (Exception ex)
            {
                result.FailedDocuments++;
                result.Errors.Add($"{doc.Id}: {ex.Message}");
            }
        }

        result.EndTime = DateTime.UtcNow;
        result.Duration = result.EndTime - result.StartTime;
        result.Success = result.FailedDocuments == 0;

        return result;
    }

    /// <summary>
    /// Search documents by similarity
    /// </summary>
    public async Task<List<RetrievedDocument>> SearchAsync(
        string query,
        int topK = 5,
        double minSimilarity = 0.7,
        CancellationToken cancellationToken = default)
    {
        var embedding = await GenerateEmbeddingAsync(query, cancellationToken);
        return await RetrieveDocumentsAsync(embedding, topK, minSimilarity, cancellationToken);
    }

    #region Private Methods

    /// <summary>
    /// Apply composite scoring to retrieved documents.
    /// Uses tag relevance, recency, and position to re-rank results.
    /// </summary>
    private async Task<List<RetrievedDocument>> ApplyCompositeScoringAsync(
        List<RetrievedDocument> documents,
        string query,
        RAGQueryOptions options,
        CancellationToken cancellationToken)
    {
        if (_compositeScorer == null || documents.Count == 0)
        {
            return documents;
        }

        // Collect all tags from documents
        var allTags = documents
            .Where(d => d.Metadata.TryGetValue("tags", out var tags) && tags is IEnumerable<string>)
            .SelectMany(d => (IEnumerable<string>)d.Metadata["tags"])
            .Distinct()
            .ToList();

        // Get or compute tag scores
        TagRelevanceIndex? tagIndex = null;
        if (_tagScoringService != null && allTags.Any())
        {
            var scoringContext = options.TagScoringContext ?? query;
            tagIndex = await _tagScoringService.GetOrComputeScoresAsync(
                allTags, scoringContext, options.TagScoreCacheAge, cancellationToken);
        }

        // Convert to ScoredDocument format
        var scoredDocs = documents.Select((doc, index) =>
        {
            var metadata = ConvertToDocumentMetadata(doc);
            return new ScoredDocument
            {
                Id = doc.Id,
                Content = doc.Content,
                Similarity = doc.Similarity,
                Metadata = metadata
            };
        }).ToList();

        // Apply composite scoring
        var scoringOptions = options.ScoringOptions ?? ScoringOptions.Default;
        var rankedDocs = await _compositeScorer.ScoreAndRankAsync(
            scoredDocs, tagIndex, scoringOptions, cancellationToken);

        // Convert back to RetrievedDocument format
        return rankedDocs.Select(sd => new RetrievedDocument
        {
            Id = sd.Id,
            Content = sd.Content,
            Similarity = sd.CompositeScore, // Use composite score as the new similarity
            Metadata = (sd.Metadata?.CustomMetadata ?? new Dictionary<string, string>())
                .ToDictionary(kvp => kvp.Key, kvp => (object)kvp.Value)
                .Concat(new Dictionary<string, object>
                {
                    ["originalSimilarity"] = sd.Similarity,
                    ["tagScore"] = sd.TagScore,
                    ["recencyScore"] = sd.RecencyScore,
                    ["positionScore"] = sd.PositionScore,
                    ["compositeScore"] = sd.CompositeScore,
                    ["scoreBreakdown"] = sd.ScoreBreakdown
                })
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
        }).ToList();
    }

    /// <summary>
    /// Convert RetrievedDocument to DocumentMetadata for scoring.
    /// </summary>
    private DocumentMetadata ConvertToDocumentMetadata(RetrievedDocument doc)
    {
        var metadata = new DocumentMetadata
        {
            Id = doc.Id,
            SearchableText = doc.Content
        };

        if (doc.Metadata.TryGetValue("tags", out var tags) && tags is IEnumerable<string> tagList)
        {
            metadata.Tags = tagList.ToList();
        }

        if (doc.Metadata.TryGetValue("created", out var created) && created is DateTime createdDate)
        {
            metadata.Created = createdDate;
        }

        if (doc.Metadata.TryGetValue("source", out var source))
        {
            metadata.OriginalPath = source?.ToString() ?? "";
        }

        if (doc.Metadata.TryGetValue("mimeType", out var mimeType))
        {
            metadata.MimeType = mimeType?.ToString() ?? "";
        }

        return metadata;
    }

    private async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var embedding = await _orchestrator.GenerateEmbedding(text);
        return embedding.Select(d => (float)d).ToArray();
    }

    private async Task<List<RetrievedDocument>> RetrieveDocumentsAsync(
        float[] queryEmbedding,
        int topK,
        double minSimilarity,
        CancellationToken cancellationToken)
    {
        var results = await _vectorStore.SearchAsync(
            queryEmbedding,
            topK,
            cancellationToken
        );

        return results
            .Where(r => r.Similarity >= minSimilarity)
            .Select(r => new RetrievedDocument
            {
                Id = r.Id,
                Content = r.Metadata.TryGetValue("content", out var content) ? content.ToString() ?? "" : "",
                Similarity = r.Similarity,
                Metadata = r.Metadata.TryGetValue("metadata", out var meta) && meta is Dictionary<string, object> metaDict
                    ? metaDict
                    : new Dictionary<string, object>()
            })
            .ToList();
    }

    private string BuildContext(List<RetrievedDocument> documents, int maxLength)
    {
        var sb = new StringBuilder();
        int currentLength = 0;

        foreach (var doc in documents.OrderByDescending(d => d.Similarity))
        {
            if (currentLength + doc.Content.Length > maxLength)
                break;

            sb.AppendLine($"[Relevance: {doc.Similarity:P0}]");
            sb.AppendLine(doc.Content);
            sb.AppendLine();

            currentLength += doc.Content.Length;
        }

        return sb.ToString();
    }

    private async Task<string> GenerateResponseAsync(
        string query,
        string context,
        RAGQueryOptions options,
        CancellationToken cancellationToken)
    {
        var prompt = BuildRAGPrompt(query, context, options);

        if (_neurochain != null && options.UseNeurochain)
        {
            var result = await _neurochain.ReasonAsync(
                prompt,
                new ReasoningContext
                {
                    MinConfidence = options.MinConfidence,
                    Domain = "RAG - Information Retrieval"
                },
                cancellationToken
            );
            return result.FinalAnswer;
        }
        else
        {
            var messages = new List<HazinaChatMessage>
            {
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.System,
                    Text = "You are a helpful assistant. Answer questions based on the provided context. If the context doesn't contain relevant information, say so."
                },
                new HazinaChatMessage
                {
                    Role = HazinaMessageRole.User,
                    Text = prompt
                }
            };

            var response = await _orchestrator.GetResponse(
                messages,
                HazinaChatResponseFormat.Text,
                null,
                null,
                cancellationToken
            );

            return response.Result;
        }
    }

    private string BuildRAGPrompt(string query, string context, RAGQueryOptions options)
    {
        var sb = new StringBuilder();

        if (!string.IsNullOrEmpty(context))
        {
            sb.AppendLine("Context:");
            sb.AppendLine(context);
            sb.AppendLine();
        }

        sb.AppendLine($"Question: {query}");

        if (options.RequireCitation)
        {
            sb.AppendLine();
            sb.AppendLine("Provide citations for your answer using [1], [2], etc. to reference the context.");
        }

        return sb.ToString();
    }

    #endregion
}

/// <summary>
/// RAG configuration
/// </summary>
public class RAGConfig
{
    public int DefaultTopK { get; set; } = 5;
    public double DefaultMinSimilarity { get; set; } = 0.7;
    public int DefaultMaxContextLength { get; set; } = 4000;
}

/// <summary>
/// RAG query options
/// </summary>
public class RAGQueryOptions
{
    public int TopK { get; set; } = 5;
    public double MinSimilarity { get; set; } = 0.7;
    public int MaxContextLength { get; set; } = 4000;
    public bool UseNeurochain { get; set; } = false;
    public double MinConfidence { get; set; } = 0.8;
    public bool RequireCitation { get; set; } = false;

    /// <summary>
    /// Whether to use embeddings for semantic search.
    /// Default: true (backwards compatible).
    /// When false, uses metadata filtering and keyword search only.
    /// </summary>
    public bool UseEmbeddings { get; set; } = true;

    /// <summary>
    /// Optional metadata filter to apply before embedding search.
    /// When UseEmbeddings=false, this is the primary filter.
    /// When UseEmbeddings=true, this pre-filters before vector search.
    /// </summary>
    public MetadataFilter? MetadataFilter { get; set; }

    /// <summary>
    /// Text to search for when UseEmbeddings=false (keyword search).
    /// If null, uses the query text.
    /// </summary>
    public string? KeywordSearchText { get; set; }

    // --- Composite Scoring Options (new, backwards compatible) ---

    /// <summary>
    /// Whether to apply composite scoring to retrieved documents.
    /// Default: false (backwards compatible).
    /// When true, re-ranks results using tag relevance, recency, and position.
    /// </summary>
    public bool UseCompositeScoring { get; set; } = false;

    /// <summary>
    /// Configuration for composite scoring weights.
    /// If null, uses ScoringOptions.Default.
    /// </summary>
    public ScoringOptions? ScoringOptions { get; set; }

    /// <summary>
    /// Context to use for tag scoring (e.g., system instruction or task description).
    /// If null, uses the query text.
    /// </summary>
    public string? TagScoringContext { get; set; }

    /// <summary>
    /// Maximum age of cached tag scores to use.
    /// If null, always uses cached scores regardless of age.
    /// </summary>
    public TimeSpan? TagScoreCacheAge { get; set; }
}

/// <summary>
/// RAG response
/// </summary>
public class RAGResponse
{
    public string Query { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Answer { get; set; } = string.Empty;
    public List<RetrievedDocument> RetrievedDocuments { get; set; } = new();
    public string ContextUsed { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Document for indexing
/// </summary>
public class Document
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Retrieved document with similarity score
/// </summary>
public class RetrievedDocument
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Indexing result
/// </summary>
public class IndexingResult
{
    public int TotalDocuments { get; set; }
    public int IndexedDocuments { get; set; }
    public int FailedDocuments { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public TimeSpan Duration { get; set; }
    public bool Success { get; set; }
}

/// <summary>
/// Vector store interface (simplified)
/// </summary>
public interface IVectorStore
{
    Task AddAsync(string id, float[] embedding, Dictionary<string, object> metadata, CancellationToken cancellationToken);
    Task<List<VectorSearchResult>> SearchAsync(float[] queryEmbedding, int topK, CancellationToken cancellationToken);
}

/// <summary>
/// Vector search result
/// </summary>
public class VectorSearchResult
{
    public string Id { get; set; } = string.Empty;
    public double Similarity { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
