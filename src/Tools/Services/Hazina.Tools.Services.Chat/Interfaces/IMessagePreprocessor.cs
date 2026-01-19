namespace Hazina.Tools.Services.Chat.Interfaces;

/// <summary>
/// Interface for message preprocessors that can enhance or transform messages before LLM processing.
/// Preprocessors add hints, metadata, or lightweight transformations without fetching external content.
/// </summary>
/// <remarks>
/// <para>
/// Message preprocessors are lightweight services that analyze and enrich messages before they reach the LLM.
/// Unlike tools (which the LLM explicitly invokes), preprocessors run automatically and add contextual hints.
/// </para>
/// <para>
/// <b>Use Cases:</b>
/// <list type="bullet">
/// <item>URL detection: Hint LLM to use retrieve_webpage tool when URLs are present</item>
/// <item>Language detection: Add language metadata for multilingual support</item>
/// <item>Intent classification: Hint whether message is question, command, or statement</item>
/// <item>Entity extraction: Identify dates, locations, people for context</item>
/// </list>
/// </para>
/// <para>
/// <b>Design Principles:</b>
/// <list type="number">
/// <item><b>Lightweight:</b> Add minimal tokens (hints only, not full content)</item>
/// <item><b>Non-blocking:</b> Should be fast (no heavy API calls)</item>
/// <item><b>Optional:</b> LLM can ignore hints if not relevant</item>
/// <item><b>Transparent:</b> Hints clearly marked as system-generated</item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class UrlDetectionHintProvider : IMessagePreprocessor
/// {
///     public async Task&lt;string&gt; AddHintsAsync(string message, CancellationToken cancellationToken)
///     {
///         var urls = DetectUrls(message);
///         if (urls.Count == 0) return message;
///
///         return message + $"\n\n[SYSTEM HINT: {urls.Count} URL(s) detected. " +
///                          $"Consider using retrieve_webpage tool if analysis needed.]";
///     }
/// }
/// </code>
/// </example>
public interface IMessagePreprocessor
{
    /// <summary>
    /// Processes a message and optionally adds hints, metadata, or lightweight transformations.
    /// </summary>
    /// <param name="message">The original user message to process</param>
    /// <param name="cancellationToken">Cancellation token for async operations</param>
    /// <returns>The processed message with optional hints/metadata appended</returns>
    /// <remarks>
    /// <para>
    /// Implementations should:
    /// <list type="bullet">
    /// <item>Return the original message unchanged if no preprocessing is needed</item>
    /// <item>Append hints as clearly-marked system messages (e.g., "[SYSTEM HINT: ...]")</item>
    /// <item>Avoid fetching external content (use tools for that)</item>
    /// <item>Be fast (ideally &lt;100ms)</item>
    /// <item>Be safe (handle null/empty messages gracefully)</item>
    /// </list>
    /// </para>
    /// </remarks>
    Task<string> AddHintsAsync(string message, CancellationToken cancellationToken = default);
}
