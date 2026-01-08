# RAG Domain Entry Point

## Start Here
- **Main Engine**: `Core/RAGEngine.cs` - All RAG operations
- **Configuration**: `Core/RAGConfig.cs`
- **Chunking**: `Embeddings/TextChunker.cs`

## Key Flows

### 1. Index Documents
```csharp
var rag = new RAGEngine(orchestrator, embeddingStore, metadataStore);
await rag.IndexDocumentsAsync(documents);
```

### 2. Search
```csharp
var results = await rag.SearchAsync("query", topK: 10);
```

### 3. Ask with Context (RAG Query)
```csharp
var answer = await rag.AskWithContextAsync("What does X do?");
// Automatically retrieves relevant documents and includes as context
```

## Projects in This Domain
| Project | Purpose | Criticality |
|---------|---------|-------------|
| `Hazina.AI.RAG` | RAG engine | CRITICAL |

## Sub-Components
- `Embeddings/` - Text chunking, embedding generation
- `Retrieval/` - Search, reranking
- `Core/` - Main engine, configuration

## Dependencies
- Requires: `Hazina.AI.Providers` (for LLM calls)
- Requires: Embedding store (SQLite, PostgreSQL, or Supabase)
- Requires: Metadata store (for document tracking)
