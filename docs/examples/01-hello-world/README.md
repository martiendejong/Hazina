# Hello World - Your First Hazina Application

**Learn the basics: Setup, configuration, and your first AI query**

## What You'll Learn

- How to set up a Hazina AI client
- How to send a simple query
- How to handle responses
- Environment-based configuration

## Prerequisites

- .NET 8.0 or higher
- OpenAI API key

## Setup

1. **Get an OpenAI API key**: Sign up at [platform.openai.com](https://platform.openai.com/)

2. **Set your API key**:
   ```bash
   # Windows
   set OPENAI_API_KEY=sk-your-key-here

   # Linux/Mac
   export OPENAI_API_KEY=sk-your-key-here
   ```

3. **Install dependencies**:
   ```bash
   dotnet restore
   ```

## Running the Example

```bash
dotnet run
```

Expected output:
```
=== Hazina Hello World ===

Question: What is 2+2?
Answer: 2+2 equals 4.

Tokens used: 15 (input: 10, output: 5)
Cost: $0.0003
```

## Code Walkthrough

### 1. Setup AI Client

```csharp
using Hazina.AI.FluentAPI.Configuration;

var ai = QuickSetup.SetupOpenAI(
    Environment.GetEnvironmentVariable("OPENAI_API_KEY")!
);
```

**What's happening:**
- `QuickSetup.SetupOpenAI()` creates a pre-configured OpenAI client
- Environment variable provides the API key (never hardcode secrets!)
- Returns an `ILLMClient` that abstracts the provider

### 2. Create a Message

```csharp
var messages = new List<HazinaChatMessage>
{
    new()
    {
        Role = HazinaMessageRole.User,
        Text = "What is 2+2?"
    }
};
```

**What's happening:**
- Messages are the fundamental unit of conversation
- Each message has a `Role` (System, User, or Assistant)
- `Text` contains the message content

### 3. Get Response

```csharp
var response = await ai.GetResponse(messages);
Console.WriteLine($"Answer: {response.Content.Text}");
```

**What's happening:**
- `GetResponse()` sends messages to the LLM
- Returns `LLMResponse<string>` with the AI's answer
- `Content.Text` contains the generated text

### 4. Check Token Usage

```csharp
Console.WriteLine($"Tokens used: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalTokens * 0.00002:F4}");
```

**What's happening:**
- Token usage is automatically tracked
- You can estimate costs based on provider pricing
- Useful for budget monitoring

## Key Concepts

### Provider Abstraction

Hazina abstracts LLM providers. This code works identically with OpenAI, Anthropic, or local models:

```csharp
// OpenAI
var ai = QuickSetup.SetupOpenAI(openAiKey);

// Anthropic
var ai = QuickSetup.SetupAnthropic(anthropicKey);

// Your code stays the same!
var response = await ai.GetResponse(messages);
```

### Message Roles

- **System**: Instructions for the AI (behavior, persona, rules)
- **User**: Messages from the user
- **Assistant**: Responses from the AI

Example with system message:

```csharp
var messages = new List<HazinaChatMessage>
{
    new()
    {
        Role = HazinaMessageRole.System,
        Text = "You are a helpful math tutor."
    },
    new()
    {
        Role = HazinaMessageRole.User,
        Text = "What is 2+2?"
    }
};
```

### Environment Configuration

**Why use environment variables?**

1. **Security**: Never commit API keys to source control
2. **Flexibility**: Different keys for dev/staging/production
3. **Standard practice**: Industry best practice for secrets management

**Setting environment variables:**

**Windows (PowerShell)**:
```powershell
$env:OPENAI_API_KEY = "sk-your-key-here"
```

**Windows (Command Prompt)**:
```cmd
set OPENAI_API_KEY=sk-your-key-here
```

**Linux/Mac**:
```bash
export OPENAI_API_KEY=sk-your-key-here
```

**Persistent (add to your shell profile)**:
```bash
# Linux/Mac: Add to ~/.bashrc or ~/.zshrc
echo 'export OPENAI_API_KEY=sk-your-key-here' >> ~/.bashrc
source ~/.bashrc
```

## Extending This Example

### Add Error Handling

```csharp
try
{
    var response = await ai.GetResponse(messages);
    Console.WriteLine(response.Content.Text);
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

### Add System Message

```csharp
var messages = new List<HazinaChatMessage>
{
    new()
    {
        Role = HazinaMessageRole.System,
        Text = "You are a friendly assistant who answers in haiku."
    },
    new()
    {
        Role = HazinaMessageRole.User,
        Text = "What is 2+2?"
    }
};
```

### Try Different Questions

```csharp
var questions = new[]
{
    "What is the capital of France?",
    "Explain quantum computing in one sentence.",
    "Write a haiku about coding."
};

foreach (var question in questions)
{
    var messages = new List<HazinaChatMessage>
    {
        new() { Role = HazinaMessageRole.User, Text = question }
    };

    var response = await ai.GetResponse(messages);
    Console.WriteLine($"\nQ: {question}");
    Console.WriteLine($"A: {response.Content.Text}");
}
```

## Troubleshooting

### "API key not found" error

**Problem**: Environment variable not set correctly.

**Solution**:
```bash
# Check if variable is set
echo $OPENAI_API_KEY  # Linux/Mac
echo %OPENAI_API_KEY%  # Windows

# Set it if missing
export OPENAI_API_KEY=your-key  # Linux/Mac
set OPENAI_API_KEY=your-key     # Windows
```

### "Package not found" error

**Problem**: NuGet packages not restored.

**Solution**:
```bash
dotnet restore
```

### "Rate limit exceeded" error

**Problem**: Too many requests to OpenAI API.

**Solution**: Wait a few seconds between requests, or use a higher-tier API key.

## Next Steps

- [Interactive Chat](../02-interactive-chat/) - Build a conversation loop
- [Multi-Provider Setup](../03-multi-provider/) - Add provider failover
- [Basic RAG](../04-basic-rag/) - Add document context to your queries

## Full Code

See [Program.cs](Program.cs) for the complete, runnable code.

---

**Congratulations! You've built your first Hazina application.**

This simple pattern scales to production. The same `QuickSetup` and `GetResponse` code works in large-scale applications - you just add capabilities like RAG, agents, and monitoring on top.
