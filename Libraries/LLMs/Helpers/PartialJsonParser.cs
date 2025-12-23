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

        Console.WriteLine("Trying to correct the JSON by removing the first part before {");
        string correctedJson = "";
        try
        {
            var start = partialJson.IndexOf('{');
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
            var start = partialJson.IndexOf('{');
            if (start < 0)
                throw new Exception("Not valid JSON object");

            var end = partialJson.LastIndexOf('}');

            partialJson = partialJson.Substring(start, end - start + 1).Trim()
                //.Replace("\\\"", "\"")
                .Replace("{{", "{")
                .Replace("}}", "}");

            var braces = CountBraces(partialJson);
            if (braces.openBraces > braces.closeBraces)
            {
                partialJson += new string('}', braces.openBraces - braces.closeBraces);
            }

            jsonPart = partialJson;
            //jsonPart = FixInvalidJsonQuotes(jsonPart);

            var json = JsonSerializer.Deserialize<TResponse>(jsonPart);

            return json;
        }
        catch (Exception e)
        {
            Console.WriteLine("Error parsing the JSON");
            Console.WriteLine(e.Message);
            Console.WriteLine(partialJson);
            Console.WriteLine();
            Console.WriteLine(jsonPart);
            throw;
        }
    }
}
