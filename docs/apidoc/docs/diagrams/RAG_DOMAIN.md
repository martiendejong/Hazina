# RAG Domain Architecture

## Overview Diagram

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                            DOCUMENT SOURCES                                  │
│  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐  ┌─────────┐           │
│  │  PDF    │  │  TXT    │  │  MD     │  │  DOCX   │  │  Images │           │
│  └────┬────┘  └────┬────┘  └────┬────┘  └────┬────┘  └────┬────┘           │
└───────┼────────────┼────────────┼────────────┼────────────┼─────────────────┘
        │            │            │            │            │
        └────────────┴────────────┴────────────┴────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           DOCUMENT PROCESSING                                │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    BinaryDocumentProcessor                           │   │
│  │  • Text extraction from PDFs, DOCX                                   │   │
│  │  • Vision API summaries for images                                   │   │
│  │  • MIME type detection                                               │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                  │                                          │
│                                  ▼                                          │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                         TextChunker                                  │   │
│  │  Strategies: FixedSize │ Sentence │ Paragraph │ Semantic             │   │
│  │  Default: ~1000 tokens per chunk with 100 token overlap              │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                          EMBEDDING GENERATION                                │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │                    LLMEmbeddingGenerator                             │   │
│  │  • Uses ILLMClient.GenerateEmbedding()                              │   │
│  │  • Default: 1536 dimensions (text-embedding-3-small)                │   │
│  │  • Batch generation support                                          │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                  │                                          │
│                                  ▼                                          │
│              ┌───────────────────────────────────────┐                     │
│              │        float[1536] vector             │                     │
│              │  [0.023, -0.156, 0.891, ...]         │                     │
│              └───────────────────────────────────────┘                     │
└─────────────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                            STORAGE LAYER                                     │
│                                                                              │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐             │
│  │  DocumentStore  │  │  EmbeddingStore │  │  MetadataStore  │             │
│  │  • Chunks       │  │  • Vectors      │  │  • Properties   │             │
│  │  • Relationships│  │  • Checksums    │  │  • Tags         │             │
│  └────────┬────────┘  └────────┬────────┘  └────────┬────────┘             │
│           │                    │                    │                       │
│           └────────────────────┴────────────────────┘                       │
│                                │                                            │
│           ┌────────────────────┼────────────────────┐                       │
│           ▼                    ▼                    ▼                       │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐             │
│  │    SQLite       │  │   PostgreSQL    │  │    Supabase     │             │
│  │  (FTS5 search)  │  │  (pgvector)     │  │    (cloud)      │             │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Indexing Flow

```
Document Input
     │
     ▼
┌─────────────────────┐
│ Store() / StoreFrom │
│ File()              │
└─────────────────────┘
     │
     ├──► Is Binary? ───► YES ──► BinaryDocumentProcessor
     │         │                          │
     │        NO                   Extract text / AI summary
     │         │                          │
     │         ▼                          ▼
     │    Raw Text ◄──────────────────────┘
     │         │
     ▼         ▼
┌─────────────────────┐
│ Create Metadata     │ ─── DocumentMetadata { Id, Path, MIME, Size, Created }
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Metadata Chunk      │ ─── "{docId}.metadata" - Always searchable
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Split into Chunks   │ ─── TextChunker with ~1000 tokens
└─────────────────────┘
     │
     ├──► Chunk 0: "{docId} chunk 0"
     ├──► Chunk 1: "{docId} chunk 1"
     ├──► Chunk 2: "{docId} chunk 2"
     │    ...
     ▼
┌─────────────────────┐
│ Generate Embeddings │ ─── LLMEmbeddingGenerator for each chunk
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Store Everything    │
│ • Metadata          │
│ • Chunks            │
│ • Embeddings        │
│ • Relationships     │
└─────────────────────┘
```

## Search Flow

```
User Query: "How does authentication work?"
     │
     ▼
┌─────────────────────┐
│  RAGEngine.Search   │
│  Async()            │
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Generate Query      │ ─── Same embedding model
│ Embedding           │
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Vector Similarity   │ ─── Cosine similarity vs all stored embeddings
│ Search              │
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Rank by Similarity  │ ─── Sort descending
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Reranker (optional) │ ─── LLM-based reranking for accuracy
└─────────────────────┘
     │
     ▼
┌─────────────────────┐
│ Return Top K        │ ─── Default: top 10 results
│ Results             │
└─────────────────────┘
     │
     ▼
┌─────────────────────────────────────────────────────────────┐
│  Results:                                                    │
│  ┌─────────────────────────────────────────────────────┐   │
│  │ [0.92] auth-guide.md chunk 2   "OAuth flow..."      │   │
│  │ [0.87] auth-guide.md.metadata  "Document about..."  │   │
│  │ [0.84] diagram.png.metadata    "Auth flowchart..."  │   │
│  │ [0.81] login.cs chunk 0        "public class Login" │   │
│  └─────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## RAG Query Flow (Ask with Context)

```
User Question: "How does user authentication work in this system?"
     │
     ▼
┌──────────────────────────┐
│  RAGEngine.AskWithContext│
│  Async()                 │
└──────────────────────────┘
     │
     ├──► 1. Search for relevant documents
     │         │
     │         ▼
     │    ┌────────────────────┐
     │    │ Top K chunks found │
     │    └────────────────────┘
     │
     ├──► 2. Build context from chunks
     │         │
     │         ▼
     │    ┌────────────────────────────────────────┐
     │    │ Context:                               │
     │    │ [Doc: auth-guide.md]                   │
     │    │ OAuth 2.0 is used for authentication...│
     │    │                                        │
     │    │ [Doc: login.cs]                        │
     │    │ public class LoginService...           │
     │    └────────────────────────────────────────┘
     │
     ├──► 3. Construct augmented prompt
     │         │
     │         ▼
     │    ┌────────────────────────────────────────┐
     │    │ System: Answer based on this context:  │
     │    │ {context}                              │
     │    │                                        │
     │    │ User: How does authentication work?    │
     │    └────────────────────────────────────────┘
     │
     └──► 4. Send to LLM (via ProviderOrchestrator)
              │
              ▼
         ┌────────────────────────────────────────┐
         │ Response with citations:               │
         │ "Based on the documentation, your      │
         │  system uses OAuth 2.0... [auth-guide] │
         │  The LoginService class handles..."    │
         └────────────────────────────────────────┘
```

## Key Files

| Component | File |
|-----------|------|
| RAG Engine | `Hazina.AI.RAG/Core/RAGEngine.cs` |
| RAG Config | `Hazina.AI.RAG/Core/RAGConfig.cs` |
| Text Chunker | `Hazina.AI.RAG/Embeddings/TextChunker.cs` |
| Reranker | `Hazina.AI.RAG/Retrieval/Reranker.cs` |
| Document Store | `Hazina.Store.DocumentStore/Core/DocumentStore.cs` |
| Embedding Store | `Hazina.Store.EmbeddingStore/Core/EmbeddingService.cs` |
| Binary Processor | `Hazina.Store.DocumentStore/Processors/BinaryDocumentProcessor.cs` |
