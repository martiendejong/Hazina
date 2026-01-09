using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

/// <summary>
/// Interface for managing a document store with RAG (Retrieval-Augmented Generation) capabilities.
/// </summary>
/// <remarks>
/// <para>
/// A document store combines three underlying stores:
/// <list type="bullet">
///   <item><description><see cref="TextStore"/> - Stores the actual document content (files)</description></item>
///   <item><description><see cref="EmbeddingStore"/> - Stores vector embeddings for semantic search</description></item>
///   <item><description>Parts Store - Stores document chunks for efficient retrieval</description></item>
/// </list>
/// </para>
/// <para>
/// Documents are automatically split into chunks and embedded for semantic search when stored.
/// Use <see cref="RelevantItems"/> to find documents relevant to a query.
/// </para>
/// </remarks>
/// <example>
/// Basic usage:
/// <code>
/// // Store a document
/// await store.Store("readme.md", "# My Project\n\nThis is my project...");
///
/// // Update embeddings
/// await store.UpdateEmbeddings();
///
/// // Find relevant documents
/// var relevant = await store.RelevantItems("how to install");
/// foreach (var key in relevant)
/// {
///     var content = await store.Get(key);
///     Console.WriteLine($"Found: {key}");
/// }
/// </code>
/// </example>
public interface IDocumentStore
{
    /// <summary>
    /// Gets or sets the name of this document store.
    /// </summary>
    /// <remarks>
    /// The name is used to identify the store in multi-store configurations and agent setups.
    /// </remarks>
    string Name { get; set; }

    /// <summary>
    /// Gets the underlying embedding store used for vector similarity search.
    /// </summary>
    ITextEmbeddingStore EmbeddingStore { get; }

    /// <summary>
    /// Gets or sets the underlying text store where document content is persisted.
    /// </summary>
    ITextStore TextStore { get; set; }

    /// <summary>
    /// Gets the file system path for a document by its key.
    /// </summary>
    /// <param name="id">The document key.</param>
    /// <returns>The full file path to the document.</returns>
    string GetPath(string id);

    /// <summary>
    /// Retrieves the content of a document by its key.
    /// </summary>
    /// <param name="id">The document key.</param>
    /// <returns>The document content as a string.</returns>
    /// <exception cref="FileNotFoundException">Thrown if the document doesn't exist.</exception>
    Task<string> Get(string id);

    /// <summary>
    /// Gets a specific chunk by its chunk key.
    /// </summary>
    /// <param name="chunkKey">The chunk key (e.g., "doc.txt part 0").</param>
    /// <returns>The chunk text, or null if not found.</returns>
    /// <remarks>
    /// Documents are split into chunks for better semantic search. Each chunk has a key
    /// in the format "{documentKey} part {chunkIndex}".
    /// </remarks>
    Task<string?> GetChunk(string chunkKey);

    /// <summary>
    /// Gets a document with all its metadata and chunk keys.
    /// </summary>
    /// <param name="key">The document key.</param>
    /// <returns>A <see cref="DocumentWithChunks"/> object containing the full document, metadata, and chunk keys.</returns>
    Task<DocumentWithChunks?> GetDocumentWithChunks(string key);

    /// <summary>
    /// Adds a text document to the document store.
    /// </summary>
    /// <param name="id">The key under which to store the document.</param>
    /// <param name="content">Text content to store.</param>
    /// <param name="metadata">Optional metadata for the document (e.g., author, date, tags).</param>
    /// <param name="split">Whether to split the document into chunks for RAG. Default is true.</param>
    /// <returns>True if the document was stored successfully.</returns>
    /// <remarks>
    /// After storing, call <see cref="UpdateEmbeddings"/> or <see cref="Embed"/> to generate vector embeddings.
    /// </remarks>
    Task<bool> Store(string id, string content, Dictionary<string, string>? metadata = null, bool split = true);

    /// <summary>
    /// Adds a binary document to the document store.
    /// </summary>
    /// <param name="id">The key under which to store the document.</param>
    /// <param name="content">Binary content to store.</param>
    /// <param name="mimeType">MIME type of the binary content (e.g., "application/pdf", "image/png").</param>
    /// <param name="metadata">Optional metadata for the document.</param>
    /// <returns>True if the document was stored successfully.</returns>
    Task<bool> Store(string id, byte[] content, string mimeType, Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Adds a document from a file path.
    /// </summary>
    /// <param name="id">The key under which to store the document.</param>
    /// <param name="filePath">Path to the file to store.</param>
    /// <param name="metadata">Optional metadata for the document.</param>
    /// <returns>True if the document was stored successfully.</returns>
    Task<bool> StoreFromFile(string id, string filePath, Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Gets metadata for a document.
    /// </summary>
    /// <param name="id">Document ID.</param>
    /// <returns>Document metadata or null if not found.</returns>
    Task<DocumentMetadata?> GetMetadata(string id);

    /// <summary>
    /// Generates and stores the vector embedding for a single document.
    /// </summary>
    /// <param name="key">The document key to embed.</param>
    /// <returns>True if embedding was successful.</returns>
    /// <remarks>
    /// For bulk embedding, use <see cref="UpdateEmbeddings"/> instead.
    /// </remarks>
    Task<bool> Embed(string key);

    /// <summary>
    /// Removes a document from the store.
    /// </summary>
    /// <param name="id">The document key to remove.</param>
    /// <returns>True if the document was removed successfully.</returns>
    /// <remarks>
    /// This removes the document content, chunks, and embeddings.
    /// </remarks>
    Task<bool> Remove(string id);

    /// <summary>
    /// Moves/renames a document within the store.
    /// </summary>
    /// <param name="name">The current document key.</param>
    /// <param name="newName">The new document key.</param>
    /// <param name="split">Whether to re-split the document after moving.</param>
    /// <returns>True if the move was successful.</returns>
    Task<bool> Move(string name, string newName, bool split = true);

    /// <summary>
    /// Returns the document hierarchy as a tree structure.
    /// </summary>
    /// <returns>A list of tree nodes representing the folder/file structure.</returns>
    Task<List<TreeNode<string>>> Tree();

    /// <summary>
    /// Lists all documents in the store, optionally filtered by folder.
    /// </summary>
    /// <param name="folder">Optional folder path to filter by.</param>
    /// <param name="recursive">If true, includes documents in subfolders.</param>
    /// <returns>A list of document keys.</returns>
    Task<List<string>> List(string folder = "", bool recursive = false);

    /// <summary>
    /// Updates embeddings for all documents that have changed or are missing embeddings.
    /// </summary>
    /// <returns>A task that completes when all embeddings are updated.</returns>
    /// <remarks>
    /// Call this after adding or modifying documents to ensure the semantic search index is current.
    /// Only documents that have changed since their last embedding are re-embedded.
    /// </remarks>
    Task UpdateEmbeddings();

    /// <summary>
    /// Finds documents relevant to the given query using semantic similarity.
    /// </summary>
    /// <param name="query">The search query (natural language).</param>
    /// <returns>A list of document keys ordered by relevance (most relevant first).</returns>
    /// <example>
    /// <code>
    /// var results = await store.RelevantItems("how to authenticate users");
    /// foreach (var key in results.Take(5))
    /// {
    ///     Console.WriteLine($"Relevant: {key}");
    /// }
    /// </code>
    /// </example>
    Task<List<string>> RelevantItems(string query);

    /// <summary>
    /// Gets embeddings with similarity scores for a query.
    /// </summary>
    /// <param name="relevancyQuery">The search query.</param>
    /// <returns>A list of <see cref="RelevantEmbedding"/> objects with similarity scores.</returns>
    /// <remarks>
    /// Use this instead of <see cref="RelevantItems"/> when you need the similarity scores
    /// or want more control over the results.
    /// </remarks>
    Task<List<RelevantEmbedding>> Embeddings(string relevancyQuery);
}
