using Hazina.LLMs.GoogleADK.Memory.Models;

namespace Hazina.LLMs.GoogleADK.Memory.Storage;

/// <summary>
/// Interface for memory storage providers
/// </summary>
public interface IMemoryStorage
{
    /// <summary>
    /// Save a memory item
    /// </summary>
    Task SaveMemoryAsync(MemoryItem memory, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve a memory by ID
    /// </summary>
    Task<MemoryItem?> GetMemoryAsync(string memoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a memory
    /// </summary>
    Task DeleteMemoryAsync(string memoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search memories
    /// </summary>
    Task<List<MemorySearchResult>> SearchMemoriesAsync(
        MemoryQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// List all memories with optional filters
    /// </summary>
    Task<List<MemoryItem>> ListMemoriesAsync(
        string? agentName = null,
        string? userId = null,
        MemoryType? type = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get memories by tag
    /// </summary>
    Task<List<MemoryItem>> GetMemoriesByTagAsync(
        string tag,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update memory access metadata
    /// </summary>
    Task UpdateMemoryAccessAsync(
        string memoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get memory count
    /// </summary>
    Task<int> GetMemoryCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Consolidate memories (remove weak/old ones)
    /// </summary>
    Task<int> ConsolidateMemoriesAsync(
        double strengthThreshold = 0.1,
        CancellationToken cancellationToken = default);
}
