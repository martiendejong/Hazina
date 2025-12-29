using Hazina.AI.Providers.Core;
using Hazina.Neurochain.Core;
using System.Text;

namespace Hazina.AI.RAG.Core;

/// <summary>
/// Retrieval-Augmented Generation (RAG) engine
/// Combines vector search with AI generation for context-aware responses
/// </summary>
public class RAGEngine
{
    private readonly IProviderOrchestrator _orchestrator;
    private readonly IVectorStore _vectorStore;
    private readonly NeuroChainOrchestrator? _neurochain;
    private readonly RAGConfig _config;

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
    /// Query using RAG - retrieve relevant context and generate response
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
            // Step 1: Generate embedding for query
            var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);

            // Step 2: Retrieve relevant documents
            response.RetrievedDocuments = await RetrieveDocumentsAsync(
                queryEmbedding,
                options.TopK,
                options.MinSimilarity,
                cancellationToken
            );

            // Step 3: Build context from retrieved documents
            var context = BuildContext(response.RetrievedDocuments, options.MaxContextLength);
            response.ContextUsed = context;

            // Step 4: Generate response with context
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
