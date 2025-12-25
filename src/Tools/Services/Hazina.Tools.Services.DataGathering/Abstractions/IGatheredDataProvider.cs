using DevGPT.GenerationTools.Services.DataGathering.Models;

namespace DevGPT.GenerationTools.Services.DataGathering.Abstractions;

/// <summary>
/// Provides storage and retrieval operations for gathered data items.
/// Implementations handle persistence (file system, database, etc.) and
/// ensure data integrity across operations.
/// </summary>
public interface IGatheredDataProvider
{
    /// <summary>
    /// Retrieves all gathered data items for a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of all gathered data items, ordered by <see cref="GatheredDataItem.GatheredAt"/> descending.</returns>
    Task<IReadOnlyList<GatheredDataItem>> GetAllAsync(string projectId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific gathered data item by its key.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="key">The unique key of the data item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The data item if found; otherwise, <c>null</c>.</returns>
    Task<GatheredDataItem?> GetAsync(string projectId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a gathered data item, creating or updating as necessary.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="item">The data item to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the save was successful; otherwise, <c>false</c>.</returns>
    Task<bool> SaveAsync(string projectId, GatheredDataItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves multiple gathered data items in a single operation.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="items">The data items to save.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of items successfully saved.</returns>
    Task<int> SaveManyAsync(string projectId, IEnumerable<GatheredDataItem> items, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a gathered data item by its key.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="key">The unique key of the data item to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the item was deleted; <c>false</c> if it did not exist.</returns>
    Task<bool> DeleteAsync(string projectId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a gathered data item exists.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="key">The unique key of the data item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the item exists; otherwise, <c>false</c>.</returns>
    Task<bool> ExistsAsync(string projectId, string key, CancellationToken cancellationToken = default);
}
