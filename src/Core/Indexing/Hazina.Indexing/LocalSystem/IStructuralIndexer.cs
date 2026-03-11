using Hazina.Indexing.Models;

namespace Hazina.Indexing.LocalSystem;

/// <summary>
/// Indexer for local file system and process structure.
/// Phase A implementation: Fast structural scan without content reading.
/// </summary>
public interface IStructuralIndexer
{
    /// <summary>
    /// Index directory structure starting from root path.
    /// Captures file/directory metadata without reading file content.
    /// Target performance: less than 1 second for 10,000 files.
    /// </summary>
    /// <param name="rootPath">Root directory to index.</param>
    /// <param name="options">Indexation options (exclusions, limits).</param>
    /// <returns>Structural index containing all discovered files and metadata.</returns>
    Task<StructuralIndex> IndexDirectoryAsync(string rootPath, IndexOptions options);

    /// <summary>
    /// Index currently running system processes.
    /// Captures process metadata snapshot.
    /// </summary>
    /// <returns>Process index containing all running processes.</returns>
    Task<ProcessIndex> IndexRunningProcessesAsync();

    /// <summary>
    /// Check if a path matches exclusion patterns.
    /// </summary>
    /// <param name="path">Path to check.</param>
    /// <param name="excludePatterns">Glob patterns to match against.</param>
    /// <returns>True if path should be excluded; false otherwise.</returns>
    bool IsExcluded(string path, List<string> excludePatterns);
}
