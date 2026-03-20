using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Hazina.LLMs.Infrastructure;

public class DocumentMetadataFileStore : IDocumentMetadataStore
{
    private readonly string _rootFolder;
    private readonly IFileSystem _fileSystem;

    public DocumentMetadataFileStore(string rootFolder, IFileSystem? fileSystem = null)
    {
        _rootFolder = rootFolder;
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
        _fileSystem.CreateDirectory(_rootFolder);
    }

    private string GetMetadataPath(string id)
    {
        var sanitized = id.Replace('/', '_').Replace('\\', '_').Replace(':', '_');
        return _fileSystem.PathCombine(_rootFolder, $"{sanitized}.metadata.json");
    }

    public async Task<bool> Store(string id, DocumentMetadata metadata)
    {
        try
        {
            var path = GetMetadataPath(id);
            var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
            await _fileSystem.WriteAllTextAsync(path, json);
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
            if (!_fileSystem.FileExists(path)) return null;

            var json = await _fileSystem.ReadAllTextAsync(path);
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
            if (_fileSystem.FileExists(path))
            {
                _fileSystem.DeleteFile(path);
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
        return _fileSystem.FileExists(path);
    }
}
