using System.Linq;
using System.Text.Json;
using Hazina.LLMs.Infrastructure;

public class ChunkFileStore : IChunkStore
{
    public string ChunksFilePath { get; set; }
    private readonly IFileSystem _fileSystem;

    public ChunkFileStore(string chunksFilePath, IFileSystem? fileSystem = null)
    {
        ChunksFilePath = chunksFilePath;
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
        LoadChunksFile();
    }

    private void LoadChunksFile()
    {
        if (_fileSystem.FileExists(ChunksFilePath))
        {
            try
            {
                var data = _fileSystem.ReadAllText(ChunksFilePath);
                Chunks = JsonSerializer.Deserialize<Dictionary<string, IEnumerable<string>>>(data);
                return;
            }
            catch { }
        }
        Chunks = new Dictionary<string, IEnumerable<string>>();
    }

    public void StoreChunksFile()
    {
        var directory = _fileSystem.GetDirectoryName(ChunksFilePath);
        if (!string.IsNullOrEmpty(directory) && !_fileSystem.DirectoryExists(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }
        var data = JsonSerializer.Serialize(Chunks);
        _fileSystem.WriteAllText(ChunksFilePath, data);
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
