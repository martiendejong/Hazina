using System.Linq;

public class ChunkMemoryStore : IChunkStore
{
    public Dictionary<string, string[]> Chunks = new Dictionary<string, string[]>();

    public async Task<bool> Store(string name, IEnumerable<string> chunkKeys)
    {
        Chunks[name] = chunkKeys.ToArray();
        return true;
    }

    public async Task<IEnumerable<string>> Get(string name)
    {
        return Chunks[name];
    }

    public async Task<bool> Remove(string name, IEnumerable<string> chunkKeys)
    {
        Chunks.Remove(name);
        return true;
    }

    public async Task<IEnumerable<string>> ListNames()
    {
        return Chunks.Keys.ToArray();
    }

    public async Task<string?> GetParentDocument(string chunkKey)
    {
        // First check if the chunk key itself is a document
        if (Chunks.ContainsKey(chunkKey))
        {
            return chunkKey;
        }

        // Otherwise, search through all documents to find which one contains this chunk
        foreach (var kvp in Chunks)
        {
            if (kvp.Value.Contains(chunkKey))
            {
                return kvp.Key;
            }
        }

        return null;
    }
}
