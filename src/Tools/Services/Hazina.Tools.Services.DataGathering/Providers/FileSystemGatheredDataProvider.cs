using System.Text.Json;
using System.Text.RegularExpressions;
using Hazina.Tools.Data;
using Hazina.Tools.Services.DataGathering.Abstractions;
using Hazina.Tools.Services.DataGathering.Models;

namespace Hazina.Tools.Services.DataGathering.Providers;

/// <summary>
/// File system-based implementation of <see cref="IGatheredDataProvider"/>.
/// Stores gathered data as JSON files in a project-specific directory.
/// </summary>
/// <remarks>
/// Storage structure:
/// <code>
/// {ProjectsFolder}/{projectId}/gathered-data/
///     {sanitized-key}.json
/// </code>
/// </remarks>
public sealed class FileSystemGatheredDataProvider : IGatheredDataProvider
{
    private const string GatheredDataFolderName = "gathered-data";
    private const string JsonExtension = ".json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex InvalidFileNameChars = new(
        $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]",
        RegexOptions.Compiled);

    private readonly ProjectFileLocator _fileLocator;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileSystemGatheredDataProvider"/> class.
    /// </summary>
    /// <param name="projectsRepository">The projects repository for file path resolution.</param>
    public FileSystemGatheredDataProvider(ProjectsRepository projectsRepository)
    {
        ArgumentNullException.ThrowIfNull(projectsRepository);
        _fileLocator = new ProjectFileLocator(projectsRepository.ProjectsFolder);
    }

    /// <summary>
    /// Initializes a new instance using a file locator directly.
    /// </summary>
    /// <param name="fileLocator">The file locator for path resolution.</param>
    public FileSystemGatheredDataProvider(ProjectFileLocator fileLocator)
    {
        _fileLocator = fileLocator ?? throw new ArgumentNullException(nameof(fileLocator));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GatheredDataItem>> GetAllAsync(
        string projectId,
        CancellationToken cancellationToken = default)
    {
        var folderPath = GetGatheredDataFolder(projectId);

        if (!Directory.Exists(folderPath))
        {
            return Array.Empty<GatheredDataItem>();
        }

        var items = new List<GatheredDataItem>();
        var files = Directory.GetFiles(folderPath, $"*{JsonExtension}");

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var item = JsonSerializer.Deserialize<GatheredDataItem>(json, SerializerOptions);

                if (item is not null)
                {
                    items.Add(item);
                }
            }
            catch (JsonException)
            {
                // Skip malformed files - log in production
            }
            catch (IOException)
            {
                // Skip inaccessible files - log in production
            }
        }

        return items
            .OrderByDescending(i => i.GatheredAt)
            .ToList()
            .AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<GatheredDataItem?> GetAsync(
        string projectId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetItemFilePath(projectId, key);

        if (!File.Exists(filePath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(filePath, cancellationToken);
            return JsonSerializer.Deserialize<GatheredDataItem>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(
        string projectId,
        GatheredDataItem item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (string.IsNullOrWhiteSpace(item.Key))
        {
            return false;
        }

        var folderPath = GetGatheredDataFolder(projectId);
        EnsureDirectoryExists(folderPath);

        var filePath = GetItemFilePath(projectId, item.Key);

        try
        {
            var json = JsonSerializer.Serialize(item, SerializerOptions);

            // Write to temp file first, then move for atomic operation
            var tempPath = filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);

            // Atomic move/replace
            File.Move(tempPath, filePath, overwrite: true);

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<int> SaveManyAsync(
        string projectId,
        IEnumerable<GatheredDataItem> items,
        CancellationToken cancellationToken = default)
    {
        var savedCount = 0;

        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await SaveAsync(projectId, item, cancellationToken))
            {
                savedCount++;
            }
        }

        return savedCount;
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(
        string projectId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetItemFilePath(projectId, key);

        if (!File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(filePath);
            return Task.FromResult(true);
        }
        catch (IOException)
        {
            return Task.FromResult(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(
        string projectId,
        string key,
        CancellationToken cancellationToken = default)
    {
        var filePath = GetItemFilePath(projectId, key);
        return Task.FromResult(File.Exists(filePath));
    }

    /// <summary>
    /// Gets the folder path for gathered data storage.
    /// </summary>
    private string GetGatheredDataFolder(string projectId)
    {
        var projectFolder = _fileLocator.GetProjectFolder(projectId);
        return Path.Combine(projectFolder, GatheredDataFolderName);
    }

    /// <summary>
    /// Gets the file path for a specific gathered data item.
    /// </summary>
    private string GetItemFilePath(string projectId, string key)
    {
        var folder = GetGatheredDataFolder(projectId);
        var sanitizedKey = SanitizeFileName(key);
        return Path.Combine(folder, sanitizedKey + JsonExtension);
    }

    /// <summary>
    /// Sanitizes a key to be used as a valid filename.
    /// </summary>
    private static string SanitizeFileName(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return "unnamed";
        }

        // Replace spaces with hyphens, convert to lowercase
        var sanitized = key.Trim().ToLowerInvariant().Replace(' ', '-');

        // Remove invalid characters
        sanitized = InvalidFileNameChars.Replace(sanitized, string.Empty);

        // Collapse multiple hyphens
        sanitized = Regex.Replace(sanitized, "-+", "-").Trim('-');

        // Ensure not empty after sanitization
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            sanitized = "unnamed";
        }

        // Limit length to prevent path issues
        const int maxLength = 100;
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized[..maxLength];
        }

        return sanitized;
    }

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    private static void EnsureDirectoryExists(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }
}
