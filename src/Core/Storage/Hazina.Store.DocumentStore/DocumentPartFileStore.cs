using System.Linq;
using System.Text.Json;
using Hazina.LLMs.Infrastructure;

public class DocumentPartFileStore : IDocumentPartStore
{
    public string PartsFilePath { get; set; }
    private readonly IFileSystem _fileSystem;

    public DocumentPartFileStore(string partsFilePath, IFileSystem? fileSystem = null)
    {
        PartsFilePath = partsFilePath;
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
        LoadPartsFile();
    }

    private void LoadPartsFile()
    {
        if (_fileSystem.FileExists(PartsFilePath))
        {
            try
            {
                var data = _fileSystem.ReadAllText(PartsFilePath);
                Parts = JsonSerializer.Deserialize<Dictionary<string, IEnumerable<string>>>(data);
                return;
            }
            catch { }
        }
        Parts = new Dictionary<string, IEnumerable<string>>();
    }

    public void StorePartsFile()
    {
        var directory = _fileSystem.GetDirectoryName(PartsFilePath);
        if (!string.IsNullOrEmpty(directory) && !_fileSystem.DirectoryExists(directory))
        {
            _fileSystem.CreateDirectory(directory);
        }
        var data = JsonSerializer.Serialize(Parts);
        _fileSystem.WriteAllText(PartsFilePath, data);
    }

    public Dictionary<string, IEnumerable<string>> Parts;

    public async Task<bool> Store(string name, IEnumerable<string> partKeys)
    {
        Parts[name] = partKeys.ToArray();
        StorePartsFile();
        return true;
    }

    public async Task<IEnumerable<string>> Get(string name)
    {
        return Parts.ContainsKey(name) ? Parts[name] : [];
    }

    public async Task<bool> Remove(string name, IEnumerable<string> partKeys)
    {
        Parts.Remove(name);
        StorePartsFile();
        return true;
    }

    public async Task<IEnumerable<string>> ListNames()
    {
        return Parts.Keys.ToArray();
    }
}
