using System.Collections.Concurrent;
using System.Threading.Tasks;

public class DocumentMetadataMemoryStore : IDocumentMetadataStore
{
    private readonly ConcurrentDictionary<string, DocumentMetadata> _metadata = new();

    public Task<bool> Store(string id, DocumentMetadata metadata)
    {
        _metadata[id] = metadata;
        return Task.FromResult(true);
    }

    public Task<DocumentMetadata?> Get(string id)
    {
        _metadata.TryGetValue(id, out var metadata);
        return Task.FromResult(metadata);
    }

    public Task<bool> Remove(string id)
    {
        _metadata.TryRemove(id, out _);
        return Task.FromResult(true);
    }

    public Task<bool> Exists(string id)
    {
        return Task.FromResult(_metadata.ContainsKey(id));
    }
}
