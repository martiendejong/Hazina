using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Hazina.LLMs.Plugins;

/// <summary>
/// Semantic Kernel plugin for DocumentStore operations
/// Provides read, write, search, and list operations for a document store
/// </summary>
public class DocumentStorePlugin
{
    private readonly IDocumentStore _store;
    private readonly bool _allowWrite;
    private readonly string _description;

    public DocumentStorePlugin(IDocumentStore store, bool allowWrite = false, string description = "")
    {
        _store = store;
        _allowWrite = allowWrite;
        _description = description;
    }

    #region Read Operations

    [KernelFunction("list")]
    [Description("Retrieve a list of files in the store")]
    public async Task<string> ListFiles(
        [Description("Folder path to list (empty for root)")] string folder = "",
        [Description("Whether to search recursively")] bool recursive = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var files = await _store.List(folder, recursive);
            return string.Join("\n", files);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("relevancy")]
    [Description("Retrieve a list of relevant files based on semantic search")]
    public async Task<string> SearchRelevant(
        [Description("Query to search for semantically similar documents")] string query,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(query))
                return "No query provided";

            var relevantItems = await _store.RelevantItems(query);
            return string.Join("\n", relevantItems);
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("read")]
    [Description("Retrieve the contents of a file from the store")]
    public async Task<string> ReadFile(
        [Description("File path to read")] string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(key))
                return "No key provided";

            var content = await _store.Get(key);
            return content ?? "File not found or empty";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    #endregion

    #region Write Operations (if enabled)

    [KernelFunction("write")]
    [Description("Store a file in the store")]
    public async Task<string> WriteFile(
        [Description("File path/key to write")] string key,
        [Description("Content to write to the file")] string content,
        CancellationToken cancellationToken = default)
    {
        if (!_allowWrite)
            return "Write operations are not allowed for this store";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
                return "No key provided";

            if (content == null)
                return "No content provided";

            var success = await _store.Store(key, content);
            return success ? $"Successfully stored file: {key}" : "Failed to store file";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("delete")]
    [Description("Delete a file from the store")]
    public async Task<string> DeleteFile(
        [Description("File path/key to delete")] string key,
        CancellationToken cancellationToken = default)
    {
        if (!_allowWrite)
            return "Write operations are not allowed for this store";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(key))
                return "No key provided";

            var success = await _store.Remove(key);
            return success ? $"Successfully deleted file: {key}" : "Failed to delete file";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    [KernelFunction("move")]
    [Description("Move/rename a file in the store")]
    public async Task<string> MoveFile(
        [Description("Current file path/key")] string oldKey,
        [Description("New file path/key")] string newKey,
        CancellationToken cancellationToken = default)
    {
        if (!_allowWrite)
            return "Write operations are not allowed for this store";

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(oldKey))
                return "No old key provided";

            if (string.IsNullOrWhiteSpace(newKey))
                return "No new key provided";

            var success = await _store.Move(oldKey, newKey);
            return success ? $"Successfully moved file from {oldKey} to {newKey}" : "Failed to move file";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    #endregion

    /// <summary>
    /// Get the plugin name based on store name
    /// </summary>
    public string GetPluginName() => _store.Name;

    /// <summary>
    /// Get the plugin description
    /// </summary>
    public string GetPluginDescription() => _description;
}
