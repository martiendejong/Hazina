# HazinaCoder Quick Start
**Get running in 2 minutes**

## 1. Prerequisites

```bash
# .NET 9.0 SDK
dotnet --version  # Should be 9.0+

# Set API keys
export OPENAI_API_KEY="sk-..."
export ANTHROPIC_API_KEY="sk-ant-..."
```

## 2. Run

```bash
# Clone
git clone https://github.com/hazina-ai/hazina.git
cd hazina/apps/CLI/Hazina.App.HazinaCoder

# Run
dotnet run -- "Explain this code: Program.cs"
```

## 3. Interactive Mode

```bash
dotnet run
> help
> "Write unit tests for MyClass.cs"
> exit
```

## Common Commands

```bash
# Specific provider
dotnet run -- --provider anthropic "Your prompt"

# Enable vision
dotnet run -- "Analyze screenshot.png"

# Verbose output
dotnet run -- --verbose "Debug this function"

# Max tool calls
dotnet run -- --max-turns 100 "Complex refactoring"
```

## Configuration

Create `appsettings.json`:

```json
{
  "Providers": {
    "OpenAI": {
      "ApiKey": "ENV:OPENAI_API_KEY",
      "Model": "gpt-4"
    }
  }
}
```

## Next Steps

- [Full README](README.md)
- [Architecture](docs/POC1-ARCHITECTURE.md)
- [Examples](examples/)

**That's it! Start coding smarter.**
