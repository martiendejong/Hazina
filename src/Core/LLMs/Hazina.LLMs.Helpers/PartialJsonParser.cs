using System.Text.Json;
using System.Text.RegularExpressions;

public class PartialJsonParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    public static(int openBraces, int closeBraces) CountBraces(string input)
    {
        int openBraces = 0, closeBraces = 0;

        foreach (char c in input)
        {
            if (c == '{') openBraces++;
            else if (c == '}') closeBraces++;
        }

        return (openBraces, closeBraces);
    }

    public TResponse? Parse<TResponse>(string partialJson)
    {
        // Early validation: null, empty, or whitespace-only input
        if (string.IsNullOrWhiteSpace(partialJson))
        {
            return default(TResponse);
        }

        // Step 1: Try parsing as-is (for valid JSON)
        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(partialJson, JsonOptions);
            return result;
        }
        catch
        {
            // Continue to cleanup attempts
        }

        // Step 2: Apply common streaming JSON fixes
        string cleanedJson = partialJson;

        // Strip trailing commas before closing braces/brackets (common in streaming JSON)
        cleanedJson = Regex.Replace(cleanedJson, @",(\s*[}\]])", "$1", RegexOptions.None, TimeSpan.FromMilliseconds(100));

        // Try parsing after comma fix
        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(cleanedJson, JsonOptions);
            return result;
        }
        catch
        {
            // Continue to more aggressive fixes
        }

        // Step 3: Extract JSON structure (remove text before { or [)
        var startBrace = cleanedJson.IndexOf('{', StringComparison.Ordinal);
        var startBracket = cleanedJson.IndexOf('[', StringComparison.Ordinal);

        int start;
        char openChar, closeChar;

        if (startBrace >= 0 && startBracket >= 0)
        {
            // Both found - use whichever comes first
            if (startBrace < startBracket)
            {
                start = startBrace;
                openChar = '{';
                closeChar = '}';
            }
            else
            {
                start = startBracket;
                openChar = '[';
                closeChar = ']';
            }
        }
        else if (startBrace >= 0)
        {
            start = startBrace;
            openChar = '{';
            closeChar = '}';
        }
        else if (startBracket >= 0)
        {
            start = startBracket;
            openChar = '[';
            closeChar = ']';
        }
        else
        {
            // No JSON structure found - return default
            return default(TResponse);
        }

        // Extract the FIRST complete JSON structure
        // Find the matching closing delimiter by counting opens/closes
        int depth = 0;
        int end = -1;
        for (int i = start; i < cleanedJson.Length; i++)
        {
            if (cleanedJson[i] == openChar)
            {
                depth++;
            }
            else if (cleanedJson[i] == closeChar)
            {
                depth--;
                if (depth == 0)
                {
                    end = i;
                    break;
                }
            }
        }

        if (end < start)
        {
            // No closing bracket found - we'll add it later
            cleanedJson = cleanedJson.Substring(start).Trim();
        }
        else
        {
            cleanedJson = cleanedJson.Substring(start, end - start + 1).Trim();
        }

        // Step 4: Fix doubled delimiters ({{ or }} or [[ or ]])
        cleanedJson = cleanedJson
            .Replace("{{", "{", StringComparison.Ordinal)
            .Replace("}}", "}", StringComparison.Ordinal)
            .Replace("[[", "[", StringComparison.Ordinal)
            .Replace("]]", "]", StringComparison.Ordinal);

        // Step 5: Balance ALL braces and brackets (add missing closing delimiters)
        // Count both {} and []
        int openBraceCount = cleanedJson.Count(c => c == '{');
        int closeBraceCount = cleanedJson.Count(c => c == '}');
        int openBracketCount = cleanedJson.Count(c => c == '[');
        int closeBracketCount = cleanedJson.Count(c => c == ']');

        // Add missing closes in reverse order (inner before outer)
        // First close inner structures (braces), then outer structures (brackets)
        if (openBraceCount > closeBraceCount)
        {
            cleanedJson += new string('}', openBraceCount - closeBraceCount);
        }
        if (openBracketCount > closeBracketCount)
        {
            cleanedJson += new string(']', openBracketCount - closeBracketCount);
        }

        // Step 6: Handle incomplete string values (add closing quote if needed)
        // Count quotes to detect incomplete strings
        bool insideEscape = false;
        int actualQuoteCount = 0;

        for (int i = 0; i < cleanedJson.Length; i++)
        {
            if (cleanedJson[i] == '\\')
            {
                insideEscape = !insideEscape;
            }
            else if (cleanedJson[i] == '"' && !insideEscape)
            {
                actualQuoteCount++;
            }
            else
            {
                insideEscape = false;
            }
        }

        // If odd number of quotes, add closing quote before final brace
        if (actualQuoteCount % 2 != 0)
        {
            var lastCloseIndex = cleanedJson.LastIndexOf(closeChar);
            if (lastCloseIndex > 0)
            {
                cleanedJson = cleanedJson.Insert(lastCloseIndex, "\"");
            }
            else
            {
                cleanedJson += "\"";
            }
        }

        // Final attempt to parse
        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(cleanedJson, JsonOptions);
            return result;
        }
        catch
        {
            // All attempts failed - return default
            return default(TResponse);
        }
    }
}
