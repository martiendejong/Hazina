public interface IChunkStore
{
    public Task<bool> Store(string name, IEnumerable<string> chunkKeys);
    public Task<IEnumerable<string>> Get(string name);
    public Task<bool> Remove(string name, IEnumerable<string> chunkKeys);
    public Task<IEnumerable<string>> ListNames();

    /// <summary>
    /// Gets the parent document key for a given chunk key
    /// </summary>
    /// <param name="chunkKey">The chunk key (e.g., "doc.txt chunk 0" or "doc.txt")</param>
    /// <returns>The parent document key, or null if not found</returns>
    public Task<string?> GetParentDocument(string chunkKey);
}

