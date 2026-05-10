namespace Hazina.Store.Faiss;

/// <summary>
/// Extension point for plugging in a native FAISS index backend.
/// The default <see cref="FaissEmbeddingStore"/> uses an in-memory brute-force
/// implementation. Provide a custom <see cref="IFaissIndexBackend"/> to delegate
/// ANN queries to a native FAISS library (e.g. via P/Invoke or a gRPC sidecar).
/// </summary>
public interface IFaissIndexBackend
{
    /// <summary>
    /// Adds or updates a vector in the FAISS index.
    /// </summary>
    /// <param name="key">Unique identifier for the vector.</param>
    /// <param name="vector">The float vector to index.</param>
    void Upsert(string key, float[] vector);

    /// <summary>
    /// Removes a vector from the FAISS index by its key.
    /// </summary>
    /// <param name="key">Unique identifier for the vector to remove.</param>
    /// <returns><c>true</c> if removed; <c>false</c> if not found.</returns>
    bool Remove(string key);

    /// <summary>
    /// Searches the index for the nearest neighbours to <paramref name="query"/>.
    /// </summary>
    /// <param name="query">The float query vector.</param>
    /// <param name="topK">Maximum number of results to return.</param>
    /// <returns>
    /// A sequence of (key, similarity) pairs ordered by descending similarity.
    /// </returns>
    IEnumerable<(string key, float similarity)> Search(float[] query, int topK);

    /// <summary>
    /// Saves the index to the specified path (optional; no-op for in-memory backends).
    /// </summary>
    /// <param name="path">File system path or URI for persistence.</param>
    void Save(string path);

    /// <summary>
    /// Loads the index from the specified path (optional; no-op for in-memory backends).
    /// </summary>
    /// <param name="path">File system path or URI to load from.</param>
    void Load(string path);
}
