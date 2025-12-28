# Hazina.LLMs.Helpers

Utility classes and helpers for LLM operations including embeddings, token counting, document splitting, checksum calculation, JSON parsing, and file tree generation.

## Overview

This library provides essential utilities used across the Hazina platform:

- **Embedding**: Vector operations and cosine similarity calculation
- **Token Counting**: Accurate token counting using cl100k_base encoding
- **Document Splitting**: Split large documents into token-sized chunks
- **Checksum**: SHA256 content hashing for change detection
- **JSON Parsing**: Parse partial/streaming JSON responses
- **File Trees**: Build hierarchical tree structures from file paths
- **Abstract Store**: Base class for dictionary-based storage with events

## Quick Start

### Installation

```xml
<ProjectReference Include="..\path\to\Hazina.LLMs.Helpers\Hazina.LLMs.Helpers.csproj" />
```

### Basic Usage

```csharp
// Count tokens
var counter = new TokenCounter();
int tokens = counter.CountTokens("Hello, world!");
Console.WriteLine($"Tokens: {tokens}"); // 4

// Split document
var splitter = new DocumentSplitter { TokensPerPart = 1000 };
var chunks = splitter.SplitDocument(longText);

// Calculate checksum
string hash = Checksum.CalculateChecksumFromString("content");

// Cosine similarity
var embedding1 = new Embedding(new[] { 0.1, 0.2, 0.3 });
var embedding2 = new Embedding(new[] { 0.2, 0.3, 0.4 });
double similarity = embedding1.CosineSimilarity(embedding2);
```

## Key Classes

### Embedding

**Location**: `Embedding/Embedding.cs:5`

Vector embedding class with cosine similarity calculation using MathNet.Numerics.

**Base Class**: `List<double>`

**Properties**:
```csharp
public Vector<double> Vector { get; } // MathNet vector representation
```

**Methods**:
```csharp
public double CosineSimilarity(Embedding compareTo)
```

**Usage Example**:
```csharp
// Create embeddings
var embedding1 = new Embedding(new[] { 0.5, 0.1, 0.8, 0.3 });
var embedding2 = new Embedding(new[] { 0.6, 0.2, 0.7, 0.4 });

// Calculate similarity (0.0 to 1.0, where 1.0 is identical)
double similarity = embedding1.CosineSimilarity(embedding2);
Console.WriteLine($"Similarity: {similarity:F4}"); // e.g., 0.9823

// Access as list
Console.WriteLine($"Dimensions: {embedding1.Count}"); // 4
Console.WriteLine($"First value: {embedding1[0]}"); // 0.5

// Access as vector for linear algebra operations
var mathNetVector = embedding1.Vector;
double norm = mathNetVector.L2Norm();
```

### EmbeddingInfo

**Location**: `Embedding/EmbeddingInfo.cs:1`

Container for an embedding with its key and content checksum.

**Properties**:
```csharp
public string Key { get; set; }         // Identifier (e.g., "doc1.txt chunk 0")
public string Checksum { get; set; }    // SHA256 hash of source content
public Embedding Data { get; set; }     // The embedding vector
```

**Constructor**:
```csharp
public EmbeddingInfo(string key, string checksum, Embedding data)
```

**Usage Example**:
```csharp
var embedding = new Embedding(new[] { 0.1, 0.2, 0.3 });
var checksum = Checksum.CalculateChecksumFromString("original content");

var info = new EmbeddingInfo("doc1.txt", checksum, embedding);

Console.WriteLine($"Key: {info.Key}");
Console.WriteLine($"Checksum: {info.Checksum}");
Console.WriteLine($"Dimensions: {info.Data.Count}");

// Check if content changed
var newChecksum = Checksum.CalculateChecksumFromString("new content");
if (newChecksum != info.Checksum)
{
    Console.WriteLine("Content changed - regenerate embedding");
}
```

### TokenCounter

**Location**: `TokenCounter.cs:4`

Accurate token counting using SharpToken with cl100k_base encoding (GPT-4/GPT-3.5).

**Key Methods**:

```csharp
// Count tokens in text
public int CountTokens(string text)

// Count total tokens across multiple documents
public int CountTotalTokens(IEnumerable<string> documents)

// Analyze tokens with preview
public (int TokenCount, string TokenPreview) AnalyzeTokens(string text)

// Filter documents to fit within token limit
public List<string> FilterDocumentsByTokenLimit(
    List<string> documents,
    int maxTokens)
```

**Usage Example**:
```csharp
var counter = new TokenCounter();

// Basic counting
int tokens = counter.CountTokens("Hello, world!");
Console.WriteLine($"Tokens: {tokens}"); // 4

// Count multiple documents
var docs = new[] { "Doc 1", "Doc 2", "Doc 3" };
int total = counter.CountTotalTokens(docs);

// Analyze with preview
var (count, preview) = counter.AnalyzeTokens("This is a test sentence.");
Console.WriteLine($"Tokens: {count}");
Console.WriteLine($"Preview: {preview}"); // First 10 tokens

// Filter to fit context window
var documents = new List<string>
{
    "Document 1 with some content...",
    "Document 2 with more content...",
    "Document 3 with additional content..."
};

var filtered = counter.FilterDocumentsByTokenLimit(documents, maxTokens: 1000);
Console.WriteLine($"Included {filtered.Count} documents within 1000 tokens");
```

### DocumentSplitter

**Location**: `DocumentSplitter.cs:1`

Splits large documents into chunks of approximately 1000 tokens each.

**Properties**:
```csharp
public int TokensPerPart { get; set; } = 1000;
```

**Methods**:
```csharp
// Split file by reading from disk
public List<string> SplitFile(string path, string split = "\n")

// Split document string
public List<string> SplitDocument(string content, string split = "\n")
```

**Usage Example**:
```csharp
var splitter = new DocumentSplitter { TokensPerPart = 1000 };

// Split a file
var chunks = splitter.SplitFile(@"C:\docs\large-document.txt");
Console.WriteLine($"Split into {chunks.Count} chunks");

// Split text content
string longText = File.ReadAllText("article.txt");
var parts = splitter.SplitDocument(longText, split: "\n");

foreach (var (part, index) in parts.Select((p, i) => (p, i)))
{
    Console.WriteLine($"Chunk {index}: ~{new TokenCounter().CountTokens(part)} tokens");
}

// Custom split delimiter (paragraphs)
var paragraphChunks = splitter.SplitDocument(content, split: "\n\n");

// Smaller chunks for summarization
var summarySplitter = new DocumentSplitter { TokensPerPart = 500 };
var smallChunks = summarySplitter.SplitDocument(content);
```

### Checksum

**Location**: `Checksum.cs:3`

SHA256 checksum calculation for content change detection.

**Static Methods**:
```csharp
// Calculate checksum from string
public static string CalculateChecksumFromString(string fileContents)

// Calculate checksum from file
public static string CalculateChecksum(string filePath)
```

**Usage Example**:
```csharp
// String content
string content = "Hello, world!";
string hash = Checksum.CalculateChecksumFromString(content);
Console.WriteLine($"Checksum: {hash}");
// Output: "315F5BDB76D078C43B8AC0064E4A0164612B1FCE77C869345BFC94C75894EDD3"

// File content
string fileHash = Checksum.CalculateChecksum(@"C:\docs\document.txt");

// Detect changes
var originalHash = Checksum.CalculateChecksumFromString(originalContent);
// ... content modified ...
var newHash = Checksum.CalculateChecksumFromString(modifiedContent);

if (originalHash != newHash)
{
    Console.WriteLine("Content has changed!");
}
else
{
    Console.WriteLine("Content is unchanged - use cached result");
}

// Use with embeddings
var contentChecksum = Checksum.CalculateChecksumFromString(text);
var existing = await embeddingStore.GetAsync(key);

if (existing?.Checksum == contentChecksum)
{
    Console.WriteLine("Using cached embedding");
}
else
{
    Console.WriteLine("Generating new embedding");
    var embedding = await llmClient.GenerateEmbedding(text);
    await embeddingStore.StoreAsync(key, embedding, contentChecksum);
}
```

### PartialJsonParser

**Location**: `PartialJsonParser.cs:4`

Parses partial, incomplete, or malformed JSON from streaming LLM responses.

**Methods**:
```csharp
// Parse partial JSON with auto-correction
public TResponse? Parse<TResponse>(string partialJson)

// Count braces for validation
public static (int openBraces, int closeBraces) CountBraces(string input)
```

**Usage Example**:
```csharp
var parser = new PartialJsonParser();

// Parse streaming JSON that may be incomplete
string streamingJson = "{\"name\":\"John\",\"age\":30"; // Missing closing brace

var result = parser.Parse<Person>(streamingJson);
if (result != null)
{
    Console.WriteLine($"Name: {result.Name}"); // "John"
    Console.WriteLine($"Age: {result.Age}");   // 30
}

// Parse with extra text before/after JSON
string messyJson = "Here is the data: {\"value\":42} and more text";
var data = parser.Parse<Data>(messyJson);

// Check brace balance
var (open, close) = PartialJsonParser.CountBraces("{{{}}");
Console.WriteLine($"Open: {open}, Close: {close}"); // Open: 3, Close: 2
```

### TreeMaker

**Location**: `FileTree/TreeMaker.cs:7`

Extension methods to build hierarchical tree structures from file paths.

**Extension Methods**:
```csharp
// Build tree from list of paths
public static List<TreeNode<string>> GetTree(this List<string> files)

// Build tree from dictionary with values
public static List<TreeNode<T>> GetTree<T>(this IDictionary<string, T> files)
```

**Usage Example**:
```csharp
// From file paths
var files = new List<string>
{
    "src/Core/LLMs/Client.cs",
    "src/Core/LLMs/Helper.cs",
    "src/Core/Storage/Store.cs",
    "src/Tools/Service.cs"
};

var tree = files.GetTree();

// Traverse tree
void PrintTree(TreeNode<string> node, int depth = 0)
{
    Console.WriteLine(new string(' ', depth * 2) + node.Name);
    foreach (var child in node.Children)
    {
        PrintTree(child, depth + 1);
    }
}

foreach (var root in tree)
{
    PrintTree(root);
}
```

### TreeNode<T>

**Location**: `FileTree/TreeNode.cs:3`

Generic tree node structure with parent/child relationships.

**Properties**:
```csharp
public string Name { get; set; }
public T? Value { get; set; }
public TreeNode<T>? Parent { get; set; }
public ObservableCollection<TreeNode<T>> Children { get; set; }
```

**Constructor**:
```csharp
public TreeNode(string name, T? value = default!, ObservableCollection<TreeNode<T>>? children = null)
```

### AbstractStore<T>

**Location**: `Store/AbstractStore.cs:4`

Base class for dictionary-based stores with before/after events.

**Abstract Methods**:
```csharp
public abstract Task Store(string key, T value);
public abstract bool Remove(string key);
```

**Events**:
```csharp
public event EventHandler<StoreUpdateEventArgs<T>>? BeforeUpdate;
public event EventHandler<StoreUpdateEventArgs<T>>? AfterUpdate;
public event EventHandler<StoreRemoveEventArgs>? BeforeRemove;
public event EventHandler<StoreRemoveEventArgs>? AfterRemove;
```

## Usage Examples

### Document Chunking Pipeline

```csharp
// Complete pipeline: split, checksum, embed
var splitter = new DocumentSplitter { TokensPerPart = 1000 };
var counter = new TokenCounter();

string document = File.ReadAllText("large-doc.txt");
var chunks = splitter.SplitDocument(document);

foreach (var (chunk, index) in chunks.Select((c, i) => (c, i)))
{
    // Calculate checksum for caching
    var checksum = Checksum.CalculateChecksumFromString(chunk);

    // Check if already embedded
    var key = $"doc chunk {index}";
    var existing = await embeddingStore.GetAsync(key);

    if (existing?.Checksum != checksum)
    {
        // Generate embedding
        var embedding = await llmClient.GenerateEmbedding(chunk);
        var info = new EmbeddingInfo(key, checksum, embedding);
        await embeddingStore.StoreAsync(key, embedding, checksum);

        Console.WriteLine($"Generated embedding for chunk {index}");
    }
    else
    {
        Console.WriteLine($"Using cached embedding for chunk {index}");
    }
}
```

### Semantic Search with Similarity

```csharp
// Search embeddings by similarity
var query = "machine learning algorithms";
var queryEmbedding = await llmClient.GenerateEmbedding(query);

var allEmbeddings = await embeddingStore.GetAllAsync();

var results = allEmbeddings
    .Select(info => new
    {
        Info = info,
        Similarity = queryEmbedding.CosineSimilarity(info.Data)
    })
    .OrderByDescending(r => r.Similarity)
    .Take(5);

foreach (var result in results)
{
    Console.WriteLine($"[{result.Similarity:F4}] {result.Info.Key}");
}
```

### Token Budget Management

```csharp
var counter = new TokenCounter();

// Get relevant documents
var relevantDocs = await GetRelevantDocuments(query);

// Filter to fit context window
var filtered = counter.FilterDocumentsByTokenLimit(relevantDocs, maxTokens: 3000);

Console.WriteLine($"Selected {filtered.Count}/{relevantDocs.Count} documents");
Console.WriteLine($"Total tokens: {counter.CountTotalTokens(filtered)}");
```

## Dependencies

- **SharpToken**: Token counting with cl100k_base encoding
- **MathNet.Numerics**: Vector operations for embeddings
- **System.Security.Cryptography**: SHA256 hashing
- **System.Text.Json**: JSON serialization

## See Also

- [Hazina.LLMs.Client](../Hazina.LLMs.Client/README.md) - LLM client using these helpers
- [Hazina.Store.EmbeddingStore](../../Storage/Hazina.Store.EmbeddingStore/README.md) - Uses Embedding and EmbeddingInfo
- [Hazina.Store.DocumentStore](../../Storage/Hazina.Store.DocumentStore/README.md) - Uses DocumentSplitter and TokenCounter
