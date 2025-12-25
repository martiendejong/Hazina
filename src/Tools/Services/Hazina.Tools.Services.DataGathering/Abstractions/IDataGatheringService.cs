using Hazina.Tools.Models;
using Hazina.Tools.Services.DataGathering.Models;

namespace Hazina.Tools.Services.DataGathering.Abstractions;

/// <summary>
/// Service responsible for extracting and storing structured data from chat conversations.
/// This is the main orchestration interface for the data gathering feature.
/// </summary>
public interface IDataGatheringService
{
    /// <summary>
    /// Analyzes a chat message and extracts any relevant data to store.
    /// This method runs asynchronously and does not block the main chat response.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="chatId">The chat identifier.</param>
    /// <param name="userMessage">The user's message to analyze.</param>
    /// <param name="conversationHistory">Previous messages in the conversation for context.</param>
    /// <param name="userId">Optional user identifier for user-specific chats.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of gathered data items extracted from the message.</returns>
    Task<IReadOnlyList<GatheredDataItem>> GatherDataFromMessageAsync(
        string projectId,
        string chatId,
        string userMessage,
        IEnumerable<HazinaChatMessage> conversationHistory,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all gathered data for a project.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All gathered data items for the project.</returns>
    Task<IReadOnlyList<GatheredDataItem>> GetProjectDataAsync(
        string projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific gathered data item.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="key">The data item key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The data item if found; otherwise, <c>null</c>.</returns>
    Task<GatheredDataItem?> GetDataItemAsync(
        string projectId,
        string key,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually stores a gathered data item (for cases where data is provided directly).
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="chatId">The chat identifier (for notification purposes).</param>
    /// <param name="item">The data item to store.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if successful; otherwise, <c>false</c>.</returns>
    Task<bool> StoreDataItemAsync(
        string projectId,
        string chatId,
        GatheredDataItem item,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a gathered data item.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="chatId">The chat identifier (for notification purposes).</param>
    /// <param name="key">The key of the item to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> if the item was deleted; otherwise, <c>false</c>.</returns>
    Task<bool> DeleteDataItemAsync(
        string projectId,
        string chatId,
        string key,
        CancellationToken cancellationToken = default);
}
