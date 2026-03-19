using System.Text.Json;
using System.Text.RegularExpressions;

namespace Hazina.LLMs;

/// <summary>
/// Parser for handling partial or malformed JSON strings, commonly encountered when streaming LLM responses.
/// Attempts multiple recovery strategies to extract valid JSON from incomplete or corrupted input.
/// </summary>
public class PartialJsonParser
{
    /// <summary>
    /// Counts the number of opening and closing braces in the input string.
    /// </summary>
    /// <param name="input">The input string to analyze</param>
    /// <returns>A tuple containing the count of opening and closing braces</returns>
    public static (int openBraces, int closeBraces) CountBraces(string input)
    {
        int openBraces = 0, closeBraces = 0;

        foreach (char c in input)
        {
            if (c == '{') openBraces++;
            else if (c == '}') closeBraces++;
        }

        return (openBraces, closeBraces);
    }

    /// <summary>
    /// Attempts to parse a potentially partial or malformed JSON string into a strongly-typed object.
    /// Employs multiple recovery strategies in sequence if direct parsing fails.
    /// </summary>
    /// <typeparam name="TResponse">The target type to deserialize into</typeparam>
    /// <param name="partialJson">The JSON string to parse</param>
    /// <returns>The deserialized object if successful, null otherwise</returns>
    public TResponse? Parse<TResponse>(string partialJson)
    {
        // Strategy 1: Try direct parsing first
        var result = TryDirectParse<TResponse>(partialJson);
        if (result.success) return result.value;

        // Strategy 2: Remove leading garbage before { or [
        result = TryRemoveLeadingGarbage<TResponse>(partialJson);
        if (result.success) return result.value;

        // Strategy 3: Fix quote escaping in string values
        result = TryFixQuoteEscaping<TResponse>(result.correctedJson ?? partialJson);
        if (result.success) return result.value;

        // Strategy 4: Remove trailing garbage after last }
        result = TryRemoveTrailingGarbage<TResponse>(result.correctedJson ?? partialJson);
        if (result.success) return result.value;

        // Strategy 5: Balance braces/brackets
        result = TryBalanceBraces<TResponse>(partialJson);
        if (result.success) return result.value;

        // All strategies failed
        Console.WriteLine("PartialJsonParser: All parsing strategies failed");
        return default;
    }

    private static (bool success, TResponse? value, string? correctedJson) TryDirectParse<TResponse>(string json)
    {
        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(json);
            return (true, result, json);
        }
        catch
        {
            return (false, default, json);
        }
    }

    private static (bool success, TResponse? value, string? correctedJson) TryRemoveLeadingGarbage<TResponse>(string json)
    {
        try
        {
            var (start, found) = FindJsonStart(json);
            if (!found)
            {
                return (false, default, json);
            }

            var correctedJson = json.Substring(start);
            var result = JsonSerializer.Deserialize<TResponse>(correctedJson);
            return (true, result, correctedJson);
        }
        catch
        {
            return (false, default, null);
        }
    }

    private static (bool success, TResponse? value, string? correctedJson) TryFixQuoteEscaping<TResponse>(string json)
    {
        try
        {
            var correctedJson = EscapeQuotesInStringValues(json);
            var result = JsonSerializer.Deserialize<TResponse>(correctedJson);
            return (true, result, correctedJson);
        }
        catch
        {
            return (false, default, json);
        }
    }

    private static (bool success, TResponse? value, string? correctedJson) TryRemoveTrailingGarbage<TResponse>(string json)
    {
        try
        {
            var end = json.LastIndexOf('}');
            if (end == -1)
            {
                end = json.LastIndexOf(']');
            }

            if (end == -1)
            {
                return (false, default, json);
            }

            var correctedJson = json.Substring(0, end + 1);
            var result = JsonSerializer.Deserialize<TResponse>(correctedJson);
            return (true, result, correctedJson);
        }
        catch
        {
            return (false, default, json);
        }
    }

    private static (bool success, TResponse? value, string? correctedJson) TryBalanceBraces<TResponse>(string json)
    {
        try
        {
            var (start, found) = FindJsonStart(json);
            if (!found)
            {
                return (false, default, json);
            }

            char openChar = json[start];
            char closeChar = openChar == '{' ? '}' : ']';

            var end = json.LastIndexOf(closeChar);
            if (end == -1)
            {
                return (false, default, json);
            }

            var correctedJson = json.Substring(start, end - start + 1).Trim();

            // Clean up doubled delimiters
            correctedJson = correctedJson
                .Replace(new string(openChar, 2), openChar.ToString())
                .Replace(new string(closeChar, 2), closeChar.ToString());

            // Count and balance delimiters
            int openCount = correctedJson.Count(c => c == openChar);
            int closeCount = correctedJson.Count(c => c == closeChar);

            if (openCount > closeCount)
            {
                correctedJson += new string(closeChar, openCount - closeCount);
            }

            var result = JsonSerializer.Deserialize<TResponse>(correctedJson);
            return (true, result, correctedJson);
        }
        catch
        {
            return (false, default, json);
        }
    }

    private static (int start, bool found) FindJsonStart(string json)
    {
        var startBrace = json.IndexOf('{');
        var startBracket = json.IndexOf('[');

        if (startBrace >= 0 && startBracket >= 0)
        {
            // Both found - use whichever comes first
            return (Math.Min(startBrace, startBracket), true);
        }
        else if (startBrace >= 0)
        {
            return (startBrace, true);
        }
        else if (startBracket >= 0)
        {
            return (startBracket, true);
        }

        return (-1, false);
    }

    private static string EscapeQuotesInStringValues(string json)
    {
        var result = json;
        var index = 0;
        var sequence = "";
        bool inString = false;
        var startSequenceIndex = 0;
        var startStringIndex = 0;

        while (index < result.Length)
        {
            var c = result[index];

            if (inString)
            {
                sequence = UpdateSequence(c, sequence, ref startSequenceIndex, index);

                if (sequence == "\",\"")
                {
                    // End of string value
                    sequence = "";
                    inString = false;
                    var endStringIndex = startSequenceIndex;
                    var stringLength = endStringIndex - startStringIndex;
                    var stringValue = result.Substring(startStringIndex, stringLength);
                    var fixedStringValue = EscapeUnescapedQuotes(stringValue);

                    result = result
                        .Remove(startStringIndex, stringLength)
                        .Insert(startStringIndex, fixedStringValue);

                    index += fixedStringValue.Length - stringValue.Length;
                }
            }
            else
            {
                sequence = UpdateSequence(c, sequence, ref startSequenceIndex, index);

                if (sequence == "\":\"")
                {
                    // Start of string value
                    inString = true;
                    sequence = "";
                    startStringIndex = index + 1;
                    startSequenceIndex = index + 1;
                }
            }

            ++index;
        }

        return result;
    }

    private static string UpdateSequence(char c, string sequence, ref int startSequenceIndex, int currentIndex)
    {
        switch (c)
        {
            case ' ':
                return sequence;
            case '"':
            case ':':
            case ',':
                return sequence + c;
            default:
                startSequenceIndex = currentIndex + 1;
                return "";
        }
    }

    private static string EscapeUnescapedQuotes(string text)
    {
        return Regex.Replace(text, @"(?<!\\)""", "\\\"");
    }
}
