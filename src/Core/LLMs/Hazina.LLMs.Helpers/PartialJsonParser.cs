using System.Text.Json;
using System.Text.RegularExpressions;

public class PartialJsonParser
{
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

    static string FixInvalidJsonQuotes(string json)
    {
        return Regex.Replace(json, "(?<=:\\s*\"(?:[^\"\\\\]|\\\\.)*)\"(?=(?:[^\"\\\\]|\\\\.)*\\n)", "\\\\\"");
    }

    public TResponse? Parse<TResponse>(string partialJson)
    {
        // Early validation: null, empty, or whitespace-only input
        if (string.IsNullOrWhiteSpace(partialJson))
        {
            return default(TResponse);
        }

        // Strip trailing commas before closing braces/brackets (common in streaming JSON)
        partialJson = Regex.Replace(partialJson, @",(\s*[}\]])", "$1");

        try
        {
            var json = JsonSerializer.Deserialize<TResponse>(partialJson);
            return json;
        }
        catch(Exception e)
        {
            Console.WriteLine("Error parsing the JSON");
            Console.WriteLine(partialJson);
            Console.WriteLine(e.Message);
        }

        Console.WriteLine("Trying to correct the JSON by removing the first part before { or [");
        string correctedJson = "";
        try
        {
            // Support both objects {...} and arrays [...]
            var startBrace = partialJson.IndexOf('{');
            var startBracket = partialJson.IndexOf('[');

            int start;
            if (startBrace >= 0 && startBracket >= 0)
            {
                // Both found - use whichever comes first
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
                throw new Exception("Not valid JSON object or array");
            }

            correctedJson = partialJson.Substring(start);

            var json = JsonSerializer.Deserialize<TResponse>(correctedJson);
            return json;
        }
        catch (Exception e)
        {
            Console.WriteLine("Error parsing the corrected JSON");
            Console.WriteLine(e.Message);
            Console.WriteLine(correctedJson);
        }

        Console.WriteLine("Trying to correct the JSON by escaping quotes in string parameter values");
        try
        {
            var escapeQuotes = (string text) => 
            {
                return Regex.Replace(text, @"(?<!\\)""", "\\\"");
            };

            var index = 0;
            var sequence = "";
            bool inString = false;
            var startSequenceIndex = 0;
            var startStringIndex = 0;
            var endStringIndex = 0;
            while (index > -1 && index < correctedJson.Length)
            {
                var c = correctedJson[index];
                if (inString)
                {
                    switch (c)
                    {
                        case ' ':
                            break;
                        case '"':
                        case ':':
                            sequence += c;
                            break;
                        default:
                            sequence = "";
                            startSequenceIndex = index + 1;
                            break;
                    }
                    switch (sequence)
                    {
                        case "\",\"":
                            sequence = "";
                            inString = false;
                            endStringIndex = startSequenceIndex;
                            var stringLength = endStringIndex - startStringIndex;
                            var stringValue = correctedJson.Substring(startStringIndex, stringLength);
                            var fixedStringValue = escapeQuotes(stringValue);
                            correctedJson = correctedJson
                                .Remove(startStringIndex, stringLength)
                                .Insert(startStringIndex, fixedStringValue);
                            index += fixedStringValue.Length - stringValue.Length;

                            break;
                    }
                }
                else
                {
                    switch (c)
                    {
                        case ' ':
                            break;
                        case '"':
                        case ':':
                            sequence += c;
                            break;
                        default:
                            sequence = "";
                            break;
                    }
                    switch (sequence)
                    {
                        case "\":\"":
                            inString = true;
                            sequence = "";
                            startStringIndex = index + 1;
                            startSequenceIndex = index + 1;
                            break;
                    }
                }

                ++index;
            }

            if(inString)
            {
                var stringValue = correctedJson.Substring(startStringIndex);
                // todo fix quotes in value
            }

            var json = JsonSerializer.Deserialize<TResponse>(correctedJson);
            return json;
        }
        catch (Exception e)
        {
            Console.WriteLine("Error parsing the corrected JSON");
            Console.WriteLine(e.Message);
            Console.WriteLine(correctedJson);
        }


        Console.WriteLine("Trying to correct the JSON by removing the text after the last }");
        try
        {
            var end = correctedJson.IndexOf('}');
            correctedJson = correctedJson.Substring(0, end);

            var json = JsonSerializer.Deserialize<TResponse>(correctedJson);
            return json;
        }
        catch (Exception e)
        {
            Console.WriteLine("Error parsing the corrected JSON");
            Console.WriteLine(e.Message);
            Console.WriteLine(correctedJson);
        }


        /*
         * ignore spaces
         * find next string parameter value ":"
         * find next end string parameter value "," or "} or "]
         * make sure all quotes inside are escaped
         */



        /* start at the beginning
        ignore spaces
        {
            <<object contents>>
                "
                    <<param contents>>
                    read param name
                    find next
                        ":
                            <<param value>>
                            "
                                <<param value contents>>
                                read param string value
                                find next
                                    ","
                                        finish param(name, value)
                                        goto <<param contents>>
                                    "}
                                        finish param(name, value)
                                        finish object
                                            ,
                                            }
                                                }
                                                ]
                                                ,
                                            ]
                            {
                                goto <<object contents>>

            }
        */







        string jsonPart = "";
        try
        {
            // Support both objects {...} and arrays [...]
            var startBrace = partialJson.IndexOf('{');
            var startBracket = partialJson.IndexOf('[');

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
                Console.WriteLine("No valid JSON object or array structure found");
                return default(TResponse);
            }

            var end = partialJson.LastIndexOf(closeChar);

            partialJson = partialJson.Substring(start, end - start + 1).Trim();

            // Clean up doubled delimiters
            if (openChar == '{')
            {
                partialJson = partialJson
                    .Replace("{{", "{")
                    .Replace("}}", "}");
            }
            else
            {
                partialJson = partialJson
                    .Replace("[[", "[")
                    .Replace("]]", "]");
            }

            // Count and balance delimiters
            int openCount = partialJson.Count(c => c == openChar);
            int closeCount = partialJson.Count(c => c == closeChar);
            if (openCount > closeCount)
            {
                partialJson += new string(closeChar, openCount - closeCount);
            }

            jsonPart = partialJson;
            //jsonPart = FixInvalidJsonQuotes(jsonPart);

            var json = JsonSerializer.Deserialize<TResponse>(jsonPart);

            return json;
        }
        catch (Exception e)
        {
            Console.WriteLine("Error parsing the JSON - all fallback attempts failed");
            Console.WriteLine(e.Message);
            Console.WriteLine(partialJson);
            Console.WriteLine();
            Console.WriteLine(jsonPart);

            // Return default instead of throwing - graceful degradation for unparseable input
            return default(TResponse);
        }
    }
}
