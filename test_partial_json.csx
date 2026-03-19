using System.Text.Json;

var parser = new PartialJsonParser();
var json = "{\"name\": \"test\", \"value\": 42}";

Console.WriteLine("Testing: " + json);

try {
    var result = parser.Parse<TestObject>(json);
    Console.WriteLine($"Result: name={result?.name}, value={result?.value}");
} catch (Exception ex) {
    Console.WriteLine($"Error: {ex.Message}");
}

public class TestObject
{
    public string? name { get; set; }
    public int value { get; set; }
}

public class PartialJsonParser
{
    public TResponse? Parse<TResponse>(string partialJson)
    {
        Console.WriteLine("Parse called with: " + partialJson);

        // Strategy 1: Try direct parse first
        var result = TryDirectParse<TResponse>(partialJson);
        Console.WriteLine($"Strategy 1 result: success={result.success}");
        if (result.success) return result.value;

        // Strategy 2: Remove leading garbage before { or [
        result = TryRemoveLeadingGarbage<TResponse>(partialJson);
        Console.WriteLine($"Strategy 2 result: success={result.success}");
        if (result.success) return result.value;

        Console.WriteLine("All strategies failed");
        return default;
    }

    private static (bool success, TResponse? value, string? correctedJson) TryDirectParse<TResponse>(string json)
    {
        try
        {
            var result = JsonSerializer.Deserialize<TResponse>(json);
            return (true, result, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"TryDirectParse exception: {ex.Message}");
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
        catch (Exception ex)
        {
            Console.WriteLine($"TryRemoveLeadingGarbage exception: {ex.Message}");
            return (false, default, null);
        }
    }

    private static (int start, bool found) FindJsonStart(string json)
    {
        var startBrace = json.IndexOf('{');
        var startBracket = json.IndexOf('[');

        if (startBrace >= 0 && startBracket >= 0)
        {
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
}
