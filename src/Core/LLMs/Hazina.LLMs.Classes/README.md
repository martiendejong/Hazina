# Hazina.LLMs.Classes

Common LLM data contracts, models, and shared classes for chat messages, tool calls, response formats, and token usage tracking. This library provides the foundational types used across all LLM providers and clients in the Hazina platform.

## Overview

Hazina.LLMs.Classes defines the core data models and contracts for working with Large Language Models:

- **Chat Models**: Message structures, roles, and conversation handling
- **Tool Models**: Function calling and tool execution definitions
- **Response Models**: Structured responses with token usage tracking
- **Format Models**: Response format specifications (text, JSON)
- **Image Models**: Image generation data structures

All LLM provider implementations (OpenAI, Anthropic, Gemini, etc.) use these common types to ensure consistency and interoperability.

## Quick Start

### Installation

```xml
<ProjectReference Include="..\path\to\Hazina.LLMs.Classes\Hazina.LLMs.Classes.csproj" />
```

### Basic Usage

```csharp
// Create chat messages
var userMessage = new HazinaChatMessage(HazinaMessageRole.User, "Hello!");
var assistantMessage = new HazinaChatMessage(HazinaMessageRole.Assistant, "Hi there!");

// Track token usage
var tokenUsage = new TokenUsageInfo(
    inputTokens: 10,
    outputTokens: 15,
    inputCost: 0.00001m,
    outputCost: 0.00003m,
    modelName: "gpt-4"
);

// Create response with usage tracking
var response = new LLMResponse<string>("Hello!", tokenUsage);
Console.WriteLine($"Total tokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Total cost: ${response.TokenUsage.TotalCost}");
```

## Key Classes

### HazinaChatMessage

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Classes\Models\Chat\HazinaChatMessage.cs`

Represents a single message in a chat conversation with support for agents, functions, and flows.

**Properties**:
```csharp
public Guid MessageId { get; set; }
public HazinaMessageRole Role { get; set; }
public string Text { get; set; }
public string AgentName { get; set; }
public string FunctionName { get; set; }
public string FlowName { get; set; }
public string Response { get; set; }
```

**Constructors**:
```csharp
public HazinaChatMessage()
public HazinaChatMessage(HazinaMessageRole role, string text)
public HazinaChatMessage(HazinaMessageRole role, string text, string agentName,
    string functionName, string flowName, string response)
```

**Usage Example**:
```csharp
// Simple message
var message = new HazinaChatMessage(HazinaMessageRole.User, "What's the weather?");

// Message with agent and function context
var agentMessage = new HazinaChatMessage(
    HazinaMessageRole.Assistant,
    "Checking weather...",
    agentName: "WeatherAgent",
    functionName: "GetWeather",
    flowName: "WeatherFlow",
    response: "It's sunny, 72°F"
);

Console.WriteLine($"Message ID: {message.MessageId}");
Console.WriteLine($"Role: {message.Role}");
Console.WriteLine($"Text: {message.Text}");
```

### HazinaMessageRole

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Classes\Models\Chat\HazinaMessageRole.cs`

Enum defining chat message roles.

**Values**:
```csharp
public enum HazinaMessageRole
{
    System,
    User,
    Assistant,
    Tool,
    Function
}
```

**Usage Example**:
```csharp
var systemMessage = new HazinaChatMessage(HazinaMessageRole.System, "You are a helpful assistant.");
var userMessage = new HazinaChatMessage(HazinaMessageRole.User, "Hello!");
var assistantMessage = new HazinaChatMessage(HazinaMessageRole.Assistant, "Hi there!");
```

### LLMResponse<T>

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Classes\Models\Chat\LLMResponse.cs`

Generic response wrapper that includes the result and token usage information.

**Properties**:
```csharp
public T Result { get; set; }
public TokenUsageInfo TokenUsage { get; set; }
```

**Constructor**:
```csharp
public LLMResponse(T result, TokenUsageInfo tokenUsage)
```

**Usage Example**:
```csharp
// String response
var stringResponse = new LLMResponse<string>(
    "The answer is 42",
    new TokenUsageInfo(50, 10, 0.0001m, 0.00002m, "gpt-4")
);

// Structured response
var structuredResponse = new LLMResponse<MyData>(
    new MyData { Value = "result" },
    new TokenUsageInfo(100, 50, 0.0002m, 0.0001m, "gpt-4-turbo")
);

Console.WriteLine($"Result: {stringResponse.Result}");
Console.WriteLine($"Total cost: ${stringResponse.TokenUsage.TotalCost:F6}");
```

### TokenUsageInfo

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Classes\Models\Chat\TokenUsageInfo.cs`

Tracks token usage and costs for LLM operations with support for aggregation.

**Properties**:
```csharp
public int InputTokens { get; set; }
public int OutputTokens { get; set; }
public int TotalTokens => InputTokens + OutputTokens;
public decimal InputCost { get; set; }
public decimal OutputCost { get; set; }
public decimal TotalCost => InputCost + OutputCost;
public string ModelName { get; set; }
```

**Constructor**:
```csharp
public TokenUsageInfo(int inputTokens, int outputTokens, decimal inputCost,
    decimal outputCost, string modelName = "")
```

**Operators**:
```csharp
public static TokenUsageInfo operator +(TokenUsageInfo a, TokenUsageInfo b)
```

**Usage Example**:
```csharp
var usage1 = new TokenUsageInfo(100, 50, 0.0001m, 0.00005m, "gpt-4");
var usage2 = new TokenUsageInfo(200, 100, 0.0002m, 0.0001m, "gpt-4");

// Aggregate usage
var total = usage1 + usage2;
Console.WriteLine($"Total tokens: {total.TotalTokens}"); // 450
Console.WriteLine($"Total cost: ${total.TotalCost:F6}"); // $0.000350
Console.WriteLine($"Model: {total.ModelName}"); // gpt-4

// Calculate cost per token
decimal costPerToken = total.TotalCost / total.TotalTokens;
Console.WriteLine($"Cost per token: ${costPerToken:F8}");
```

### HazinaChatTool

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Classes\Models\Tools\HazinaChatTool.cs`

Defines a tool/function that can be called by the LLM during conversation.

**Properties**:
```csharp
public string FunctionName { get; set; }
public string Description { get; set; }
public List<ChatToolParameter> Parameters { get; set; }
public Func<List<HazinaChatMessage>, HazinaChatToolCall, CancellationToken, Task<string>> Execute { get; set; }
```

**Constructor**:
```csharp
public HazinaChatTool(string name, string description, List<ChatToolParameter> parameters,
    Func<List<HazinaChatMessage>, HazinaChatToolCall, CancellationToken, Task<string>> execute)
```

**Static Methods**:
```csharp
public static async Task<string> CallTool(Func<Task<string>> action, CancellationToken cancel)
```

**Usage Example**:
```csharp
// Define a weather tool
var weatherTool = new HazinaChatTool(
    name: "get_weather",
    description: "Get current weather for a location",
    parameters: new List<ChatToolParameter>
    {
        new ChatToolParameter
        {
            Name = "location",
            Description = "City name",
            Type = "string",
            Required = true
        }
    },
    execute: async (messages, toolCall, cancel) =>
    {
        // Extract location from tool call
        var param = weatherTool.Parameters[0];
        if (param.TryGetValue(toolCall, out string location))
        {
            // Call weather API
            return $"Weather in {location}: Sunny, 72°F";
        }
        return "Location not provided";
    }
);

// Execute the tool
var toolCall = new HazinaChatToolCall("call_123", "get_weather",
    BinaryData.FromString("{\"location\":\"San Francisco\"}"));

var result = await weatherTool.Execute(new List<HazinaChatMessage>(), toolCall, CancellationToken.None);
Console.WriteLine(result); // "Weather in San Francisco: Sunny, 72°F"
```

### HazinaChatToolCall

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Classes\Models\Tools\HazinaChatToolCall.cs`

Represents a tool call made by the LLM with its arguments.

**Properties**:
```csharp
public string Id { get; }
public string FunctionName { get; }
public BinaryData FunctionArguments { get; }
```

**Constructor**:
```csharp
public HazinaChatToolCall(string id, string functionName, BinaryData functionArguments)
```

**Usage Example**:
```csharp
// Create a tool call
var toolCall = new HazinaChatToolCall(
    id: "call_abc123",
    functionName: "search_database",
    functionArguments: BinaryData.FromString("{\"query\":\"users\",\"limit\":10}")
);

Console.WriteLine($"Call ID: {toolCall.Id}");
Console.WriteLine($"Function: {toolCall.FunctionName}");
Console.WriteLine($"Arguments: {toolCall.FunctionArguments.ToString()}");
```

### ChatToolParameter

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Classes\Models\Tools\ChatToolParameter.cs`

Defines a parameter for a chat tool with type information and value extraction.

**Properties**:
```csharp
public string Name { get; set; }
public string Description { get; set; }
public string Type { get; set; }
public bool Required { get; set; }
```

**Key Methods**:
```csharp
public bool TryGetValue(HazinaChatToolCall call, out string value)
public bool TryGetValue(HazinaChatToolCall call, out bool value)
```

**Usage Example**:
```csharp
var parameter = new ChatToolParameter
{
    Name = "query",
    Description = "Search query string",
    Type = "string",
    Required = true
};

// Extract value from tool call
var toolCall = new HazinaChatToolCall(
    "call_123",
    "search",
    BinaryData.FromString("{\"query\":\"hello world\",\"limit\":5}")
);

if (parameter.TryGetValue(toolCall, out string query))
{
    Console.WriteLine($"Query: {query}"); // "hello world"
}
```

### HazinaChatResponseFormat

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Classes\Models\Formats\HazinaChatResponseFormat.cs`

Specifies the desired response format from the LLM (text or JSON).

**Static Instances**:
```csharp
public static readonly HazinaChatResponseFormat Text
public static readonly HazinaChatResponseFormat Json
```

**Static Methods**:
```csharp
public static HazinaChatResponseFormat CreateTextFormat()
```

**Usage Example**:
```csharp
// Request text response
var textFormat = HazinaChatResponseFormat.Text;

// Request JSON response
var jsonFormat = HazinaChatResponseFormat.Json;

// Using factory method
var format = HazinaChatResponseFormat.CreateTextFormat();

Console.WriteLine($"Format: {format.Format}"); // "text"
```

### ChatResponse<T>

**Location**: `C:\Projects\hazina\src\Core\LLMs\Hazina.LLMs.Classes\Models\Chat\ChatResponse.cs`

Abstract base class for structured chat responses with JSON schema support.

**Abstract Properties**:
```csharp
public abstract T _example { get; }
public abstract string _signature { get; }
```

**Static Properties**:
```csharp
public static T Example => new T()._example;
public static string Signature => new T()._signature;
```

**Usage Example**:
```csharp
// Define a structured response type
public class WeatherResponse : ChatResponse<WeatherResponse>
{
    public string Location { get; set; }
    public double Temperature { get; set; }
    public string Condition { get; set; }

    public override WeatherResponse _example => new WeatherResponse
    {
        Location = "San Francisco",
        Temperature = 72.5,
        Condition = "Sunny"
    };

    public override string _signature => "WeatherResponse(Location: string, Temperature: double, Condition: string)";
}

// Use in LLM calls
var example = WeatherResponse.Example;
var signature = WeatherResponse.Signature;
```

## Usage Examples

### Building a Conversation

```csharp
var conversation = new List<HazinaChatMessage>
{
    new HazinaChatMessage(HazinaMessageRole.System, "You are a helpful coding assistant."),
    new HazinaChatMessage(HazinaMessageRole.User, "How do I sort a list in C#?"),
    new HazinaChatMessage(HazinaMessageRole.Assistant, "You can use the Sort() method or LINQ's OrderBy().")
};

foreach (var message in conversation)
{
    Console.WriteLine($"[{message.Role}] {message.Text}");
}
```

### Tool Execution with Error Handling

```csharp
var searchTool = new HazinaChatTool(
    "search_database",
    "Search the database",
    new List<ChatToolParameter>
    {
        new ChatToolParameter { Name = "query", Type = "string", Required = true },
        new ChatToolParameter { Name = "limit", Type = "number", Required = false }
    },
    async (messages, call, cancel) =>
    {
        return await HazinaChatTool.CallTool(async () =>
        {
            // Actual search logic
            return "Found 5 results";
        }, cancel);
    }
);
```

### Tracking Costs Across Multiple Calls

```csharp
var totalUsage = new TokenUsageInfo();

for (int i = 0; i < 10; i++)
{
    // Make LLM call
    var response = await llmClient.ChatAsync(messages);

    // Aggregate usage
    totalUsage = totalUsage + response.TokenUsage;
}

Console.WriteLine($"Total API calls: 10");
Console.WriteLine($"Total tokens: {totalUsage.TotalTokens}");
Console.WriteLine($"Total cost: ${totalUsage.TotalCost:F4}");
Console.WriteLine($"Average cost per call: ${totalUsage.TotalCost / 10:F4}");
```

## Dependencies

- **System.Text.Json**: JSON serialization for tool parameters and responses
- **BinaryData**: For handling tool call arguments

## Design Patterns

### Message Pattern
The `HazinaChatMessage` class supports rich conversation context including agent names, function names, and flow names, enabling multi-agent conversations and function calling scenarios.

### Response Wrapping Pattern
`LLMResponse<T>` wraps any result type with token usage information, enabling consistent cost tracking across all LLM operations.

### Tool Definition Pattern
`HazinaChatTool` provides a declarative way to define tools with parameters and executable logic, making it easy to extend LLM capabilities.

## See Also

- [Hazina.LLMs.Client](../Hazina.LLMs.Client/README.md) - LLM client abstraction using these classes
- [Hazina.LLMs.OpenAI](../../LLMs.Providers/Hazina.LLMs.OpenAI/README.md) - OpenAI implementation
- [Hazina.LLMs.Anthropic](../../LLMs.Providers/Hazina.LLMs.Anthropic/README.md) - Anthropic implementation
- [Hazina.AgentFactory](../../Agents/Hazina.AgentFactory/README.md) - Agent framework using these message types
