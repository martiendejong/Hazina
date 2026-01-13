# Hazina Search API - Complete Architecture

**Version:** 1.0
**Date:** 2026-01-13
**Status:** Implementation in Progress

---

## Executive Summary

The Hazina Search API is a **fully functional RAG (Retrieval-Augmented Generation) system** that enables:

1. **RAG Store Management** - Create and manage multiple RAG stores with different configurations
2. **Multi-Format Document Processing** - Process text, chat messages, TXT, DOCX, PDF, and images
3. **Vector Search** - Semantic search using embeddings
4. **Graph-Enhanced Search** - Knowledge graph integration for contextual results
5. **Real-Time Search** - Natural language queries with LLM-powered responses

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     Hazina Search API                           │
│                    (ASP.NET Core 9.0)                          │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      API Layer                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │ RAGStore     │  │ Search       │  │ Documents    │         │
│  │ Controller   │  │ Controller   │  │ Controller   │         │
│  └──────────────┘  └──────────────┘  └──────────────┘         │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Service Layer                                │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │ RAGStore     │  │ Document     │  │ Search       │         │
│  │ Manager      │  │ Processor    │  │ Service      │         │
│  └──────────────┘  └──────────────┘  └──────────────┘         │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Core Hazina Services                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │ DocumentStore│  │EmbeddingStore│  │  RAGEngine   │         │
│  │  (SQLite)    │  │ (pgvector)   │  │   (LLM)      │         │
│  └──────────────┘  └──────────────┘  └──────────────┘         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │  GraphStore  │  │Text Extractor│  │  Chunking    │         │
│  │   (Neo4j)    │  │ (OCR/Parser) │  │   Engine     │         │
│  └──────────────┘  └──────────────┘  └──────────────┘         │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                   Storage Backends                              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │  PostgreSQL  │  │    SQLite    │  │    Neo4j     │         │
│  │  (pgvector)  │  │  (Metadata)  │  │   (Graph)    │         │
│  └──────────────┘  └──────────────┘  └──────────────┘         │
└─────────────────────────────────────────────────────────────────┘
```

---

## Core Components

### 1. RAG Store Manager

**Purpose**: Manage multiple independent RAG stores with different configurations.

**Functionality**:
- Create new RAG stores with custom configurations
- Configure embedding models per store (OpenAI, Anthropic, local)
- Set chunking strategies (fixed, semantic, sliding window)
- Configure LLM models for generation
- List and delete RAG stores

**API Endpoints**:
```
POST   /api/v1/rag-stores              - Create new RAG store
GET    /api/v1/rag-stores              - List all stores
GET    /api/v1/rag-stores/{id}         - Get store details
DELETE /api/v1/rag-stores/{id}         - Delete store
PUT    /api/v1/rag-stores/{id}/config  - Update configuration
```

**Configuration Schema**:
```json
{
  "storeId": "my-knowledge-base",
  "name": "My Knowledge Base",
  "embeddingModel": "text-embedding-3-small",
  "llmModel": "gpt-4o",
  "chunkingStrategy": "semantic",
  "chunkSize": 512,
  "chunkOverlap": 50,
  "enableGraphIndex": true,
  "enableReranking": true
}
```

### 2. Document Processor

**Purpose**: Process multiple document formats and extract searchable content.

**Supported Formats**:
- **Text**: Plain text, chat messages
- **Documents**: TXT, DOCX, PDF
- **Images**: PNG, JPG, JPEG (OCR)
- **Structured**: JSON, XML, CSV

**Processing Pipeline**:
```
Upload → Format Detection → Content Extraction → Chunking → Embedding → Storage
```

**Format Handlers**:
- **TextHandler**: Plain text and chat messages
- **DocxHandler**: Microsoft Word documents
- **PdfHandler**: PDF documents with text and OCR
- **ImageHandler**: OCR for images
- **JsonHandler**: Structured JSON data

**API Endpoints**:
```
POST /api/v1/rag-stores/{storeId}/documents/upload     - Upload document
POST /api/v1/rag-stores/{storeId}/documents/text       - Add text/chat message
POST /api/v1/rag-stores/{storeId}/documents/batch      - Bulk upload
```

### 3. Search Service

**Purpose**: Execute searches across RAG stores with multiple strategies.

**Search Strategies**:

1. **Vector Search**: Pure semantic similarity using embeddings
2. **Hybrid Search**: Vector + keyword matching
3. **Graph-Enhanced**: Vector + knowledge graph traversal
4. **RAG Query**: Full RAG with LLM generation

**Search Flow**:
```
Query → Embedding → Vector Search → (Optional) Graph Expansion
  → Reranking → LLM Generation → Response
```

**API Endpoints**:
```
POST /api/v1/rag-stores/{storeId}/search/query     - Natural language search
POST /api/v1/rag-stores/{storeId}/search/semantic  - Vector search only
POST /api/v1/rag-stores/{storeId}/search/hybrid    - Hybrid search
```

### 4. Document Store Integration

**Purpose**: Persist document metadata and content.

**Storage**:
- **SQLite**: Document metadata, chunks, relationships
- **File System**: Binary content (PDFs, images)

**Schema**:
```sql
CREATE TABLE Documents (
    Id TEXT PRIMARY KEY,
    StoreId TEXT NOT NULL,
    Title TEXT,
    ContentType TEXT,
    OriginalFilename TEXT,
    ContentHash TEXT,
    SizeBytes INTEGER,
    CreatedAt DATETIME,
    ProcessedAt DATETIME,
    Tags TEXT
);

CREATE TABLE Chunks (
    Id TEXT PRIMARY KEY,
    DocumentId TEXT,
    StoreId TEXT,
    ChunkIndex INTEGER,
    Content TEXT,
    TokenCount INTEGER,
    EmbeddingId TEXT
);
```

### 5. Embedding Store Integration

**Purpose**: Store and search vector embeddings.

**Storage**: PostgreSQL with pgvector extension

**Capabilities**:
- Store embeddings (768/1536/3072 dimensions)
- Cosine similarity search
- Approximate nearest neighbor (ANN) search
- Batch operations

**Index Types**:
- **HNSW**: Fast approximate search
- **IVFFlat**: Memory-efficient search

### 6. RAG Engine Integration

**Purpose**: Orchestrate retrieval and generation.

**Components**:
- **Retriever**: Fetch relevant chunks from vector store
- **Reranker**: Re-score results using LLM or cross-encoder
- **Generator**: Synthesize answer using LLM
- **Citation Tracker**: Track source documents

**Generation Flow**:
```
1. Embed query
2. Retrieve top-k chunks (k=20)
3. Rerank to top-n (n=5)
4. Build prompt with context
5. Generate answer with LLM
6. Extract citations
7. Return response with sources
```

---

## Data Flow Examples

### Example 1: Upload and Process PDF

```
1. Client uploads PDF via POST /api/v1/rag-stores/kb-001/documents/upload

2. API receives file
   ├─ Validates format (PDF)
   ├─ Generates document ID
   └─ Saves to file system

3. DocumentProcessor extracts text
   ├─ Uses PdfHandler
   ├─ Extracts text from all pages
   └─ Detects language

4. ChunkingEngine splits content
   ├─ Uses semantic chunking
   ├─ Creates 15 chunks
   └─ Overlaps 50 tokens

5. EmbeddingService generates vectors
   ├─ Calls OpenAI API (text-embedding-3-small)
   ├─ Gets 1536-dim vectors
   └─ Stores in pgvector

6. GraphExtractor builds entities
   ├─ Extracts named entities
   ├─ Creates relationships
   └─ Stores in Neo4j

7. Response returns
   {
     "documentId": "doc_abc123",
     "status": "processed",
     "chunks": 15,
     "entities": 8
   }
```

### Example 2: Search Query

```
1. Client queries: "What is OAuth2?"
   POST /api/v1/rag-stores/kb-001/search/query
   { "query": "What is OAuth2?", "topK": 5 }

2. SearchService embeds query
   ├─ Calls embedding model
   └─ Gets query vector

3. Retriever searches vectors
   ├─ Queries pgvector
   ├─ Returns top 20 similar chunks
   └─ Filters by similarity > 0.7

4. Reranker re-scores
   ├─ Uses LLM to judge relevance
   ├─ Re-ranks to top 5
   └─ Adds reasoning

5. Generator builds response
   ├─ Constructs prompt with chunks
   ├─ Calls LLM (GPT-4)
   ├─ Extracts answer
   └─ Identifies citations

6. Response returns
   {
     "answer": "OAuth2 is an authorization framework...",
     "confidence": 0.92,
     "sources": [
       {
         "documentId": "doc_abc123",
         "title": "OAuth2 Guide",
         "excerpt": "...",
         "relevanceScore": 0.95
       }
     ]
   }
```

### Example 3: Add Chat Message

```
1. Client sends chat message
   POST /api/v1/rag-stores/support-001/documents/text
   {
     "text": "User: How do I reset my password?\nAgent: Click 'Forgot Password'...",
     "metadata": {
       "conversationId": "conv_123",
       "timestamp": "2026-01-13T23:00:00Z"
     }
   }

2. DocumentProcessor
   ├─ Detects format: chat dialogue
   ├─ Parses turns (User, Agent)
   └─ Creates structured document

3. ChunkingEngine
   ├─ Chunks by conversation turn
   ├─ Preserves context
   └─ Creates 2 chunks

4. Storage
   ├─ Stores in DocumentStore
   ├─ Generates embeddings
   └─ Indexes for search

5. Future searches can now find this exchange
```

---

## Configuration

### Environment Variables

```bash
# Database
POSTGRES_CONNECTION_STRING=Host=localhost;Database=hazina;Username=postgres;Password=postgres
SQLITE_DB_PATH=./data/hazina.db
NEO4J_URI=bolt://localhost:7687
NEO4J_USERNAME=neo4j
NEO4J_PASSWORD=password

# Embedding Models
OPENAI_API_KEY=sk-...
ANTHROPIC_API_KEY=sk-ant-...
DEFAULT_EMBEDDING_MODEL=text-embedding-3-small

# LLM Models
DEFAULT_LLM_MODEL=gpt-4o
LLM_TEMPERATURE=0.1
LLM_MAX_TOKENS=2000

# Storage
FILE_STORAGE_PATH=./data/files
MAX_UPLOAD_SIZE_MB=100

# Performance
EMBEDDING_BATCH_SIZE=50
VECTOR_SEARCH_LIMIT=1000
CACHE_TTL_MINUTES=15
```

### RAG Store Configuration

Each RAG store can have custom settings:

```json
{
  "embeddingModel": "text-embedding-3-small",
  "embeddingDimensions": 1536,
  "llmModel": "gpt-4o",
  "llmTemperature": 0.1,
  "chunkingStrategy": "semantic",
  "chunkSize": 512,
  "chunkOverlap": 50,
  "enableGraphIndex": true,
  "enableReranking": true,
  "rerankModel": "gpt-4o-mini",
  "retrievalTopK": 20,
  "rerankTopN": 5,
  "minSimilarity": 0.7
}
```

---

## API Reference

### RAG Store Management

```http
# Create RAG Store
POST /api/v1/rag-stores
Content-Type: application/json
Authorization: Bearer <token>

{
  "storeId": "my-kb",
  "name": "My Knowledge Base",
  "config": { ... }
}

# List RAG Stores
GET /api/v1/rag-stores
Authorization: Bearer <token>

# Get Store Details
GET /api/v1/rag-stores/{storeId}
Authorization: Bearer <token>

# Delete RAG Store
DELETE /api/v1/rag-stores/{storeId}
Authorization: Bearer <token>
```

### Document Management

```http
# Upload Document (PDF, DOCX, TXT, Image)
POST /api/v1/rag-stores/{storeId}/documents/upload
Content-Type: multipart/form-data
Authorization: Bearer <token>

file: <binary>
tags: "support,documentation"
generateEmbeddings: true

# Add Text/Chat Message
POST /api/v1/rag-stores/{storeId}/documents/text
Content-Type: application/json
Authorization: Bearer <token>

{
  "text": "Content here...",
  "title": "Optional title",
  "metadata": {
    "source": "chat",
    "conversationId": "conv_123"
  }
}

# List Documents
GET /api/v1/rag-stores/{storeId}/documents
Authorization: Bearer <token>

# Delete Document
DELETE /api/v1/rag-stores/{storeId}/documents/{docId}
Authorization: Bearer <token>
```

### Search

```http
# Natural Language Query (RAG)
POST /api/v1/rag-stores/{storeId}/search/query
Content-Type: application/json
Authorization: Bearer <token>

{
  "query": "What is OAuth2?",
  "topK": 5,
  "includeGraphContext": true,
  "includeCitations": true
}

# Semantic Search (Vector Only)
POST /api/v1/rag-stores/{storeId}/search/semantic
Content-Type: application/json
Authorization: Bearer <token>

{
  "query": "OAuth authentication",
  "topK": 10,
  "minSimilarity": 0.7
}
```

---

## Deployment

### Development

```bash
# Start PostgreSQL with pgvector
docker run -d \
  --name postgres-pgvector \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=hazina \
  -p 5432:5432 \
  pgvector/pgvector:pg16

# Start API
cd apps/Web/Hazina.API.Search
dotnet run
```

### Production

```bash
# Docker Compose
docker-compose up -d

# Kubernetes
kubectl apply -f k8s/
```

---

## Performance Characteristics

### Latency Targets
- **Document Upload**: < 5 seconds (1MB file)
- **Embedding Generation**: < 500ms per chunk
- **Vector Search**: < 100ms (p95)
- **RAG Query**: < 2 seconds (p95)

### Throughput Targets
- **Concurrent Uploads**: 10/second
- **Search Queries**: 100/second
- **Embeddings**: 1000 chunks/minute

### Storage Estimates
- **Document Metadata**: ~5KB per document
- **Chunk Storage**: ~2KB per chunk
- **Vector Storage**: ~6KB per embedding (1536-dim)
- **Graph Storage**: ~10KB per 100 entities

---

## Security

### Authentication
- JWT Bearer tokens
- Role-based access (Admin, User)
- API key support for service-to-service

### Rate Limiting
- Search: 100 req/min per user
- Upload: 20 req/min per user
- Configurable per RAG store

### Data Protection
- Encryption at rest (configurable)
- TLS in transit
- Document-level access control (future)

---

## Monitoring

### Metrics
- Request latency (p50, p95, p99)
- Error rates
- Storage utilization
- Embedding API costs
- LLM API costs

### Logging
- Structured logs (Serilog)
- Request/response logging
- Error tracking
- Performance profiling

---

**Last Updated**: 2026-01-13
**Next Review**: After Phase 1 completion
