using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

public class DocumentMetadataFileStore : IDocumentMetadataStore
{
    private readonly string _rootFolder;

    public DocumentMetadataFileStore(string rootFolder)
    {
        _rootFolder = rootFolder;
        Directory.CreateDirectory(_rootFolder);
    }

    private string GetMetadataPath(string id)
    {
        var sanitized = id.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        return Path.Combine(_rootFolder, $"{sanitized}.metadata.json");
    }

    public async Task<bool> Store(string id, DocumentMetadata metadata)
    {
        try
        {
            var path = GetMetadataPath(id);
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(path, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<DocumentMetadata?> Get(string id)
    {
        try
        {
            var path = GetMetadataPath(id);
            if (!File.Exists(path)) return null;

            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<DocumentMetadata>(json);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> Remove(string id)
    {
        try
        {
            var path = GetMetadataPath(id);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> Exists(string id)
    {
        var path = GetMetadataPath(id);
        return File.Exists(path);
    }
}
