# Hazina Repository

Hazina is a collection of agents, tooling, services, demos, and apps for LLM-powered workflows.

## Quick Start
- Restore dependencies: `dotnet restore Hazina.sln`
- Build everything: `dotnet build Hazina.sln`
- Run tests: `dotnet test Hazina.sln` (or per-project test csproj)

## Using the DocumentStore

The DocumentStore is a core component for storing, embedding, and querying documents with semantic search capabilities.

### 1. Instantiating a DocumentStore

```csharp
using Hazina.Store.EmbeddingStore;

// Configure your LLM client (example with OpenAI)
var openAIConfig = new OpenAIConfig("your-api-key");
var llmClient = new OpenAIClientWrapper(openAIConfig);

// Create the required stores
var embeddingStore = new EmbeddingFileStore(@"C:\myproject\data.embed", llmClient);
var textStore = new TextFileStore(@"C:\myproject\documents");
var chunkStore = new ChunkFileStore(@"C:\myproject\data.chunks");
var metadataStore = new DocumentMetadataFileStore(@"C:\myproject\data.metadata");

// Instantiate the DocumentStore
var documentStore = new DocumentStore(
    embeddingStore,
    textStore,
    chunkStore,
    metadataStore,
    llmClient
);
```

### 2. Storing and Embedding Documents

#### Store text content
```csharp
// Store a simple text document
await documentStore.Store(
    "document-id",
    "This is my document content...",
    metadata: new Dictionary<string, string> { { "author", "John Doe" } },
    split: true  // Automatically split into chunks for large documents
);
```

#### Store binary files (PDF, images, etc.)
```csharp
// Store binary content with automatic text extraction
byte[] pdfBytes = File.ReadAllBytes("report.pdf");
await documentStore.Store(
    "report-2024",
    pdfBytes,
    "application/pdf",
    metadata: new Dictionary<string, string> { { "year", "2024" } }
);
```

#### Store from file path
```csharp
// Store directly from a file (auto-detects type and extracts text)
await documentStore.StoreFromFile(
    "my-doc",
    @"C:\documents\myfile.docx",
    metadata: new Dictionary<string, string> { { "department", "Engineering" } }
);
```

#### Update embeddings
```csharp
// Generate embeddings for a specific document
await documentStore.Embed("document-id");

// Or update all embeddings in the store
await documentStore.UpdateEmbeddings();
```

### 3. Running Relevancy Queries

#### Get relevant document IDs
```csharp
// Find documents relevant to a query (returns document IDs)
var relevantDocs = await documentStore.RelevantItems(
    "What are the best practices for API design?"
);

foreach (var docId in relevantDocs)
{
    var content = await documentStore.Get(docId);
    Console.WriteLine($"Document: {docId}");
    Console.WriteLine(content);
}
```

#### Get detailed embeddings with similarity scores
```csharp
// Get embeddings with similarity scores and metadata
var embeddings = await documentStore.Embeddings(
    "machine learning algorithms"
);

foreach (var embedding in embeddings)
{
    Console.WriteLine($"Similarity: {embedding.Similarity:F3}");
    Console.WriteLine($"Document: {embedding.Document.Key}");

    // Get the actual text content
    var text = await embedding.GetText(embedding.Document.Key);
    Console.WriteLine($"Content: {text}");
    Console.WriteLine($"Parent Document: {embedding.ParentDocumentKey}");
}
```

### 4. Retrieving Documents

#### Get document content
```csharp
// Retrieve full document content
string content = await documentStore.Get("document-id");
```

#### Get specific chunk
```csharp
// Retrieve a specific chunk by its key
string chunkContent = await documentStore.GetChunk("document-id chunk 0");
```

#### Get document with all chunks and metadata
```csharp
// Get complete document information
var docWithChunks = await documentStore.GetDocumentWithChunks("document-id");

Console.WriteLine($"Document: {docWithChunks.Key}");
Console.WriteLine($"Content: {docWithChunks.Content}");
Console.WriteLine($"Metadata: {docWithChunks.Metadata.MimeType}");
Console.WriteLine($"Chunks: {string.Join(", ", docWithChunks.ChunkKeys)}");
```

#### Get metadata only
```csharp
// Retrieve document metadata
var metadata = await documentStore.GetMetadata("document-id");
Console.WriteLine($"Created: {metadata.Created}");
Console.WriteLine($"Size: {metadata.Size} bytes");
Console.WriteLine($"Type: {metadata.MimeType}");
```

### 5. Updating Documents

#### Move/rename a document
```csharp
// Move or rename a document
await documentStore.Move("old-document-id", "new-document-id", split: true);
```

#### Remove a document
```csharp
// Remove document and all its chunks/embeddings
await documentStore.Remove("document-id");
```

### 6. Listing and Navigation

#### List all documents
```csharp
// List all documents (non-recursive, root level only)
var allDocs = await documentStore.List();

// List all documents recursively
var allDocsRecursive = await documentStore.List(recursive: true);

// List documents in a specific folder
var folderDocs = await documentStore.List("projects/2024", recursive: false);
```

#### Get hierarchical tree
```csharp
// Get a tree structure of all documents
var tree = await documentStore.Tree();
foreach (var node in tree)
{
    Console.WriteLine($"- {node.Value}");
}
```

### Example: Complete Workflow

```csharp
// 1. Setup
var config = new OpenAIConfig("sk-...");
var llm = new OpenAIClientWrapper(config);
var store = new DocumentStore(
    new EmbeddingFileStore(@"C:\data\docs.embed", llm),
    new TextFileStore(@"C:\data\docs"),
    new ChunkFileStore(@"C:\data\docs.chunks"),
    new DocumentMetadataFileStore(@"C:\data\docs.metadata"),
    llm
);

// 2. Add documents
await store.StoreFromFile("readme", @"C:\projects\README.md");
await store.StoreFromFile("guide", @"C:\projects\GUIDE.pdf");
await store.Store("notes", "Important notes about the project...");

// 3. Update embeddings
await store.UpdateEmbeddings();

// 4. Query
var results = await store.Embeddings("How do I configure the application?");
foreach (var result in results.Take(3))
{
    Console.WriteLine($"\nRelevance: {result.Similarity:P1}");
    Console.WriteLine($"From: {result.ParentDocumentKey ?? result.Document.Key}");

    var text = await result.GetText(result.Document.Key);
    Console.WriteLine($"Content: {text.Substring(0, Math.Min(200, text.Length))}...");
}

// 5. List all stored documents
var docs = await store.List(recursive: true);
Console.WriteLine($"\nTotal documents: {docs.Count}");
```

## Using AgentFactory and AgentManager

The AgentFactory and AgentManager provide high-level orchestration for creating and managing AI agents with tool capabilities.

### 1. Setting Up AgentManager

```csharp
// Initialize AgentManager with configuration files
var agentManager = new AgentManager(
    storesJsonPath: "stores.hazina",
    agentsJsonPath: "agents.hazina",
    flowsJsonPath: "flows.hazina",
    openAIApiKey: "sk-...",
    logFilePath: @"C:\logs\hazina.log",
    googleProjectId: "" // Optional, for BigQuery
);

// Load all stores and agents from config
await agentManager.LoadStoresAndAgents();
```

### 2. Making Requests to Agents

#### Simple request (returns string only)
```csharp
var result = await agentManager.SendMessage(
    "Explain how authentication works",
    cancellationToken,
    agentName: "my_agent" // Optional, uses first agent if not specified
);

Console.WriteLine(result); // The response text
```

#### Request with full token tracking
```csharp
// Get the agent directly for access to token usage
var agent = agentManager.GetAgent("my_agent");

var response = await agent.Generator.GetResponse<IsReadyResult>(
    "What files are in the project?",
    cancellationToken,
    agentManager.History,  // Conversation history
    addRelevantDocuments: true,
    addFilesList: true,
    agent.Tools,
    images: null
);

// Access the response
Console.WriteLine($"Response: {response.Result.Message}");

// Access token usage
Console.WriteLine($"Input tokens: {response.TokenUsage.InputTokens}");
Console.WriteLine($"Output tokens: {response.TokenUsage.OutputTokens}");
Console.WriteLine($"Total tokens: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Input cost: ${response.TokenUsage.InputCost:F4}");
Console.WriteLine($"Output cost: ${response.TokenUsage.OutputCost:F4}");
Console.WriteLine($"Total cost: ${response.TokenUsage.TotalCost:F4}");
Console.WriteLine($"Model: {response.TokenUsage.ModelName}");
```

#### Streaming response with token tracking
```csharp
var agent = agentManager.GetAgent("my_agent");

var response = await agent.Generator.StreamResponse(
    "Write a detailed explanation of the codebase",
    cancellationToken,
    onChunkReceived: chunk => Console.Write(chunk), // Handle each streaming chunk
    history: agentManager.History,
    addRelevantDocuments: true,
    addFilesList: true,
    toolsContext: agent.Tools,
    images: null
);

Console.WriteLine($"\n\nStreaming complete. Tokens used: {response.TokenUsage.TotalTokens}");
Console.WriteLine($"Total cost: ${response.TokenUsage.TotalCost:F4}");
```

### 3. Tracking Token Usage Across Multiple Requests

```csharp
var agent = agentManager.GetAgent("my_agent");
var totalUsage = new TokenUsageInfo();

var queries = new[] {
    "List all TypeScript files",
    "Find authentication code",
    "Explain the database schema"
};

foreach (var query in queries)
{
    var response = await agent.Generator.GetResponse(
        query,
        cancellationToken,
        agentManager.History
    );

    // Aggregate token usage using the + operator
    totalUsage += response.TokenUsage;

    Console.WriteLine($"\nQuery: {query}");
    Console.WriteLine($"Tokens: {response.TokenUsage.TotalTokens}");
    Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
}

Console.WriteLine($"\n--- Summary ---");
Console.WriteLine($"Total input tokens: {totalUsage.InputTokens}");
Console.WriteLine($"Total output tokens: {totalUsage.OutputTokens}");
Console.WriteLine($"Total tokens: {totalUsage.TotalTokens}");
Console.WriteLine($"Total cost: ${totalUsage.TotalCost:F4}");
```

### 4. Working with Flows

```csharp
// Execute a flow (sequence of agents)
var flowResult = await agentManager.SendMessage_Flow(
    "Process this document end-to-end",
    cancellationToken,
    flowName: "document_processing_flow"
);

Console.WriteLine($"Flow result: {flowResult}");
```

### 5. Creating Agents Programmatically

```csharp
// Using QuickAgentCreator (via AgentManager)
var creator = agentManager._quickAgentCreator;

// Create a custom agent without registering it
var customAgent = await creator.AgentFactory.CreateUnregisteredAgent(
    name: "temp_agent",
    systemPrompt: "You are a helpful code reviewer",
    stores: new[] { (myDocumentStore, write: false) },
    function: new[] { "git", "dotnet" },
    agents: Array.Empty<string>(),
    flows: Array.Empty<string>(),
    isCoder: false
);

var response = await customAgent.Generator.GetResponse(
    "Review the authentication code",
    cancellationToken
);

Console.WriteLine($"Review: {response.Result}");
Console.WriteLine($"Cost: ${response.TokenUsage.TotalCost:F4}");
```

### 6. Using Typed Responses

```csharp
// Define a custom response type
public class AnalysisResult : ChatResponse<AnalysisResult>
{
    public string Summary { get; set; }
    public List<string> Issues { get; set; }
    public int Severity { get; set; }
}

// Request with typed response
var agent = agentManager.GetAgent("analyzer");
var response = await agent.Generator.GetResponse<AnalysisResult>(
    "Analyze the security of this code",
    cancellationToken,
    agentManager.History
);

// Access typed result
Console.WriteLine($"Summary: {response.Result?.Summary}");
Console.WriteLine($"Issues found: {response.Result?.Issues.Count}");
Console.WriteLine($"Severity: {response.Result?.Severity}");

// Token usage still available
Console.WriteLine($"Analysis cost: ${response.TokenUsage.TotalCost:F4}");
```

### 7. Configuration Files

#### stores.hazina
```
Name: my_code_store
Description: All project source code
Path: C:\Projects\MyApp
FileFilters: *.cs,*.ts,*.js,*.json
SubDirectory:
ExcludePattern: bin,obj,node_modules
```

#### agents.hazina
```
Name: code_assistant
Description: Helps with code questions and modifications
Prompt: You are an expert code assistant. Help the user understand and modify the codebase.
Stores: my_code_store|False
Functions: git,dotnet
CallsAgents:
CallsFlows:
ExplicitModify: False
```

### Token Usage Reference

All LLM responses return `LLMResponse<T>` which contains:
- **Result**: The actual response (string, typed object, etc.)
- **TokenUsage**: `TokenUsageInfo` object with:
  - `InputTokens`: Tokens in the prompt
  - `OutputTokens`: Tokens in the response
  - `TotalTokens`: Sum of input + output
  - `InputCost`: Cost of input tokens (in USD)
  - `OutputCost`: Cost of output tokens (in USD)
  - `TotalCost`: Total cost (in USD)
  - `ModelName`: The model used (e.g., "gpt-4", "claude-3-opus")

### Example: Complete Workflow with Cost Tracking

```csharp
// Setup
var agentManager = new AgentManager(
    "stores.hazina", "agents.hazina", "flows.hazina",
    "sk-...", @"C:\logs\hazina.log"
);
await agentManager.LoadStoresAndAgents();

var agent = agentManager.GetAgent("code_assistant");
var sessionUsage = new TokenUsageInfo();

// Interactive session
var questions = new[] {
    "What's the project structure?",
    "Find all API endpoints",
    "Show me the authentication flow"
};

foreach (var question in questions)
{
    Console.WriteLine($"\nQ: {question}");

    var response = await agent.Generator.GetResponse(
        question,
        CancellationToken.None,
        agentManager.History,
        addRelevantDocuments: true,
        addFilesList: true,
        agent.Tools
    );

    Console.WriteLine($"A: {response.Result}");
    Console.WriteLine($"   Tokens: {response.TokenUsage.TotalTokens} | Cost: ${response.TokenUsage.TotalCost:F4}");

    sessionUsage += response.TokenUsage;
}

// Session summary
Console.WriteLine("\n=== Session Summary ===");
Console.WriteLine($"Questions asked: {questions.Length}");
Console.WriteLine($"Total tokens: {sessionUsage.TotalTokens:N0}");
Console.WriteLine($"Total cost: ${sessionUsage.TotalCost:F4}");
Console.WriteLine($"Model: {sessionUsage.ModelName}");
```

## Document Processing Pipeline: Complete Technical Overview

This section explains in detail how documents are processed, chunked, embedded, and searched in Hazina.

### Overview of the Pipeline

When you store a document, Hazina performs the following steps:
1. **Document Storage** - Store the original content
2. **Text Extraction** - Extract/generate text from binary files (images, PDFs)
3. **Metadata Creation** - Create searchable metadata
4. **Document Splitting** - Split large documents into token-sized chunks
5. **Embedding Generation** - Generate vector embeddings for each chunk
6. **Chunk Indexing** - Index chunks for parent document tracking
7. **Relevancy Search** - Use cosine similarity to find relevant chunks

### 1. Document Splitting (Chunking)

**Purpose**: Large documents must be split into smaller chunks that fit within LLM context windows.

**Implementation** (DocumentSplitter.cs:77):
```csharp
public class DocumentSplitter
{
    public int TokensPerPart { get; set; } = 1000;  // Default chunk size

    public List<string> SplitDocument(string content, string split = "\n")
    {
        var tokenCounter = new TokenCounter();  // Uses SharpToken with cl100k_base encoding
        var remainingLines = content.Split(split).ToList();
        var result = new List<string>();

        while (remainingLines.Count > 0)
        {
            var partLines = new List<string>();
            bool partComplete = false;

            // Keep adding lines until we reach TokensPerPart (1000 tokens)
            while (!partComplete && remainingLines.Count > 0)
            {
                partLines.Add(remainingLines[0]);
                remainingLines.RemoveAt(0);

                var partTokens = tokenCounter.CountTokens(string.Join(split, partLines));
                partComplete = partTokens >= TokensPerPart;
            }

            result.Add(string.Join(split, partLines));
        }

        return result;
    }
}
```

**How it works**:
- Documents are split by newlines (`\n`) by default
- Each chunk contains approximately **1000 tokens**
- Token counting uses the **cl100k_base** encoding (GPT-4/GPT-3.5 tokenizer)
- Lines are added until the token limit is reached
- This preserves line boundaries for better semantic coherence

**Example**:
```
Input: 3500-token document with 200 lines
Output:
  - Chunk 0: Lines 1-57 (≈1000 tokens)
  - Chunk 1: Lines 58-114 (≈1000 tokens)
  - Chunk 2: Lines 115-171 (≈1000 tokens)
  - Chunk 3: Lines 172-200 (≈500 tokens)
```

### 2. Binary Document Processing

**Purpose**: Extract searchable text from images, PDFs, and other binary files.

**Implementation** (BinaryDocumentProcessor.cs:8):

#### Image Processing
```csharp
private async Task<string> GenerateImageSummary(byte[] content, string mimeType)
{
    // Uses LLM vision capabilities to analyze the image
    var messages = new List<HazinaChatMessage>
    {
        new HazinaChatMessage(
            HazinaMessageRole.User,
            "Describe this image in detail. Include any text visible in the image, " +
            "the main subjects, colors, composition, and any other relevant details."
        )
    };

    var imageData = new ImageData
    {
        MimeType = mimeType,
        BinaryData = new BinaryData(content)
    };

    var response = await _llmClient.GetResponse(
        messages,
        HazinaChatResponseFormat.Text,
        null,
        new List<ImageData> { imageData },
        CancellationToken.None
    );

    return response.Result;  // Returns detailed description
}
```

**What happens**:
- **Images** (PNG, JPEG, GIF, etc.) → Sent to LLM vision API
- LLM generates detailed description including:
  - Visible text in the image
  - Main subjects and objects
  - Colors and composition
  - Any other relevant visual details
- The description becomes searchable text

**Example**:
```
Input: screenshot.png (chart showing Q4 sales data)
Output: "This image shows a bar chart titled 'Q4 Sales Performance'.
         The chart displays sales data across four quarters with blue bars.
         The y-axis shows values from $0 to $500K. Text at the top reads
         'Annual Revenue Target: $1.5M'. The chart indicates Q4 reached
         approximately $450K in sales."
```

#### PDF Processing
```csharp
private async Task<string> GeneratePdfSummary(byte[] content)
{
    var summary = new StringBuilder();
    summary.AppendLine("PDF Document");
    summary.AppendLine($"Size: {content.Length} bytes");

    // Detect PDF version from header
    if (content.Length > 8)
    {
        var header = Encoding.ASCII.GetString(content, 0, Math.Min(8, content.Length));
        if (header.StartsWith("%PDF-"))
        {
            summary.AppendLine($"PDF Version: {header}");
        }
    }

    // Note: Full text extraction requires additional libraries
    summary.AppendLine("Note: Full text extraction not yet implemented.");

    return summary.ToString();
}
```

**Current limitations**:
- PDFs currently generate basic metadata (size, version)
- Future enhancement: Use PDF libraries for full text extraction

#### Other Binary Files
```csharp
private string GenerateBasicBinaryInfo(byte[] content, string mimeType)
{
    return $@"Binary file: {mimeType}
Size: {content.Length} bytes
File signature: {BitConverter.ToString(content, 0, Math.Min(4, content.Length))}";
}
```

### 3. Metadata Generation and Embedding

**Purpose**: Make document metadata searchable alongside content.

**Implementation** (DocumentStore.cs:77-123):

When storing a document, metadata is created and embedded as a **separate searchable chunk**:

```csharp
public async Task<bool> Store(string name, string content,
    Dictionary<string, string>? metadata = null, bool split = true)
{
    // 1. Create metadata
    var docMetadata = new DocumentMetadata
    {
        Id = name,
        OriginalPath = "",
        MimeType = "text/plain",
        Size = content.Length,
        Created = DateTime.UtcNow,
        CustomMetadata = metadata ?? new Dictionary<string, string>(),
        IsBinary = false,
        Summary = null
    };
    await MetadataStore.Store(name, docMetadata);

    var chunkKeys = new List<string>();

    // 2. Store metadata as a searchable chunk
    var metadataKey = $"{name}.metadata";
    var metadataChunk = docMetadata.ToChunkText();  // Converts to searchable text
    await EmbeddingStore.StoreEmbedding(metadataKey, metadataChunk);
    await TextStore.Store(metadataKey, metadataChunk);
    chunkKeys.Add(metadataKey);

    // 3. Split and store content chunks
    var chunks = split ? DocumentSplitter.SplitDocument(content) : [...];

    if (chunks.Count == 1)
    {
        // Small document - no splitting needed
        await EmbeddingStore.StoreEmbedding(name, content);
        await TextStore.Store(name, content);
        chunkKeys.Add(name);
    }
    else
    {
        // Large document - store each chunk
        for (var i = 0; i < chunks.Count; ++i)
        {
            var chunkKey = $"{name} chunk {i}";
            await EmbeddingStore.StoreEmbedding(chunkKey, chunks[i]);
            await TextStore.Store(chunkKey, chunks[i]);
            chunkKeys.Add(chunkKey);
        }
    }

    // 4. Store chunk index for parent tracking
    await ChunkStore.Store(name, chunkKeys);
    return true;
}
```

**Metadata chunk example**:
```
Document ID: projects/auth/login.cs
Original Path: C:\code\auth\login.cs
MIME Type: text/x-csharp
Size: 3842 bytes
Created: 2024-01-15 14:32:10 UTC
Is Binary: False
Custom Metadata:
  Author: John Doe
  Project: Authentication
  LastModified: 2024-01-15
```

This metadata chunk gets its own embedding, making it searchable!

### 4. For Binary Documents (Images, PDFs)

**Complete flow** (DocumentStore.cs:126-180):

```csharp
public async Task<bool> Store(string name, byte[] content, string mimeType,
    Dictionary<string, string>? metadata = null)
{
    // 1. Extract text and generate summary
    var textContent = await BinaryProcessor.ExtractText(content, mimeType);
    var summary = BinaryProcessor.IsBinary(mimeType)
        ? await BinaryProcessor.GenerateSummary(content, mimeType)
        : null;

    // 2. Create metadata with summary
    var docMetadata = new DocumentMetadata
    {
        Id = name,
        MimeType = mimeType,
        Size = content.Length,
        IsBinary = BinaryProcessor.IsBinary(mimeType),
        Summary = summary,  // AI-generated summary for images
        CustomMetadata = metadata ?? new Dictionary<string, string>()
    };

    // 3. Store metadata as searchable chunk
    var metadataKey = $"{name}.metadata";
    var metadataChunk = docMetadata.ToChunkText();  // Includes summary!
    await EmbeddingStore.StoreEmbedding(metadataKey, metadataChunk);

    // 4. Combine summary + extracted text for content
    var contentToStore = string.IsNullOrEmpty(summary)
        ? textContent
        : $"{summary}\n\nExtracted content:\n{textContent}";

    // 5. Split and embed the combined content
    var chunks = DocumentSplitter.SplitDocument(contentToStore);
    for (var i = 0; i < chunks.Count; ++i)
    {
        var chunkKey = $"{name} chunk {i}";
        await EmbeddingStore.StoreEmbedding(chunkKey, chunks[i]);
        await TextStore.Store(chunkKey, chunks[i]);
        chunkKeys.Add(chunkKey);
    }

    return true;
}
```

**Example: Storing an image**:
```
Input: chart.png (600KB bar chart)

Step 1 - Vision API generates summary:
  "Bar chart showing Q4 sales. Values range $0-$500K.
   Q4 reached $450K. Title: 'Annual Revenue Target: $1.5M'"

Step 2 - Metadata chunk created:
  Document ID: reports/chart.png
  MIME Type: image/png
  Size: 614400 bytes
  Summary: [the vision description above]

Step 3 - Both metadata and summary are embedded and searchable

Step 4 - User searches "Q4 sales performance"
  → Finds chunk: "reports/chart.png.metadata"
  → Returns the chart because metadata mentions "Q4 sales"
```

### 5. Embedding Generation

**Purpose**: Convert text into high-dimensional vectors for semantic search.

**How embeddings are created**:
```csharp
public async Task<bool> StoreEmbedding(string key, string text)
{
    // 1. Generate embedding vector from LLM
    var embedding = await _llmClient.GenerateEmbedding(text);

    // 2. Create embedding info with checksum
    var checksum = CalculateChecksum(text);
    var embeddingInfo = new EmbeddingInfo(key, checksum, embedding);

    // 3. Store to file/database
    await AddEmbedding(embeddingInfo);

    return true;
}
```

**Embedding structure** (Embedding.cs:5):
```csharp
public class Embedding : List<double>
{
    // Typical size: 1536 dimensions for OpenAI ada-002
    // Example: [0.0234, -0.0123, 0.0456, ..., 0.0189]

    public double CosineSimilarity(Embedding compareTo)
    {
        // Calculate similarity: dot product / (magnitude1 * magnitude2)
        return Vector.DotProduct(compareTo.Vector) /
               (Vector.L2Norm() * compareTo.Vector.L2Norm());
    }
}

public class EmbeddingInfo
{
    public string Key { get; set; }         // e.g., "doc.txt chunk 0"
    public string Checksum { get; set; }    // Detect content changes
    public Embedding Data { get; set; }     // 1536-dim vector
}
```

**Storage format** (EmbeddingFileStore.cs:47-91):
Embeddings are stored as JSON:
```json
[
  {
    "Key": "login.cs chunk 0",
    "Checksum": "a3f2c1d4...",
    "Data": [0.0234, -0.0123, 0.0456, ..., 0.0189]
  },
  {
    "Key": "login.cs chunk 1",
    "Checksum": "b5e8f2a1...",
    "Data": [0.0145, -0.0267, 0.0389, ..., 0.0234]
  },
  {
    "Key": "login.cs.metadata",
    "Checksum": "c7d9a3f2...",
    "Data": [0.0189, -0.0145, 0.0278, ..., 0.0167]
  }
]
```

### 6. Chunk Tracking

**Purpose**: Map chunks back to their parent documents.

**ChunkStore maintains the index** (ChunkFileStore):
```json
{
  "login.cs": [
    "login.cs.metadata",
    "login.cs chunk 0",
    "login.cs chunk 1",
    "login.cs chunk 2"
  ],
  "chart.png": [
    "chart.png.metadata",
    "chart.png chunk 0"
  ]
}
```

This allows:
- Finding all chunks for a document
- Finding the parent document for any chunk
- Deleting all chunks when removing a document

### 7. Relevancy Search (The Complete Picture)

**Implementation** (DocumentStore.cs:311-367):

```csharp
public async Task<List<RelevantEmbedding>> Embeddings(string query)
{
    // 1. Truncate query to fit token limits
    var cutOffQuery = EmbeddingMatcher.CutOffQuery(query);  // Max 8000 tokens

    // 2. Generate embedding for the search query
    var queryEmbedding = await _llmClient.GenerateEmbedding(cutOffQuery);

    // 3. Calculate cosine similarity with ALL stored embeddings
    var similarities = EmbeddingMatcher.GetEmbeddingsWithSimilarity(
        queryEmbedding,
        EmbeddingStore.Embeddings
    );
    // Returns: [(0.89, "login.cs chunk 0"), (0.82, "chart.png.metadata"), ...]

    // 4. Convert to RelevantEmbedding objects
    var results = new List<RelevantEmbedding>();
    foreach (var (similarity, embeddingInfo) in similarities)
    {
        var chunkKey = embeddingInfo.Key;

        // Find parent document for this chunk
        var parentKey = await ChunkStore.GetParentDocument(chunkKey);

        results.Add(new RelevantEmbedding
        {
            Similarity = similarity,              // 0.82
            StoreName = Name,
            Document = embeddingInfo,             // The chunk
            ParentDocumentKey = parentKey,        // "chart.png"
            GetText = async (key) => await TextStore.Get(key)
        });
    }

    return results.OrderByDescending(r => r.Similarity).ToList();
}
```

**Cosine Similarity Calculation** (Embedding.cs:16-24):
```csharp
public double CosineSimilarity(Embedding compareTo)
{
    // Formula: similarity = (A · B) / (||A|| × ||B||)
    // Where:
    //   A · B = dot product (sum of element-wise multiplication)
    //   ||A|| = L2 norm (magnitude/length of vector)

    var dotProduct = Vector.DotProduct(compareTo.Vector);
    var magnitudeProduct = Vector.L2Norm() * compareTo.Vector.L2Norm();

    return dotProduct / magnitudeProduct;
    // Returns value between -1 and 1
    //   1.0 = identical semantic meaning
    //   0.8+ = very similar
    //   0.5-0.8 = somewhat related
    //   <0.5 = not very related
}
```

**Token-limited result selection** (EmbeddingMatcher.cs:31-63):
```csharp
public async Task<List<string>> TakeTop(List<RelevantEmbedding> total, int maxTokens = 8000)
{
    var selectedDocuments = new List<string>();
    int currentTokenCount = 0;

    // Results are already sorted by similarity (highest first)
    foreach (var document in total)
    {
        // Get the actual text for this chunk
        var text = await document.GetText(document.Document.Key);

        // Format for LLM context
        var documentView = $@"Store: {document.StoreName}
File path: {document.Document.Key}
File content:
{text}";

        // Count tokens
        int documentTokenCount = TokenCounter.CountTokens(documentView);

        // Add if it fits within limit
        if (currentTokenCount + documentTokenCount <= maxTokens)
        {
            selectedDocuments.Add(documentView);
            currentTokenCount += documentTokenCount;
        }
        else
        {
            break;  // Stop when we exceed token limit
        }
    }

    return selectedDocuments;
}
```

### Complete Example: End-to-End Flow

**Scenario**: Store and search a codebase with an image

```csharp
// 1. STORE TEXT DOCUMENT
await documentStore.Store(
    "auth/login.cs",
    "public class LoginController { /* 3000 tokens of code */ }",
    metadata: new Dictionary<string, string> {
        { "Author", "John" },
        { "Project", "Auth" }
    }
);

// Behind the scenes:
// ✓ Creates metadata chunk: "auth/login.cs.metadata"
// ✓ Splits into: "auth/login.cs chunk 0", "chunk 1", "chunk 2"
// ✓ Generates embeddings for all 4 chunks (1 metadata + 3 content)
// ✓ Stores in ChunkStore: auth/login.cs → [metadata, chunk 0, 1, 2]

// 2. STORE IMAGE
await documentStore.Store(
    "diagrams/architecture.png",
    imageBytes,
    "image/png"
);

// Behind the scenes:
// ✓ Sends to vision API → "Diagram showing microservices architecture
//                         with API Gateway, Auth Service, User Service..."
// ✓ Creates metadata with summary
// ✓ Embeds metadata chunk: "diagrams/architecture.png.metadata"
// ✓ Embeds content chunk: "diagrams/architecture.png chunk 0"

// 3. UPDATE EMBEDDINGS
await documentStore.UpdateEmbeddings();

// 4. SEARCH
var results = await documentStore.Embeddings("user authentication flow");

// Behind the scenes:
// ✓ Generates embedding for "user authentication flow"
// ✓ Calculates cosine similarity with ALL embeddings:
//     - auth/login.cs.metadata: 0.87 (high - mentions "Auth")
//     - auth/login.cs chunk 0: 0.92 (very high - login code)
//     - auth/login.cs chunk 1: 0.78
//     - diagrams/architecture.png.metadata: 0.84 (mentions "Auth Service")
//     - diagrams/architecture.png chunk 0: 0.81
// ✓ Sorts by similarity (descending)
// ✓ Selects top results within 8000 token limit

// Results returned:
// [
//   { Similarity: 0.92, Document: "auth/login.cs chunk 0", Parent: "auth/login.cs" },
//   { Similarity: 0.87, Document: "auth/login.cs.metadata", Parent: "auth/login.cs" },
//   { Similarity: 0.84, Document: "diagrams/architecture.png.metadata", Parent: "diagrams/architecture.png" },
//   { Similarity: 0.81, Document: "diagrams/architecture.png chunk 0", Parent: "diagrams/architecture.png" },
//   ...
// ]

// 5. USE IN QUERY
foreach (var result in results.Take(3))
{
    var text = await result.GetText(result.Document.Key);
    Console.WriteLine($"[{result.Similarity:F2}] {result.ParentDocumentKey}");
    Console.WriteLine(text.Substring(0, 200));
}
```

**Output**:
```
[0.92] auth/login.cs
public class LoginController {
    public async Task<IActionResult> Login(string username, string password) {
        var user = await _authService.AuthenticateAsync(username, password);
        ...

[0.87] auth/login.cs
Document ID: auth/login.cs
Original Path: C:\code\auth\login.cs
MIME Type: text/x-csharp
Custom Metadata:
  Author: John
  Project: Auth

[0.84] diagrams/architecture.png
Document ID: diagrams/architecture.png
MIME Type: image/png
Summary: Diagram showing microservices architecture with API Gateway, Auth Service, User Service connected via REST APIs...
```

### Key Takeaways

1. **Documents are split** into ~1000 token chunks for embedding
2. **Metadata is embedded separately** as its own searchable chunk
3. **Binary files (images)** use vision APIs to generate searchable descriptions
4. **Each chunk gets a vector embedding** (typically 1536 dimensions)
5. **Search uses cosine similarity** to find semantically similar chunks
6. **Results are token-limited** to fit in LLM context (default 8000 tokens)
7. **Parent tracking** allows finding the source document for any chunk
8. **Everything is searchable**: code, metadata, image descriptions, PDFs

This architecture enables semantic search across mixed content types while maintaining the relationship between chunks and their source documents.

## Solution Organization

The solution is organized into logical categories to improve navigation and maintainability.

**Architecture Diagram**: See [solution-architecture.drawio](solution-architecture.drawio) for a visual representation of how the categories relate to each other.

### 1. Agents
Core agent orchestration, factory patterns, and content generation engines.
- Hazina.AgentFactory
- Hazina.DynamicAPI
- Hazina.Generator

### 2. LLM Core
Foundation libraries for LLM interactions, including client abstractions, data contracts, and tool definitions.
- Hazina.LLMClientTools
- Hazina.LLMs.Classes
- Hazina.LLMs.Client
- Hazina.LLMs.Helpers
- Hazina.LLMs.Tools

### 3. LLM Providers
Concrete integrations with AI service providers and adapters.
- Hazina.LLMs.Anthropic
- Hazina.LLMs.Gemini
- Hazina.LLMs.HuggingFace
- Hazina.LLMs.Mistral
- Hazina.LLMs.OpenAI
- Hazina.LLMs.SemanticKernel

### 4. Storage & UI
Data persistence layers and shared user interface components.
- Hazina.Store.DocumentStore
- Hazina.Store.EmbeddingStore
- Hazina.ChatShared

### 5. Tools Foundation
Core utilities, extension methods, base types, and fundamental tool infrastructure.
- Hazina.Tools.AI.Agents
- Hazina.Tools.Core
- Hazina.Tools.Data
- Hazina.Tools.Extensions
- Hazina.Tools.Models
- Hazina.Tools.TextExtraction

### 6. Tools Common
Shared infrastructure and models used across multiple tool categories.
- Hazina.Tools.Common.Infrastructure.AspNetCore
- Hazina.Tools.Common.Models

### 7. Services - Core
Essential services for chat, embeddings, prompts, and storage operations.
- Hazina.Tools.Services
- Hazina.Tools.Services.Chat
- Hazina.Tools.Services.Embeddings
- Hazina.Tools.Services.Prompts
- Hazina.Tools.Services.Store

### 8. Services - Data
Data processing, ingestion, and file operation services.
- Hazina.Tools.Services.BigQuery
- Hazina.Tools.Services.ContentRetrieval
- Hazina.Tools.Services.DataGathering
- Hazina.Tools.Services.FileOps
- Hazina.Tools.Services.Intake

### 9. Services - Integration
External system connectors and third-party integrations.
- Hazina.Tools.Services.Social
- Hazina.Tools.Services.Web
- Hazina.Tools.Services.WordPress

### 10. Desktop Apps
Windows desktop applications for building, viewing, and managing Hazina workflows.
- Hazina.App.AppBuilder
- Hazina.App.EmbeddingsViewer
- Hazina.App.ExplorerIntegration
- Hazina.App.Windows

### 11. CLI & Web Apps
Command-line tools and web applications.
- Hazina.App.ClaudeCode
- Hazina.App.HtmlMockupGenerator

### 12. Demos
Example applications demonstrating Hazina capabilities.
- Hazina.Demo.Crosslink
- Hazina.Demo.FolderToPostgres
- Hazina.Demo.Llama
- Hazina.Demo.PDFMaker
- Hazina.Demo.Postgres

## Notes
- `.local` projects are developer-specific variants not referenced in the main solution file.
- XML docs are emitted on build under `bin/Debug/net8.0/*.xml` for library projects.