using Hazina.AI.RAG.Embeddings;
using Hazina.LLMs;
using Hazina.Store.EmbeddingStore;

namespace Hazina.AI.RAG.Extensions;

/// <summary>
/// Extension methods to add semantic chunking support to DocumentStore
/// Phase 4: Semantic chunking integration
///
/// Usage:
/// <code>
/// var options = new TextChunkingOptions
/// {
///     Strategy = ChunkingStrategy.Semantic,
///     SemanticOptions = new SemanticChunkingOptions { ... }
/// };
///
/// await documentStore.StoreWithSemanticChunking(
///     name: "my-document",
///     content: text,
///     embeddingGenerator: embeddingGenerator,
///     chunkingOptions: options
/// );
/// </code>
/// </summary>
public static class DocumentStoreSemanticExtensions
{
    /// <summary>
    /// Store a document using semantic chunking
    /// </summary>
    /// <param name="documentStore">The document store</param>
    /// <param name="name">Document name/key</param>
    /// <param name="content">Document content</param>
    /// <param name="embeddingGenerator">Embedding generator for semantic chunking</param>
    /// <param name="chunkingOptions">Chunking options (optional, defaults to Semantic strategy)</param>
    /// <param name="llmClient">LLM client for metadata extraction (optional)</param>
    /// <param name="metadata">Document metadata</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if successful</returns>
    public static async Task<bool> StoreWithSemanticChunking(
        this DocumentStore documentStore,
        string name,
        string content,
        IEmbeddingGenerator embeddingGenerator,
        TextChunkingOptions? chunkingOptions = null,
        ILLMClient? llmClient = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        // Default to semantic chunking if not specified
        var options = chunkingOptions ?? new TextChunkingOptions
        {
            Strategy = ChunkingStrategy.Semantic,
            SemanticOptions = new SemanticChunkingOptions()
        };

        // Create text chunker
        var textChunker = llmClient != null
            ? new TextChunker(embeddingGenerator, llmClient, options)
            : new TextChunker(embeddingGenerator, options);

        // Chunk the content
        var chunks = await textChunker.ChunkTextAsync(
            content,
            options,
            new Dictionary<string, object>
            {
                ["document_id"] = name,
                ["document_name"] = name
            },
            ct
        );

        // Store metadata
        var docMetadata = new DocumentMetadata
        {
            Id = name,
            OriginalPath = "",
            MimeType = "text/plain",
            Size = content.Length,
            Created = DateTime.UtcNow,
            CustomMetadata = metadata ?? new Dictionary<string, string>(),
            IsBinary = false,
            Summary = null
        };
        await documentStore.MetadataStore.Store(name, docMetadata);

        var chunkKeys = new List<string>();

        // Store metadata as searchable chunk
        var metadataKey = $"{name}.metadata";
        var metadataChunk = docMetadata.ToChunkText();
        await documentStore.EmbeddingStore.StoreEmbedding(metadataKey, metadataChunk);
        await documentStore.TextStore.Store(metadataKey, metadataChunk);
        chunkKeys.Add(metadataKey);

        // Store semantic chunks
        if (chunks.Count == 1)
        {
            var chunk = chunks[0];
            await StoreChunkWithMetadata(
                documentStore,
                embeddingGenerator,
                name,
                chunk.Text,
                chunk.Metadata,
                ct
            );
            chunkKeys.Add(name);
        }
        else
        {
            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var chunkKey = $"{name} chunk {i}";
                await StoreChunkWithMetadata(
                    documentStore,
                    embeddingGenerator,
                    chunkKey,
                    chunk.Text,
                    chunk.Metadata,
                    ct
                );
                chunkKeys.Add(chunkKey);
            }
        }

        await documentStore.ChunkStore.Store(name, chunkKeys);
        return true;
    }

    /// <summary>
    /// Store a chunk with its semantic metadata
    /// Note: Currently stores chunks using legacy architecture (embeddings without metadata).
    /// Future enhancement: Store metadata with embeddings when vector store supports it.
    /// </summary>
    private static async Task StoreChunkWithMetadata(
        DocumentStore documentStore,
        IEmbeddingGenerator embeddingGenerator,
        string chunkKey,
        string chunkText,
        Dictionary<string, object> chunkMetadata,
        CancellationToken ct = default)
    {
        // Store the text
        await documentStore.TextStore.Store(chunkKey, chunkText);

        // Store embedding
        // TODO: When vector store supports metadata, include chunkMetadata in the embedding
        await documentStore.EmbeddingStore.StoreEmbedding(chunkKey, chunkText);
    }

    /// <summary>
    /// Store binary document with semantic chunking
    /// </summary>
    public static async Task<bool> StoreWithSemanticChunking(
        this DocumentStore documentStore,
        string name,
        byte[] content,
        string mimeType,
        IEmbeddingGenerator embeddingGenerator,
        TextChunkingOptions? chunkingOptions = null,
        ILLMClient? llmClient = null,
        Dictionary<string, string>? metadata = null,
        CancellationToken ct = default)
    {
        // Extract text from binary content
        var binaryProcessor = documentStore.BinaryProcessor;
        var textContent = await binaryProcessor.ExtractText(content, mimeType);
        var summary = binaryProcessor.IsBinary(mimeType)
            ? await binaryProcessor.GenerateSummary(content, mimeType)
            : null;

        var contentToStore = string.IsNullOrEmpty(summary)
            ? textContent
            : $"{summary}\n\nExtracted content:\n{textContent}";

        // Update metadata
        var updatedMetadata = metadata ?? new Dictionary<string, string>();
        updatedMetadata["mime_type"] = mimeType;
        updatedMetadata["is_binary"] = "true";

        // Use text variant with extracted content
        return await StoreWithSemanticChunking(
            documentStore,
            name,
            contentToStore,
            embeddingGenerator,
            chunkingOptions,
            llmClient,
            updatedMetadata,
            ct
        );
    }
}
