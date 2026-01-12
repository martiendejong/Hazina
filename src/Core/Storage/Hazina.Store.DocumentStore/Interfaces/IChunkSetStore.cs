using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Interface for storing and retrieving chunk sets.
/// Chunk sets represent logical groupings of chunks with collective summaries.
/// </summary>
public interface IChunkSetStore
{
    /// <summary>
    /// Store chunk sets for a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <param name="chunkSets">Collection of chunk sets to store.</param>
    /// <returns>True if successful.</returns>
    Task<bool> StoreAsync(string documentId, IEnumerable<ChunkSet> chunkSets);

    /// <summary>
    /// Get all chunk sets for a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <returns>Collection of chunk sets, or empty if none exist.</returns>
    Task<IEnumerable<ChunkSet>> GetAsync(string documentId);

    /// <summary>
    /// Get a specific chunk set by ID.
    /// </summary>
    /// <param name="setId">The chunk set ID (e.g., "doc.txt:section:0").</param>
    /// <returns>The chunk set, or null if not found.</returns>
    Task<ChunkSet?> GetByIdAsync(string setId);

    /// <summary>
    /// Remove all chunk sets for a document.
    /// </summary>
    /// <param name="documentId">The document ID.</param>
    /// <returns>True if successful.</returns>
    Task<bool> RemoveAsync(string documentId);

    /// <summary>
    /// List all document IDs that have chunk sets.
    /// </summary>
    /// <returns>Collection of document IDs.</returns>
    Task<IEnumerable<string>> ListDocumentIdsAsync();
}
