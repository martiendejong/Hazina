using System.Text.Json;

namespace Hazina.LLMs.Classes.Testing;

/// <summary>
/// Pre-built fake tools for common testing scenarios
/// </summary>
public static class FakeTools
{
    /// <summary>
    /// Create a fake echo tool that returns its input
    /// </summary>
    public static HazinaChatTool CreateEchoTool(string name = "echo")
    {
        return new HazinaChatTool(
            name,
            "Echo back the input message",
            new List<ChatToolParameter>
            {
                new ChatToolParameter
                {
                    Name = "message",
                    Type = "string",
                    Required = true,
                    Description = "Message to echo back"
                }
            },
            async (messages, toolCall, cancel) =>
            {
                using var doc = JsonDocument.Parse(toolCall.FunctionArguments);
                if (doc.RootElement.TryGetProperty("message", out var msg))
                {
                    return msg.GetString() ?? string.Empty;
                }
                return "No message provided";
            });
    }

    /// <summary>
    /// Create a fake calculator tool
    /// </summary>
    public static HazinaChatTool CreateCalculatorTool(string name = "calculator")
    {
        return new HazinaChatTool(
            name,
            "Perform basic arithmetic operations",
            new List<ChatToolParameter>
            {
                new ChatToolParameter
                {
                    Name = "operation",
                    Type = "string",
                    Required = true,
                    Description = "Operation: add, subtract, multiply, divide"
                },
                new ChatToolParameter
                {
                    Name = "a",
                    Type = "number",
                    Required = true,
                    Description = "First number"
                },
                new ChatToolParameter
                {
                    Name = "b",
                    Type = "number",
                    Required = true,
                    Description = "Second number"
                }
            },
            async (messages, toolCall, cancel) =>
            {
                using var doc = JsonDocument.Parse(toolCall.FunctionArguments);

                if (!doc.RootElement.TryGetProperty("operation", out var opElement) ||
                    !doc.RootElement.TryGetProperty("a", out var aElement) ||
                    !doc.RootElement.TryGetProperty("b", out var bElement))
                {
                    return "Error: Missing required parameters";
                }

                var operation = opElement.GetString();
                var a = aElement.GetDouble();
                var b = bElement.GetDouble();

                double result = operation?.ToLower() switch
                {
                    "add" => a + b,
                    "subtract" => a - b,
                    "multiply" => a * b,
                    "divide" => b != 0 ? a / b : throw new DivideByZeroException(),
                    _ => throw new ArgumentException($"Unknown operation: {operation}")
                };

                return result.ToString();
            });
    }

    /// <summary>
    /// Create a fake timer tool that waits for a specified duration
    /// </summary>
    public static HazinaChatTool CreateTimerTool(string name = "timer")
    {
        return new HazinaChatTool(
            name,
            "Wait for a specified number of seconds",
            new List<ChatToolParameter>
            {
                new ChatToolParameter
                {
                    Name = "seconds",
                    Type = "number",
                    Required = true,
                    Description = "Number of seconds to wait"
                }
            },
            async (messages, toolCall, cancel) =>
            {
                using var doc = JsonDocument.Parse(toolCall.FunctionArguments);

                if (doc.RootElement.TryGetProperty("seconds", out var secondsElement))
                {
                    var seconds = secondsElement.GetInt32();
                    await Task.Delay(TimeSpan.FromSeconds(seconds), cancel);
                    return $"Waited {seconds} seconds";
                }

                return "Error: seconds parameter not provided";
            });
    }

    /// <summary>
    /// Create a fake random number generator tool
    /// </summary>
    public static HazinaChatTool CreateRandomTool(string name = "random")
    {
        var random = new Random();

        return new HazinaChatTool(
            name,
            "Generate a random number",
            new List<ChatToolParameter>
            {
                new ChatToolParameter
                {
                    Name = "min",
                    Type = "number",
                    Required = false,
                    Description = "Minimum value (default: 0)"
                },
                new ChatToolParameter
                {
                    Name = "max",
                    Type = "number",
                    Required = false,
                    Description = "Maximum value (default: 100)"
                }
            },
            async (messages, toolCall, cancel) =>
            {
                using var doc = JsonDocument.Parse(toolCall.FunctionArguments);

                var min = 0;
                var max = 100;

                if (doc.RootElement.TryGetProperty("min", out var minElement))
                {
                    min = minElement.GetInt32();
                }

                if (doc.RootElement.TryGetProperty("max", out var maxElement))
                {
                    max = maxElement.GetInt32();
                }

                var result = random.Next(min, max + 1);
                return result.ToString();
            });
    }

    /// <summary>
    /// Create a fake tool that always fails
    /// </summary>
    public static HazinaChatTool CreateFailingTool(string name = "failing", string errorMessage = "This tool always fails")
    {
        return new HazinaChatTool(
            name,
            "A tool that always throws an exception",
            new List<ChatToolParameter>(),
            async (messages, toolCall, cancel) =>
            {
                throw new InvalidOperationException(errorMessage);
            });
    }

    /// <summary>
    /// Create a fake tool that returns deterministic results based on input
    /// </summary>
    public static HazinaChatTool CreateDeterministicTool(
        string name,
        Dictionary<string, string> inputOutputMap)
    {
        return new HazinaChatTool(
            name,
            "A tool with deterministic responses",
            new List<ChatToolParameter>
            {
                new ChatToolParameter
                {
                    Name = "input",
                    Type = "string",
                    Required = true,
                    Description = "Input key"
                }
            },
            async (messages, toolCall, cancel) =>
            {
                using var doc = JsonDocument.Parse(toolCall.FunctionArguments);

                if (doc.RootElement.TryGetProperty("input", out var inputElement))
                {
                    var input = inputElement.GetString() ?? string.Empty;

                    if (inputOutputMap.TryGetValue(input, out var output))
                    {
                        return output;
                    }

                    return $"No mapping found for input: {input}";
                }

                return "Error: input parameter not provided";
            });
    }

    /// <summary>
    /// Create a fake data store tool
    /// </summary>
    public static HazinaChatTool CreateDataStoreTool(string name = "datastore")
    {
        var store = new Dictionary<string, string>();

        return new HazinaChatTool(
            name,
            "Store and retrieve key-value pairs",
            new List<ChatToolParameter>
            {
                new ChatToolParameter
                {
                    Name = "action",
                    Type = "string",
                    Required = true,
                    Description = "Action: set, get, delete, list"
                },
                new ChatToolParameter
                {
                    Name = "key",
                    Type = "string",
                    Required = false,
                    Description = "Key for get/set/delete operations"
                },
                new ChatToolParameter
                {
                    Name = "value",
                    Type = "string",
                    Required = false,
                    Description = "Value for set operation"
                }
            },
            async (messages, toolCall, cancel) =>
            {
                using var doc = JsonDocument.Parse(toolCall.FunctionArguments);

                if (!doc.RootElement.TryGetProperty("action", out var actionElement))
                {
                    return "Error: action parameter required";
                }

                var action = actionElement.GetString()?.ToLower();

                switch (action)
                {
                    case "set":
                        if (doc.RootElement.TryGetProperty("key", out var setKeyElement) &&
                            doc.RootElement.TryGetProperty("value", out var valueElement))
                        {
                            var key = setKeyElement.GetString() ?? string.Empty;
                            var value = valueElement.GetString() ?? string.Empty;
                            store[key] = value;
                            return $"Stored: {key} = {value}";
                        }
                        return "Error: key and value required for set";

                    case "get":
                        if (doc.RootElement.TryGetProperty("key", out var getKeyElement))
                        {
                            var key = getKeyElement.GetString() ?? string.Empty;
                            if (store.TryGetValue(key, out var value))
                            {
                                return value;
                            }
                            return $"Key not found: {key}";
                        }
                        return "Error: key required for get";

                    case "delete":
                        if (doc.RootElement.TryGetProperty("key", out var delKeyElement))
                        {
                            var key = delKeyElement.GetString() ?? string.Empty;
                            if (store.Remove(key))
                            {
                                return $"Deleted: {key}";
                            }
                            return $"Key not found: {key}";
                        }
                        return "Error: key required for delete";

                    case "list":
                        if (store.Count == 0)
                        {
                            return "Store is empty";
                        }
                        return string.Join("\n", store.Select(kvp => $"{kvp.Key}: {kvp.Value}"));

                    default:
                        return $"Unknown action: {action}";
                }
            });
    }
}
