using System.Text.RegularExpressions;
using Hazina.Tools.Services.Chat.Interfaces;

namespace Hazina.Tools.Services.Chat.MessagePreprocessors;

/// <summary>
/// Message preprocessor that detects URLs in user messages and adds hints for the LLM to use the retrieve_webpage tool.
/// </summary>
/// <remarks>
/// <para>
/// This preprocessor scans messages for HTTP/HTTPS URLs and appends a system hint when URLs are detected.
/// The hint informs the LLM that URLs are present and suggests using the retrieve_webpage tool if content analysis is needed.
/// </para>
/// <para>
/// <b>Why This Approach (Instead of Automatic Fetching):</b>
/// <list type="number">
/// <item><b>LLM Agency:</b> LLM decides whether to fetch based on context (not all URLs need fetching)</item>
/// <item><b>Token Optimization:</b> Only fetches content when actually needed for the response</item>
/// <item><b>Transparent Failures:</b> If URL fetch fails, LLM can inform the user (vs silent failure)</item>
/// <item><b>Configurable:</b> LLM can skip fetching based on user intent</item>
/// </list>
/// </para>
/// <para>
/// <b>Example Scenarios:</b>
/// <list type="bullet">
/// <item>User: "Summarize https://example.com" → Hint added → LLM fetches → Provides summary</item>
/// <item>User: "I found this: https://example.com" → Hint added → LLM may acknowledge without fetching</item>
/// <item>User: "Compare https://site1.com and https://site2.com" → Hint added → LLM fetches both</item>
/// </list>
/// </para>
/// </remarks>
public class UrlDetectionHintProvider : IMessagePreprocessor
{
    /// <summary>
    /// Regex pattern for detecting HTTP/HTTPS URLs in text.
    /// Compiled for performance when processing many messages.
    /// </summary>
    private static readonly Regex UrlPattern = new(
        @"https?://[^\s]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    /// <summary>
    /// Detects URLs in the message and adds a system hint if any are found.
    /// </summary>
    /// <param name="message">The user's message to analyze</param>
    /// <param name="cancellationToken">Cancellation token (not used - lightweight operation)</param>
    /// <returns>Original message with optional URL detection hint appended</returns>
    public Task<string> AddHintsAsync(string message, CancellationToken cancellationToken = default)
    {
        // Fast path: null/empty messages
        if (string.IsNullOrWhiteSpace(message))
            return Task.FromResult(message);

        // Detect URLs using regex
        var matches = UrlPattern.Matches(message);
        if (matches.Count == 0)
            return Task.FromResult(message);

        // Extract unique URLs, cleaning up trailing punctuation
        var urls = matches
            .Select(m => CleanUrl(m.Value))
            .Distinct()
            .ToList();

        // Build hint message
        var hint = BuildHint(urls);

        // Append hint to original message
        return Task.FromResult(message + hint);
    }

    /// <summary>
    /// Removes trailing punctuation from URLs that are often part of sentences.
    /// </summary>
    /// <param name="url">The URL to clean</param>
    /// <returns>URL without trailing punctuation</returns>
    /// <example>
    /// <code>
    /// CleanUrl("https://example.com.") → "https://example.com"
    /// CleanUrl("https://example.com)") → "https://example.com"
    /// </code>
    /// </example>
    private static string CleanUrl(string url)
    {
        return url.TrimEnd('.', ',', ')', ';', '!', '?', ':', '"', '\'');
    }

    /// <summary>
    /// Builds the system hint message based on detected URLs.
    /// </summary>
    /// <param name="urls">List of detected URLs</param>
    /// <returns>Formatted hint message for the LLM</returns>
    private static string BuildHint(List<string> urls)
    {
        var count = urls.Count;
        var urlList = string.Join(", ", urls.Take(3)); // Show max 3 URLs to avoid bloat

        if (count > 3)
        {
            urlList += $", and {count - 3} more";
        }

        return $"\n\n[SYSTEM HINT: {count} URL(s) detected in user message. " +
               $"If the user wants you to analyze, summarize, or reference the webpage content, " +
               $"use the retrieve_webpage tool. Detected URLs: {urlList}]";
    }
}
