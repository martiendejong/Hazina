using System.Linq;
using System.Text.Json;

public class ChunkFileStore : IChunkStore
{
    public string ChunksFilePath { get; set; }
    public ChunkFileStore(string chunksFilePath) { ChunksFilePath = chunksFilePath;
        LoadChunksFile();
    }

    private void LoadChunksFile()
    {
        if (File.Exists(ChunksFilePath))
        {
            try
            {
                var data = File.ReadAllText(ChunksFilePath);
                Chunks = JsonSerializer.Deserialize<Dictionary<string, IEnumerable<string>>>(data);
                return;
            }
            catch { }
        }
        Chunks = new Dictionary<string, IEnumerable<string>>();
    }

    public void StoreChunksFile()
    {
        var directory = Path.GetDirectoryName(ChunksFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        var data = JsonSerializer.Serialize(Chunks);
        File.WriteAllText(ChunksFilePath, data);
    }

    public Dictionary<string, IEnumerable<string>> Chunks;

    public async Task<bool> Store(string name, IEnumerable<string> chunkKeys)
    {
        Chunks[name] = chunkKeys.ToArray();
        StoreChunksFile();
        return true;
    }

    public async Task<IEnumerable<string>> Get(string name)
    {
        return Chunks.ContainsKey(name) ? Chunks[name] : [];
    }

    public async Task<bool> Remove(string name, IEnumerable<string> chunkKeys)
    {
        Chunks.Remove(name);
        StoreChunksFile();
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
