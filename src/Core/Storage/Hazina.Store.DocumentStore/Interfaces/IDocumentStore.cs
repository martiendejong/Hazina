using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

public interface IDocumentStore
{
    public string Name { get; set; }
    public ITextEmbeddingStore EmbeddingStore { get; }
    public ITextStore TextStore { get; set; }
    public string GetPath(string id);
    public Task<string> Get(string id);

    /// <summary>
    /// Gets a specific chunk by its chunk key
    /// </summary>
    /// <param name="chunkKey">The chunk key (e.g., "doc.txt part 0")</param>
    /// <returns>The chunk text, or null if not found</returns>
    public Task<string?> GetChunk(string chunkKey);

    /// <summary>
    /// Gets a document with all its metadata and chunk keys
    /// </summary>
    /// <param name="key">The document key</param>
    /// <returns>DocumentWithChunks object containing the full document, metadata, and chunk keys</returns>
    public Task<DocumentWithChunks?> GetDocumentWithChunks(string key);

    /// <summary>
    /// Adds a text document to the document store under the given key/name
    /// </summary>
    /// <param name="id">The key under which to store the document</param>
    /// <param name="content">Text content to store</param>
    /// <param name="metadata">Optional metadata for the document</param>
    /// <param name="split">Whether to split the document into chunks</param>
    /// <returns></returns>
    public Task<bool> Store(string id, string content, Dictionary<string, string>? metadata = null, bool split = true);

    /// <summary>
    /// Adds a binary document to the document store
    /// </summary>
    /// <param name="id">The key under which to store the document</param>
    /// <param name="content">Binary content to store</param>
    /// <param name="mimeType">MIME type of the binary content</param>
    /// <param name="metadata">Optional metadata for the document</param>
    /// <returns></returns>
    public Task<bool> Store(string id, byte[] content, string mimeType, Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Adds a document from a file path
    /// </summary>
    /// <param name="id">The key under which to store the document</param>
    /// <param name="filePath">Path to the file to store</param>
    /// <param name="metadata">Optional metadata for the document</param>
    /// <returns></returns>
    public Task<bool> StoreFromFile(string id, string filePath, Dictionary<string, string>? metadata = null);

    /// <summary>
    /// Gets metadata for a document
    /// </summary>
    /// <param name="id">Document ID</param>
    /// <returns>Document metadata or null if not found</returns>
    public Task<DocumentMetadata?> GetMetadata(string id);
    public Task<bool> Embed(string key);
    public Task<bool> Remove(string id);
    public Task<bool> Move(string name, string newName, bool split = true);
    public Task<List<TreeNode<string>>> Tree();
    public Task<List<string>> List(string folder = "", bool recursive = false);
    Task UpdateEmbeddings();
    Task<List<string>> RelevantItems(string query);
    Task<List<RelevantEmbedding>> Embeddings(string relevancyQuery);
}
