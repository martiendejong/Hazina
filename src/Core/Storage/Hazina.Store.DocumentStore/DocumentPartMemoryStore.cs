using System.Linq;

public class DocumentPartMemoryStore : IDocumentPartStore
{
    public Dictionary<string, string[]> Parts = new Dictionary<string, string[]>();

    public async Task<bool> Store(string name, IEnumerable<string> partKeys)
    {
        Parts[name] = partKeys.ToArray();
        return true;
    }

    public async Task<IEnumerable<string>> Get(string name)
    {
        return Parts[name];
    }

    public async Task<bool> Remove(string name, IEnumerable<string> partKeys)
    {
        Parts.Remove(name);
        return true;
    }

    public async Task<IEnumerable<string>> ListNames()
    {
        return Parts.Keys.ToArray();
    }
}
