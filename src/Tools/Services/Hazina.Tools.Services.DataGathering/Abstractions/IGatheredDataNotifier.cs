using Hazina.Tools.Services.DataGathering.Models;

namespace Hazina.Tools.Services.DataGathering.Abstractions;

/// <summary>
/// Provides real-time notification capabilities for gathered data events.
/// Implementations typically integrate with SignalR or similar technologies
/// to push updates to connected clients.
/// </summary>
public interface IGatheredDataNotifier
{
    /// <summary>
    /// Notifies clients that a single data item has been gathered.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="chatId">The chat identifier where the data was gathered.</param>
    /// <param name="item">The gathered data item.</param>
    /// <param name="userId">Optional user identifier for user-specific chats.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyDataGatheredAsync(
        string projectId,
        string chatId,
        GatheredDataItem item,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that multiple data items have been gathered in a batch.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="chatId">The chat identifier where the data was gathered.</param>
    /// <param name="items">The gathered data items.</param>
    /// <param name="userId">Optional user identifier for user-specific chats.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyDataGatheredBatchAsync(
        string projectId,
        string chatId,
        IEnumerable<GatheredDataItem> items,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that a data item has been updated.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="chatId">The chat identifier where the update occurred.</param>
    /// <param name="item">The updated data item.</param>
    /// <param name="userId">Optional user identifier for user-specific chats.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyDataUpdatedAsync(
        string projectId,
        string chatId,
        GatheredDataItem item,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that a data item has been deleted.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="chatId">The chat identifier where the deletion occurred.</param>
    /// <param name="key">The key of the deleted data item.</param>
    /// <param name="userId">Optional user identifier for user-specific chats.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task NotifyDataDeletedAsync(
        string projectId,
        string chatId,
        string key,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
