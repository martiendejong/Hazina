public class TextFileStore : ITextStore
{
    public string RootFolder { get; set; }

    public TextFileStore(string rootFolder)
    {
        RootFolder = rootFolder;
    }

    public string GetPath(string key)
    {
        return Path.Combine(RootFolder, key);
    }

    public async Task<string?> Get(string key)
    {
        return File.Exists(GetPath(key)) ? await File.ReadAllTextAsync(GetPath(key)) : null;
    }

    public async Task<bool> Remove(string key)
    {
        var path = GetPath(key);
        if (!File.Exists(path)) return false;
        File.Delete(path);
        return true;
    }

    public async Task<bool> Store(string key, string value)
    {
        var fi = new FileInfo(GetPath(key));
        if(fi ==  null || fi.Directory == null) return false;
        var dir = fi.Directory.FullName;
        Directory.CreateDirectory(dir);            
        await File.WriteAllTextAsync(GetPath(key), value);
        return true;
    }
}
