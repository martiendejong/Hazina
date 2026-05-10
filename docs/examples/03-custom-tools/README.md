# Custom Tools - Function Calling

**Give your AI the ability to call external functions and APIs**

## What You'll Learn

- How to define custom tools (functions) that AI can call
- How to register tools with the LLM
- How to handle tool calls and return results
- How multi-turn tool calling works
- Real-world tool patterns (weather, calculations, database queries)

## Prerequisites

- .NET 8.0 or higher
- OpenAI API key (function calling requires GPT-3.5-turbo-1106 or GPT-4)

## What are Tools (Function Calling)?

**Tools** (also called "function calling") allow the AI to:

1. **Detect when it needs external data** (e.g., "What's the weather?" → needs weather API)
2. **Request function execution** with parameters (e.g., `get_weather(location="Paris")`)
3. **Incorporate results** into its response (e.g., "The weather in Paris is 18°C...")

**This makes AI applications dynamic** — not limited to training data.

## Running the Example

```bash
# Set your API key
export OPENAI_API_KEY=sk-your-key-here

# Run
dotnet run
```

Expected output:
```
=== Custom Tools Example ===

✓ AI client initialized
✓ Registered 3 custom tools

--- Example 1: Weather Query ---

User: What's the weather like in Paris?

[TOOL CALLED] get_weather(location='Paris', unit='celsius')
Assistant: The current weather in Paris is 18°C and partly cloudy, with 65% humidity and winds at 12 km/h.

Tokens used: 156

------------------------------------------------------------

--- Example 2: Mortgage Calculation ---

User: Calculate monthly payment for a $300,000 loan at 4.5% interest for 30 years

[TOOL CALLED] calculate_mortgage(principal=$300,000, rate=4.5%, years=30)
Assistant: For a $300,000 mortgage at 4.5% interest over 30 years, your monthly payment would be $1,520.06.
Over the life of the loan, you'll pay a total of $547,221.60, including $247,221.60 in interest.

Tokens used: 182

✓ Success! Your custom tools are working.
```

## Code Walkthrough

### 1. Define a Tool

```csharp
using Hazina.Tools.Models;

var tools = new ToolsContext();

tools.RegisterTool(new ToolDefinition
{
    Name = "get_weather",
    Description = "Get the current weather for a location",
    Parameters = new ToolParameters
    {
        Properties = new Dictionary<string, ToolProperty>
        {
            ["location"] = new ToolProperty
            {
                Type = "string",
                Description = "The city name (e.g., 'London', 'New York')"
            },
            ["unit"] = new ToolProperty
            {
                Type = "string",
                Description = "Temperature unit (celsius or fahrenheit)",
                Enum = new[] { "celsius", "fahrenheit" }
            }
        },
        Required = new[] { "location" }
    },
    Handler = async (args) =>
    {
        var location = args["location"]?.ToString() ?? "Unknown";
        var unit = args.ContainsKey("unit") ? args["unit"]?.ToString() : "celsius";

        // Call actual weather API here
        var weatherData = await CallWeatherAPI(location, unit);

        return JsonSerializer.Serialize(weatherData);
    }
});
```

**What's happening:**
- **Name**: The function identifier (AI uses this to call the tool)
- **Description**: Tells AI when to use this tool (critical for accuracy!)
- **Parameters**: Define inputs with types, descriptions, and required fields
- **Handler**: Your actual implementation (async function that returns JSON string)

### 2. Pass Tools to LLM

```csharp
var response = await ai.GetResponse(
    messages,
    HazinaChatResponseFormat.Text,
    tools,  // <-- Tools context
    null,
    CancellationToken.None
);
```

**What's happening:**
- LLM receives tool definitions
- LLM decides if it needs to call a tool
- If yes, response contains `ToolCalls` instead of text

### 3. Handle Tool Calls

```csharp
if (response.ToolCalls != null && response.ToolCalls.Count > 0)
{
    // Execute each tool
    foreach (var toolCall in response.ToolCalls)
    {
        var result = await tools.ExecuteToolAsync(toolCall.Name, toolCall.Arguments);

        // Add result to conversation
        messages.Add(new HazinaChatMessage
        {
            Role = HazinaMessageRole.Tool,
            ToolCallId = toolCall.Id,
            Text = result
        });
    }

    // Send tool results back to LLM for final response
    var finalResponse = await ai.GetResponse(messages, ...);
}
```

**What's happening:**
- Tool calls contain function name + arguments (JSON)
- You execute the function with provided arguments
- Return result as JSON string
- Add result to conversation with `Role = Tool`
- LLM uses result to generate final response

## Tool Conversation Flow

```
User: "What's the weather in Paris?"
  ↓
[Turn 1] LLM receives question + tool definitions
  ↓
LLM decides: "I need weather data" → requests get_weather(location="Paris")
  ↓
[Your code] Executes get_weather("Paris") → returns {"temperature": 18, "condition": "Cloudy"}
  ↓
[Turn 2] LLM receives tool result
  ↓
LLM generates: "The weather in Paris is 18°C and cloudy."
  ↓
User sees final response
```

**Key insight**: Multi-turn conversation — AI can request tools, get results, and formulate answer.

## Real-World Tool Examples

### Weather API Tool

```csharp
tools.RegisterTool(new ToolDefinition
{
    Name = "get_weather",
    Description = "Get current weather for any location",
    Parameters = new ToolParameters { /* ... */ },
    Handler = async (args) =>
    {
        var location = args["location"]?.ToString();
        using var httpClient = new HttpClient();
        var apiUrl = $"https://api.openweathermap.org/data/2.5/weather?q={location}&appid={weatherApiKey}";
        var response = await httpClient.GetStringAsync(apiUrl);
        return response; // Already JSON
    }
});
```

### Database Query Tool

```csharp
tools.RegisterTool(new ToolDefinition
{
    Name = "query_customers",
    Description = "Search customer database by name, email, or ID",
    Parameters = new ToolParameters { /* ... */ },
    Handler = async (args) =>
    {
        var query = args["query"]?.ToString();
        await using var connection = new SqlConnection(connectionString);
        var command = new SqlCommand("SELECT * FROM Customers WHERE Name LIKE @query OR Email LIKE @query", connection);
        command.Parameters.AddWithValue("@query", $"%{query}%");

        await connection.OpenAsync();
        var reader = await command.ExecuteReaderAsync();

        var results = new List<object>();
        while (await reader.ReadAsync())
        {
            results.Add(new
            {
                id = reader["Id"],
                name = reader["Name"],
                email = reader["Email"]
            });
        }

        return JsonSerializer.Serialize(results);
    }
});
```

### Email Sending Tool

```csharp
tools.RegisterTool(new ToolDefinition
{
    Name = "send_email",
    Description = "Send an email to a recipient",
    Parameters = new ToolParameters
    {
        Properties = new Dictionary<string, ToolProperty>
        {
            ["to"] = new ToolProperty { Type = "string", Description = "Recipient email address" },
            ["subject"] = new ToolProperty { Type = "string", Description = "Email subject" },
            ["body"] = new ToolProperty { Type = "string", Description = "Email body content" }
        },
        Required = new[] { "to", "subject", "body" }
    },
    Handler = async (args) =>
    {
        var to = args["to"]?.ToString();
        var subject = args["subject"]?.ToString();
        var body = args["body"]?.ToString();

        // Send email using SMTP or email service
        await emailService.SendAsync(to, subject, body);

        return JsonSerializer.Serialize(new { status = "sent", timestamp = DateTime.UtcNow });
    }
});
```

## Tool Design Best Practices

### 1. Clear, Specific Descriptions

**Bad**:
```csharp
Description = "Gets data"
```

**Good**:
```csharp
Description = "Get the current weather (temperature, condition, humidity) for any city or location worldwide"
```

**Why**: AI uses description to decide when to call the tool. Vague descriptions → wrong tool selection.

### 2. Detailed Parameter Descriptions

**Bad**:
```csharp
["location"] = new ToolProperty { Type = "string" }
```

**Good**:
```csharp
["location"] = new ToolProperty
{
    Type = "string",
    Description = "The city name (e.g., 'London', 'Tokyo') or coordinates (e.g., '40.7128,-74.0060')"
}
```

### 3. Use Enums for Limited Choices

```csharp
["unit"] = new ToolProperty
{
    Type = "string",
    Description = "Temperature unit",
    Enum = new[] { "celsius", "fahrenheit", "kelvin" }
}
```

**Why**: Guides AI to use valid values only.

### 4. Mark Required vs Optional

```csharp
Parameters = new ToolParameters
{
    Properties = new Dictionary<string, ToolProperty>
    {
        ["required_field"] = new ToolProperty { Type = "string", Description = "..." },
        ["optional_field"] = new ToolProperty { Type = "string", Description = "..." }
    },
    Required = new[] { "required_field" }  // <-- Only required fields
}
```

### 5. Return Structured JSON

**Bad**:
```csharp
return "The temperature is 18 degrees";  // Hard for AI to parse
```

**Good**:
```csharp
return JsonSerializer.Serialize(new
{
    temperature = 18,
    unit = "celsius",
    condition = "cloudy"
});
```

**Why**: Structured data is easier for AI to interpret and incorporate into responses.

## Multi-Tool Calls

AI can call **multiple tools in one turn**:

```csharp
User: "What's the weather in Paris and New York?"

// AI calls both tools:
- get_weather(location="Paris")
- get_weather(location="New York")

// You execute both, return results
// AI combines results: "Paris is 18°C and cloudy, while New York is 22°C and sunny."
```

Handle multiple calls:
```csharp
foreach (var toolCall in response.ToolCalls)
{
    var result = await tools.ExecuteToolAsync(toolCall.Name, toolCall.Arguments);
    messages.Add(new HazinaChatMessage
    {
        Role = HazinaMessageRole.Tool,
        ToolCallId = toolCall.Id,  // Important: matches request to result
        Text = result
    });
}
```

## Security Considerations

### 1. Validate Tool Arguments

```csharp
Handler = async (args) =>
{
    var email = args["email"]?.ToString();

    // Validate before executing
    if (!IsValidEmail(email))
        return JsonSerializer.Serialize(new { error = "Invalid email address" });

    // Safe to proceed
    await SendEmail(email);
}
```

### 2. Rate Limiting

```csharp
private static SemaphoreSlim _rateLimiter = new SemaphoreSlim(5, 5); // Max 5 concurrent calls

Handler = async (args) =>
{
    await _rateLimiter.WaitAsync();
    try
    {
        // Execute tool
    }
    finally
    {
        _rateLimiter.Release();
    }
}
```

### 3. Restrict Dangerous Operations

```csharp
// DON'T expose tools that can:
// - Delete data without confirmation
// - Execute arbitrary code
// - Access sensitive user data without authorization

// DO expose tools that:
// - Read public data
// - Perform safe calculations
// - Create draft content (user reviews before sending)
```

## Troubleshooting

### AI Not Calling Tools

**Problem**: AI responds without calling your tool.

**Solutions**:
1. Improve tool description (be more specific about when to use it)
2. Add examples in system prompt:
   ```csharp
   messages.Add(new HazinaChatMessage
   {
       Role = HazinaMessageRole.System,
       Text = "When users ask about weather, ALWAYS use the get_weather tool."
   });
   ```
3. Use GPT-4 (better at tool selection than GPT-3.5)

### Tool Arguments Missing/Wrong

**Problem**: AI calls tool with wrong parameters.

**Solutions**:
1. Improve parameter descriptions
2. Add `Enum` for limited choices
3. Mark required parameters explicitly

### Infinite Tool Calling Loop

**Problem**: AI keeps calling tools without generating final response.

**Solution**: Add max turns limit:
```csharp
int maxTurns = 5;
for (int turn = 0; turn < maxTurns; turn++)
{
    // ... tool calling logic
}
```

## Next Steps

- [Basic RAG](../04-basic-rag/) - Combine tools with document retrieval
- [Agent Orchestration](../05-agent-orchestration/) - Agents with multiple tools
- [Tool Providers](../16-tool-providers/) - Advanced tool architecture
- [Database Tools](../../src/Tools/Hazina.Tools.Database/) - Pre-built database tools

## Full Code

See [Program.cs](Program.cs) for the complete, runnable code.

---

**Congratulations! You've unlocked AI function calling.**

Your AI can now interact with external APIs, databases, and services — not limited to its training data!
