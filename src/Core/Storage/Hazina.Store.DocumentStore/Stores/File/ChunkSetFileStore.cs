using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Hazina.LLMs.Infrastructure;

/// <summary>
/// File-based storage for chunk sets.
/// Stores chunk sets as JSON files with naming pattern: {documentId}.chunksets.json
/// </summary>
public class ChunkSetFileStore : IChunkSetStore
{
    private readonly string _baseDirectory;
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Create a new chunk set file store.
    /// </summary>
    /// <param name="baseDirectory">Base directory for storing chunk set files.</param>
    /// <param name="fileSystem">Optional file system abstraction for testing.</param>
    public ChunkSetFileStore(string baseDirectory, IFileSystem? fileSystem = null)
    {
        _baseDirectory = baseDirectory;
        _fileSystem = fileSystem ?? new PhysicalFileSystem();
        if (!_fileSystem.DirectoryExists(_baseDirectory))
        {
            _fileSystem.CreateDirectory(_baseDirectory);
        }
    }

    /// <summary>
    /// Get the file path for a document's chunk sets.
    /// </summary>
    private string GetFilePath(string documentId)
    {
        // Sanitize document ID for filename
        var safeFileName = documentId.Replace(":", "_").Replace("/", "_").Replace("\\", "_");
        return _fileSystem.PathCombine(_baseDirectory, $"{safeFileName}.chunksets.json");
    }

    /// <summary>
    /// Store chunk sets for a document.
    /// </summary>
    public async Task<bool> StoreAsync(string documentId, IEnumerable<ChunkSet> chunkSets)
    {
        try
        {
            var filePath = GetFilePath(documentId);
            var directory = _fileSystem.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !_fileSystem.DirectoryExists(directory))
            {
                _fileSystem.CreateDirectory(directory);
            }

            var chunkSetList = chunkSets.ToList();
            var json = JsonSerializer.Serialize(chunkSetList, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await _fileSystem.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Get all chunk sets for a document.
    /// </summary>
    public async Task<IEnumerable<ChunkSet>> GetAsync(string documentId)
    {
        try
        {
            var filePath = GetFilePath(documentId);
            if (!_fileSystem.FileExists(filePath))
            {
                return Enumerable.Empty<ChunkSet>();
            }

            var json = await _fileSystem.ReadAllTextAsync(filePath);
            var chunkSets = JsonSerializer.Deserialize<List<ChunkSet>>(json);
            return chunkSets ?? Enumerable.Empty<ChunkSet>();
        }
        catch (Exception)
        {
            return Enumerable.Empty<ChunkSet>();
        }
    }

    /// <summary>
    /// Get a specific chunk set by ID.
    /// </summary>
    public async Task<ChunkSet?> GetByIdAsync(string setId)
    {
        // Extract document ID from set ID (format: "documentId:section:N")
        var parts = setId.Split(':');
        if (parts.Length < 3)
        {
            return null;
        }

        var documentId = parts[0];
        var chunkSets = await GetAsync(documentId);
        return chunkSets.FirstOrDefault(cs => cs.SetId == setId);
    }

    /// <summary>
    /// Remove all chunk sets for a document.
    /// </summary>
    public async Task<bool> RemoveAsync(string documentId)
    {
        try
        {
            var filePath = GetFilePath(documentId);
            if (_fileSystem.FileExists(filePath))
            {
                _fileSystem.DeleteFile(filePath);
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// List all document IDs that have chunk sets.
    /// </summary>
    public async Task<IEnumerable<string>> ListDocumentIdsAsync()
    {
        try
        {
            var files = _fileSystem.GetFiles(_baseDirectory, "*.chunksets.json", SearchOption.TopDirectoryOnly);
            var documentIds = files.Select(f =>
            {
                var fileName = _fileSystem.GetFileNameWithoutExtension(f);
                // Remove .chunksets suffix if present
                if (fileName != null && fileName.EndsWith(".chunksets"))
                {
                    fileName = fileName.Substring(0, fileName.Length - ".chunksets".Length);
                }
                // Reverse sanitization
                return fileName?.Replace("_", ":") ?? string.Empty;
            });
            return documentIds;
        }
        catch (Exception)
        {
            return Enumerable.Empty<string>();
        }
    }
}
