using Hazina.Tools.Models;
using Hazina.Tools.Services.DataGathering.Services;
using Hazina.Tools.Services.Store;

namespace Hazina.Tools.Services.DataGathering.Abstractions;

/// <summary>
/// Service for automatically generating analysis fields from chat conversations.
/// Runs in parallel with the main chat to populate analysis fields when enough context is available.
/// </summary>
public interface IAnalysisFieldService
{
    public Task<Dictionary<string, AnalysisFieldConfig>> LoadFieldConfigsAsync(string projectId);

    /// <summary>
    /// Analyzes the conversation and attempts to generate any analysis fields
    /// that have enough context to be populated.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="chatId">The chat identifier.</param>
    /// <param name="userMessage">The latest user message.</param>
    /// <param name="conversationHistory">Recent conversation history for context.</param>
    /// <param name="userId">Optional user identifier for user-specific storage.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of analysis fields that were generated.</returns>
    Task<IReadOnlyList<GeneratedAnalysisField>> GenerateFromConversationAsync(
        string projectId,
        string chatId,
        string userMessage,
        IEnumerable<HazinaChatMessage> conversationHistory,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all available analysis field configurations.
    /// </summary>
    Task<IReadOnlyList<AnalysisFieldInfo>> GetFieldsAsync(string projectId);

    /// <summary>
    /// Gets the current content of an analysis field.
    /// </summary>
    Task<string?> GetFieldContentAsync(string projectId, string key);

    /// <summary>
    /// Manually saves content to an analysis field.
    /// </summary>
    Task<bool> SaveFieldAsync(
        string projectId,
        string chatId,
        string key,
        string content,
        string? feedback = null,
        string? userId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a typed analysis field using InternalGenerate&lt;T&gt; pattern.
    /// This is used for fields with GenericType defined in configuration.
    /// The result is serialized to JSON and stored in the same format as AnalysisController.
    /// </summary>
    /// <param name="projectId">The project identifier.</param>
    /// <param name="chatId">The chat identifier.</param>
    /// <param name="key">The analysis field key.</param>
    /// <param name="instruction">Optional instruction for the generation.</param>
    /// <param name="userId">Optional user identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated typed object, or null if generation failed.</returns>
    Task<object?> GenerateTypedFieldAsync(
        string projectId,
        string chatId,
        string key,
        string? instruction = null,
        string? userId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an analysis field that was generated.
/// </summary>
public class GeneratedAnalysisField
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
