# Hazina.Tools.ContextCompression

Context compression and optimization for LLM requests. Reduces token usage while maintaining code understanding.

## Features

- **AST-based Compression**: Extract code structure without implementation details (70-85% token reduction)
- **Diff-based Compression**: Send only changed code with minimal context (80-95% token reduction)
- **Symbol Indexing**: Reference code locations instead of duplicating code (50-70% token reduction)
- **Automatic Strategy Selection**: Intelligently choose the best compression approach
- **Token Counting**: Accurate token counting using tiktoken (SharpToken)

## Installation

```bash
dotnet add package Hazina.Tools.ContextCompression
```

## Quick Start

### Basic Usage

```csharp
using Hazina.Tools.ContextCompression.Core;
using Hazina.Tools.ContextCompression.Models;

// Create compression manager
var manager = new CompressionManager();

// Compress code files
var result = await manager.CompressAsync(new CompressionRequest
{
    FilePaths = new List<string>
    {
        "Services/UserService.cs",
        "Repositories/UserRepository.cs"
    },
    Query = "How does user authentication work?",
    Strategy = CompressionStrategy.Auto
});

Console.WriteLine($"Original: {result.OriginalTokens} tokens");
Console.WriteLine($"Compressed: {result.CompressedTokens} tokens");
Console.WriteLine($"Savings: {result.TokenSavingsPercent:F1}%");
Console.WriteLine($"\nCompressed Content:\n{result.Content}");
```

### Custom Options

```csharp
var options = new CompressionOptions
{
    MaxTokens = 4000,
    IncludeLineNumbers = true,
    IncludeComments = true,
    IncludePrivateMembers = false,
    TokenizerModel = "gpt-4"
};

var manager = new CompressionManager(options);
```

### AST-based Compression

Extract code structure without implementation details:

```csharp
var result = await manager.CompressAsync(new CompressionRequest
{
    FilePaths = new List<string> { "UserService.cs" },
    Strategy = CompressionStrategy.Ast
});

// Output:
// namespace MyApp.Services;
//
// public class UserService
// {
//     public UserService(IUserRepository)
//     public Task<User> GetUserAsync(int id)
//     public Task CreateUserAsync(User user)
// }
```

### Diff-based Compression

Send only changed code:

```csharp
var result = await manager.CompressAsync(new CompressionRequest
{
    FilePaths = new List<string> { "UserService.cs" },
    Strategy = CompressionStrategy.Diff,
    RepositoryPath = @"C:\Projects\MyApp"
});

// Output shows only git diff with context lines
```

### Token Counting

```csharp
using Hazina.Tools.ContextCompression.Utilities;

var tokenCounter = new TokenCounter("gpt-4");

var text = "Your code here...";
var count = tokenCounter.Count(text);
var truncated = tokenCounter.Truncate(text, maxTokens: 500);
var chunks = tokenCounter.ChunkByTokens(text, chunkSize: 1000, overlap: 100);
```

### Symbol Extraction

```csharp
using Hazina.Tools.ContextCompression.Utilities;

var extractor = new SymbolExtractor();

// Extract symbols from file
var symbols = await extractor.ExtractSymbolsAsync("UserService.cs");

// Output:
// {
//   "class UserService": "UserService.cs:15",
//   "UserService.GetUserAsync": "UserService.cs:23",
//   "UserService.CreateUserAsync": "UserService.cs:35"
// }
```

### Get Compression Statistics

```csharp
var stats = await manager.GetCompressionStatsAsync(new List<string>
{
    "UserService.cs",
    "UserRepository.cs"
});

Console.WriteLine($"Total tokens: {stats.TotalOriginalTokens}");
foreach (var (file, fileStats) in stats.FileStats)
{
    Console.WriteLine($"{file}: {fileStats.OriginalTokens} tokens");
}
```

## Compression Strategies

| Strategy | Use Case | Token Reduction | Quality Loss |
|----------|----------|-----------------|--------------|
| `Ast` | Architecture queries | 70-85% | Low |
| `Diff` | Incremental changes | 80-95% | Very Low |
| `SymbolIndex` | Cross-file references | 50-70% | Low |
| `Auto` | Automatic selection | Varies | Low |

## Integration with LLM Pipeline

```csharp
// Before sending to LLM
var compressed = await compressionManager.CompressAsync(new CompressionRequest
{
    FilePaths = context.Files,
    Query = userQuery,
    MaxTokens = 4000 // Reserve tokens for response
});

// Send to LLM
var llmResponse = await llmClient.ChatAsync(new ChatRequest
{
    SystemPrompt = "You are a code assistant.",
    Context = compressed.Content,
    Query = userQuery
});
```

## How It Works

### AST Compression

1. Parse code into Abstract Syntax Tree using Roslyn
2. Extract class/interface definitions
3. Extract method signatures (not bodies)
4. Extract property declarations
5. Include XML documentation comments
6. Omit private members (configurable)

### Diff Compression

1. Use LibGit2Sharp to get git diff
2. Extract changed hunks with context lines
3. Include file metadata (path, change type)
4. Omit unchanged files entirely

### Strategy Auto-Selection

The `Auto` strategy selects based on:

- **Single file with git repo** → Diff
- **Architecture/structure query** → AST
- **Multiple files** → AST
- **Default** → AST

## Performance

Token reductions on real-world codebases:

- **Small service (500 LOC)**: 75% reduction with AST
- **Large controller (2000 LOC)**: 82% reduction with AST
- **Git changes (50 LOC modified)**: 92% reduction with Diff
- **Multiple files (5 files, 3000 LOC)**: 68% reduction with AST

## Roadmap

Future enhancements:

- [ ] Semantic chunking with embeddings
- [ ] Template reduction for boilerplate
- [ ] Hierarchical context building
- [ ] TypeScript/JavaScript AST support
- [ ] Python AST support
- [ ] Caching layer for symbol indexes
- [ ] Relevance scoring for semantic filtering

## License

MIT License - see [LICENSE](../../../../LICENSE) for details.
