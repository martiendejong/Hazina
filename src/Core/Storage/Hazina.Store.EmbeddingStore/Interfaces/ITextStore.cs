public interface ITextStore
{
    string RootFolder { get; set; }

    public Task<bool> Store(string key, string value);
    public Task<string?> Get(string key);
    public string GetPath(string key);
    public Task<bool> Remove(string key);
}
