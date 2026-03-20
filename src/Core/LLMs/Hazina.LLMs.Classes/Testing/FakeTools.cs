using System.Text.Json;

/// <summary>
/// Pre-built fake tools for testing
/// </summary>
public static class FakeTools
{
    public static HazinaChatTool CreateEchoTool(string name = "echo")
    {
        return new HazinaChatTool(
            name,
            "Echo back the input",
            new List<ChatToolParameter> 
            { 
                new ChatToolParameter { Name = "message", Type = "string", Required = true } 
            },
            async (messages, toolCall, cancel) =>
            {
                using var doc = JsonDocument.Parse(toolCall.FunctionArguments.ToString());
                if (doc.RootElement.TryGetProperty("message", out var msg))
                    return msg.GetString() ?? string.Empty;
                return "No message provided";
            });
    }

    public static HazinaChatTool CreateCalculatorTool(string name = "calculator")
    {
        return new HazinaChatTool(
            name,
            "Basic arithmetic",
            new List<ChatToolParameter>
            {
                new ChatToolParameter { Name = "operation", Type = "string", Required = true },
                new ChatToolParameter { Name = "a", Type = "number", Required = true },
                new ChatToolParameter { Name = "b", Type = "number", Required = true }
            },
            async (messages, toolCall, cancel) =>
            {
                using var doc = JsonDocument.Parse(toolCall.FunctionArguments.ToString());
                var op = doc.RootElement.GetProperty("operation").GetString();
                var a = doc.RootElement.GetProperty("a").GetDouble();
                var b = doc.RootElement.GetProperty("b").GetDouble();
                
                double result = op?.ToLower() switch
                {
                    "add" => a + b,
                    "subtract" => a - b,
                    "multiply" => a * b,
                    "divide" => b != 0 ? a / b : throw new DivideByZeroException(),
                    _ => throw new ArgumentException($"Unknown operation: {op}")
                };
                
                return result.ToString();
            });
    }
}
