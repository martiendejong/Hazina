using System.Threading.Tasks;

public interface IDocumentMetadataStore
{
    public Task<bool> Store(string id, DocumentMetadata metadata);
    public Task<DocumentMetadata?> Get(string id);
    public Task<bool> Remove(string id);
    public Task<bool> Exists(string id);
}
