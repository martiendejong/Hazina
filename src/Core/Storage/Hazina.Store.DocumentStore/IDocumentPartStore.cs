public interface IDocumentPartStore
{
    public Task<bool> Store(string name, IEnumerable<string> partKeys);
    public Task<IEnumerable<string>> Get(string name);
    public Task<bool> Remove(string name, IEnumerable<string> partKeys);
    public Task<IEnumerable<string>> ListNames();
}

