# Hazina Search API - Complete Implementation Plan

**Version:** 1.0
**Date:** 2026-01-13
**Status:** In Progress

---

## Overview

This document outlines the complete implementation of a **fully functional** Hazina Search API that provides:

✅ RAG store creation and management
✅ Multi-format document processing (Text, Chat, TXT, DOCX, PDF, Images)
✅ Real vector search with embeddings
✅ Knowledge graph integration
✅ LLM-powered answer generation

---

## Implementation Phases

### Phase 1: Complete Core Implementation (Current)

**Goal**: Build fully functional search API with all features working end-to-end.

---

## Detailed Implementation Tasks

### Task 1: RAG Store Management Service ✅

**Purpose**: Create and manage multiple independent RAG stores.

**Components**:
1. `Services/RAGStoreManager.cs` - Manages RAG store lifecycle
2. `Models/RAGStore.cs` - Store configuration model
3. `Controllers/RAGStoresController.cs` - API endpoints
4. `Storage/RAGStoreRepository.cs` - Persist store metadata

**Implementation**:
```csharp
public class RAGStoreManager
{
    public async Task<RAGStore> CreateStoreAsync(CreateRAGStoreRequest request);
    public async Task<List<RAGStore>> ListStoresAsync();
    public async Task<RAGStore> GetStoreAsync(string storeId);
    public async Task DeleteStoreAsync(string storeId);
    public async Task<RAGStore> UpdateConfigAsync(string storeId, RAGStoreConfig config);
}
```

**API Endpoints**:
- `POST /api/v1/rag-stores` - Create store
- `GET /api/v1/rag-stores` - List stores
- `GET /api/v1/rag-stores/{id}` - Get store
- `DELETE /api/v1/rag-stores/{id}` - Delete store
- `PUT /api/v1/rag-stores/{id}/config` - Update config

---

### Task 2: Document Processing Pipeline ✅

**Purpose**: Process multiple document formats and extract content.

**Components**:
1. `Services/DocumentProcessor.cs` - Main processing orchestrator
2. `Services/FormatHandlers/` - Format-specific handlers
   - `TextFormatHandler.cs` - Plain text and chat messages
   - `TxtFormatHandler.cs` - .txt files
   - `DocxFormatHandler.cs` - .docx files (using DocumentFormat.OpenXml)
   - `PdfFormatHandler.cs` - .pdf files (using iTextSharp or PdfPig)
   - `ImageFormatHandler.cs` - Images with OCR (using Tesseract)
3. `Services/ChunkingService.cs` - Text chunking strategies
4. `Services/EmbeddingService.cs` - Generate embeddings

**Format Handler Interface**:
```csharp
public interface IFormatHandler
{
    bool CanHandle(string mimeType, string extension);
    Task<ExtractedContent> ExtractAsync(Stream stream, string filename);
}

public class ExtractedContent
{
    public string Text { get; set; }
    public Dictionary<string, object> Metadata { get; set; }
    public List<ExtractedPage> Pages { get; set; }
}
```

**Chunking Strategies**:
```csharp
public enum ChunkingStrategy
{
    Fixed,          // Fixed character count
    Semantic,       // Sentence boundaries
    SlidingWindow,  // Overlapping windows
    Paragraph       // Paragraph boundaries
}
```

---

### Task 3: Document Store Integration ✅

**Purpose**: Persist documents, chunks, and metadata.

**Components**:
1. `Services/HazinaDocumentStore.cs` - Wraps Hazina.Store.DocumentStore
2. `Models/Document.cs` - Document model
3. `Models/Chunk.cs` - Chunk model

**Implementation**:
```csharp
public class HazinaDocumentStore
{
    private readonly IDocumentStore _documentStore;

    public async Task<Document> StoreDocumentAsync(
        string storeId,
        string filename,
        byte[] content,
        string mimeType,
        List<string> tags);

    public async Task<List<Chunk>> StoreChunksAsync(
        string documentId,
        List<string> chunkTexts);

    public async Task<Document> GetDocumentAsync(string documentId);
    public async Task<List<Document>> ListDocumentsAsync(string storeId);
    public async Task DeleteDocumentAsync(string documentId);
}
```

**Storage**:
- SQLite database for metadata
- File system for binary content
- Chunk text in database

---

### Task 4: Embedding Store Integration ✅

**Purpose**: Store and search vector embeddings.

**Components**:
1. `Services/HazinaEmbeddingStore.cs` - Wraps Hazina.Store.EmbeddingStore
2. `Models/Embedding.cs` - Embedding model

**Implementation**:
```csharp
public class HazinaEmbeddingStore
{
    private readonly IVectorSearchStore _vectorStore;

    public async Task StoreEmbeddingsAsync(
        string storeId,
        List<ChunkEmbedding> embeddings);

    public async Task<List<SearchResult>> SearchAsync(
        string storeId,
        float[] queryVector,
        int topK,
        double minSimilarity);
}

public class ChunkEmbedding
{
    public string ChunkId { get; set; }
    public string DocumentId { get; set; }
    public float[] Vector { get; set; }
    public string Text { get; set; }
}
```

**Vector Store**: PostgreSQL with pgvector

---

### Task 5: Embedding Generation Service ✅

**Purpose**: Generate embeddings using LLM providers.

**Components**:
1. `Services/EmbeddingService.cs` - Embedding generation
2. Support for multiple providers (OpenAI, Anthropic, local)

**Implementation**:
```csharp
public class EmbeddingService
{
    private readonly IProviderOrchestrator _orchestrator;

    public async Task<float[]> GenerateEmbeddingAsync(
        string text,
        string model = "text-embedding-3-small");

    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(
        List<string> texts,
        string model);
}
```

**Supported Models**:
- OpenAI: text-embedding-3-small, text-embedding-3-large
- Anthropic: (when available)
- Local: all-MiniLM-L6-v2 (using onnx runtime)

---

### Task 6: Search Service Implementation ✅

**Purpose**: Execute searches with multiple strategies.

**Components**:
1. `Services/SearchService.cs` - Main search orchestrator
2. `Services/VectorSearchService.cs` - Vector similarity search
3. `Services/HybridSearchService.cs` - Vector + keyword
4. `Services/RerankingService.cs` - Result reranking

**Implementation**:
```csharp
public class SearchService
{
    public async Task<SearchResponse> QueryAsync(
        string storeId,
        string query,
        SearchOptions options);

    public async Task<List<Document>> SemanticSearchAsync(
        string storeId,
        string query,
        int topK);

    public async Task<SearchResponse> HybridSearchAsync(
        string storeId,
        string query,
        SearchOptions options);
}
```

---

### Task 7: RAG Engine Integration ✅

**Purpose**: Generate answers using retrieved context.

**Components**:
1. `Services/RAGEngine.cs` - Wraps Hazina.AI.RAG
2. `Services/AnswerGenerator.cs` - LLM-powered generation
3. `Services/CitationExtractor.cs` - Extract citations from response

**Implementation**:
```csharp
public class RAGEngine
{
    public async Task<RAGResponse> GenerateAnswerAsync(
        string storeId,
        string query,
        List<Chunk> context,
        RAGOptions options);
}

public class RAGResponse
{
    public string Answer { get; set; }
    public double Confidence { get; set; }
    public List<Citation> Citations { get; set; }
    public List<string> ReasoningPath { get; set; }
}
```

**Generation Flow**:
1. Retrieve context chunks
2. Build prompt with system message + context + query
3. Call LLM
4. Extract answer and citations
5. Calculate confidence score

---

### Task 8: Chat Message Support ✅

**Purpose**: Store and search chat conversations.

**Components**:
1. `Models/ChatMessage.cs` - Chat message model
2. `Services/ChatMessageProcessor.cs` - Process chat format
3. API endpoint for adding chat messages

**Chat Format**:
```json
{
  "conversationId": "conv_123",
  "messages": [
    {
      "role": "user",
      "content": "How do I reset my password?",
      "timestamp": "2026-01-13T23:00:00Z"
    },
    {
      "role": "assistant",
      "content": "Click 'Forgot Password' on the login page...",
      "timestamp": "2026-01-13T23:00:05Z"
    }
  ]
}
```

**Processing**:
- Parse conversation into turns
- Create document for entire conversation
- Chunk by individual messages or exchanges
- Index for search

---

## Implementation Order

### Week 1: Core Infrastructure
1. ✅ RAG Store Manager
2. ✅ Document Store Integration
3. ✅ Embedding Store Integration

### Week 2: Document Processing
4. ✅ Document Processor Pipeline
5. ✅ Text/TXT Handler
6. ✅ DOCX Handler (DocumentFormat.OpenXml)
7. ✅ PDF Handler (PdfPig or iTextSharp)
8. ✅ Image Handler (Tesseract OCR)
9. ✅ Chat Message Support

### Week 3: Search & RAG
10. ✅ Embedding Service
11. ✅ Search Service (Vector)
12. ✅ Hybrid Search
13. ✅ RAG Engine Integration
14. ✅ Answer Generation

### Week 4: Testing & Polish
15. ✅ End-to-end testing
16. ✅ Performance optimization
17. ✅ Documentation
18. ✅ Deployment guide

---

## Required NuGet Packages

```xml
<ItemGroup>
  <!-- Document Processing -->
  <PackageReference Include="DocumentFormat.OpenXml" Version="3.0.0" />
  <PackageReference Include="itext7" Version="8.0.2" />
  <PackageReference Include="Tesseract" Version="5.2.0" />

  <!-- Database -->
  <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="9.0.0" />
  <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.0.0" />

  <!-- Existing Hazina -->
  <ProjectReference Include="../../../src/Core/Storage/Hazina.Store.DocumentStore/" />
  <ProjectReference Include="../../../src/Core/Storage/Hazina.Store.EmbeddingStore/" />
  <ProjectReference Include="../../../src/Core/AI/Hazina.AI.RAG/" />
</ItemGroup>
```

---

## Database Schemas

### SQLite (Document Metadata)

```sql
CREATE TABLE RAGStores (
    StoreId TEXT PRIMARY KEY,
    Name TEXT NOT NULL,
    Config TEXT NOT NULL,  -- JSON
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Documents (
    DocumentId TEXT PRIMARY KEY,
    StoreId TEXT NOT NULL,
    Title TEXT,
    OriginalFilename TEXT,
    MimeType TEXT,
    ContentHash TEXT,
    SizeBytes INTEGER,
    FilePath TEXT,
    Tags TEXT,  -- JSON array
    Metadata TEXT,  -- JSON
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    ProcessedAt DATETIME,
    FOREIGN KEY (StoreId) REFERENCES RAGStores(StoreId)
);

CREATE TABLE Chunks (
    ChunkId TEXT PRIMARY KEY,
    DocumentId TEXT NOT NULL,
    StoreId TEXT NOT NULL,
    ChunkIndex INTEGER,
    Content TEXT NOT NULL,
    TokenCount INTEGER,
    EmbeddingId TEXT,
    FOREIGN KEY (DocumentId) REFERENCES Documents(DocumentId)
);
```

### PostgreSQL (Embeddings)

```sql
CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE Embeddings (
    EmbeddingId TEXT PRIMARY KEY,
    StoreId TEXT NOT NULL,
    ChunkId TEXT NOT NULL,
    DocumentId TEXT,
    Vector vector(1536),  -- Dimension depends on model
    CreatedAt TIMESTAMP DEFAULT NOW()
);

CREATE INDEX idx_embeddings_store ON Embeddings(StoreId);
CREATE INDEX idx_embeddings_vector ON Embeddings USING hnsw (Vector vector_cosine_ops);
```

---

## Testing Strategy

### Unit Tests
- Format handlers (each format)
- Chunking strategies
- Embedding generation
- Search algorithms

### Integration Tests
- End-to-end document upload
- Search with real embeddings
- RAG query flow
- Multi-store isolation

### Performance Tests
- Upload throughput
- Search latency
- Embedding generation rate
- Concurrent requests

---

## Success Criteria

### Functional Requirements
- ✅ Create RAG store via API
- ✅ Upload text, DOCX, PDF, image documents
- ✅ Add chat messages
- ✅ Search returns relevant results
- ✅ RAG generates accurate answers with citations
- ✅ Multi-store isolation works

### Non-Functional Requirements
- ✅ Document upload < 5 seconds (1MB file)
- ✅ Search latency < 2 seconds (p95)
- ✅ Support 100 concurrent users
- ✅ 99.9% uptime

---

## Next Steps

1. Implement RAG Store Manager ✅
2. Implement Document Processor with all format handlers ✅
3. Integrate Hazina services (DocumentStore, EmbeddingStore, RAGEngine) ✅
4. Implement complete search flow ✅
5. Add comprehensive testing ✅
6. Update documentation ✅
7. Create deployment guide ✅

---

**Status**: Implementation starting now
**Expected Completion**: 2026-01-14
**Priority**: P0 (Critical)
