using System.Text;
using System.Text.Json;

namespace Hazina.LLMs;

/// <summary>
/// Parser for handling incomplete or malformed JSON strings commonly received from streaming LLM responses.
/// </summary>
public class PartialJsonParser
{
    private readonly JsonSerializerOptions _options;

    public PartialJsonParser()
    {
        _options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };
    }

    /// <summary>
    /// Counts opening and closing braces in the input, excluding those inside strings.
    /// </summary>
    public static (int openBraces, int closeBraces) CountBraces(string input)
    {
        int openBraces = 0, closeBraces = 0;
        bool inString = false;
        bool escaped = false;

        foreach (char c in input)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (c == '{') openBraces++;
                else if (c == '}') closeBraces++;
            }
        }

        return (openBraces, closeBraces);
    }

    /// <summary>
    /// Attempts to parse partial or malformed JSON into a strongly-typed object.
    /// Applies multiple recovery strategies to handle common streaming issues.
    /// </summary>
    public TResponse? Parse<TResponse>(string partialJson)
    {
        if (string.IsNullOrWhiteSpace(partialJson))
            return default;

        // Strategy 1: Try parsing as-is
        var result = TryParse<TResponse>(partialJson);
        if (result.success)
            return result.value;

        // Strategy 2: Remove text before first JSON delimiter
        var cleaned = RemoveTextBeforeJson(partialJson);
        result = TryParse<TResponse>(cleaned);
        if (result.success)
            return result.value;

        // Strategy 3: Clean doubled delimiters
        cleaned = CleanDoubledDelimiters(cleaned);
        result = TryParse<TResponse>(cleaned);
        if (result.success)
            return result.value;

        // Strategy 4: Balance unmatched delimiters
        cleaned = BalanceDelimiters(cleaned);
        result = TryParse<TResponse>(cleaned);
        if (result.success)
            return result.value;

        // Strategy 5: Remove trailing commas (if not already handled by options)
        cleaned = RemoveTrailingCommas(cleaned);
        result = TryParse<TResponse>(cleaned);
        if (result.success)
            return result.value;

        // All strategies failed
        return default;
    }

    private (bool success, TResponse? value) TryParse<TResponse>(string json)
    {
        try
        {
            var value = JsonSerializer.Deserialize<TResponse>(json, _options);
            return (true, value);
        }
        catch
        {
            return (false, default);
        }
    }

    private string RemoveTextBeforeJson(string input)
    {
        var startBrace = input.IndexOf('{');
        var startBracket = input.IndexOf('[');

        int start;
        if (startBrace >= 0 && startBracket >= 0)
        {
            start = Math.Min(startBrace, startBracket);
        }
        else if (startBrace >= 0)
        {
            start = startBrace;
        }
        else if (startBracket >= 0)
        {
            start = startBracket;
        }
        else
        {
            return input; // No JSON delimiter found
        }

        return input.Substring(start);
    }

    private string CleanDoubledDelimiters(string input)
    {
        var result = new StringBuilder();
        bool inString = false;
        bool escaped = false;
        char? lastChar = null;

        foreach (char c in input)
        {
            if (escaped)
            {
                result.Append(c);
                escaped = false;
                lastChar = c;
                continue;
            }

            if (c == '\\' && inString)
            {
                result.Append(c);
                escaped = true;
                lastChar = c;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                result.Append(c);
                lastChar = c;
                continue;
            }

            // Skip doubled delimiters outside of strings
            if (!inString && lastChar.HasValue)
            {
                if ((c == '{' && lastChar == '{') ||
                    (c == '}' && lastChar == '}') ||
                    (c == '[' && lastChar == '[') ||
                    (c == ']' && lastChar == ']'))
                {
                    lastChar = c;
                    continue; // Skip the duplicate
                }
            }

            result.Append(c);
            lastChar = c;
        }

        return result.ToString();
    }

    private string BalanceDelimiters(string input)
    {
        // Determine if we're dealing with an object or array
        var firstBrace = input.IndexOf('{');
        var firstBracket = input.IndexOf('[');

        char openChar, closeChar;
        if (firstBrace >= 0 && (firstBracket < 0 || firstBrace < firstBracket))
        {
            openChar = '{';
            closeChar = '}';
        }
        else if (firstBracket >= 0)
        {
            openChar = '[';
            closeChar = ']';
        }
        else
        {
            return input; // No delimiters found
        }

        // Count delimiters while respecting strings
        int openCount = 0, closeCount = 0;
        bool inString = false;
        bool escaped = false;

        foreach (char c in input)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (c == openChar) openCount++;
                else if (c == closeChar) closeCount++;
            }
        }

        // Add missing closing delimiters
        if (openCount > closeCount)
        {
            return input + new string(closeChar, openCount - closeCount);
        }

        // Remove excess closing delimiters from the end
        if (closeCount > openCount)
        {
            var excess = closeCount - openCount;
            var result = input.TrimEnd();
            while (excess > 0 && result.Length > 0 && result[^1] == closeChar)
            {
                result = result.Substring(0, result.Length - 1).TrimEnd();
                excess--;
            }
            return result;
        }

        return input;
    }

    private string RemoveTrailingCommas(string input)
    {
        // This is a simple implementation that removes ", }" and ", ]" patterns
        // The JsonSerializerOptions.AllowTrailingCommas should handle most cases
        var result = input
            .Replace(",}", "}")
            .Replace(", }", "}")
            .Replace(",]", "]")
            .Replace(", ]", "]");

        return result;
    }
}
