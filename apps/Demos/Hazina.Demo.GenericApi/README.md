# Hazina.Demo.GenericApi

A demo API showing how to use `Hazina.API.Generic` to build REST APIs with minimal code.

## Quick Start

```bash
cd apps/Demos/Hazina.Demo.GenericApi
dotnet run
```

Open https://localhost:5001 for Swagger UI.

## What This Demo Shows

### 1. Minimal Controller (TagsController)

```csharp
public class TagsController : GenericEntityController<Tag>
{
    public TagsController(IRepository<Tag> repository) : base(repository) { }
}
```

**One line of code** gives you 10 endpoints:
- `GET /api/tags` - List with pagination
- `GET /api/tags/{id}` - Get by ID
- `POST /api/tags` - Create
- `PUT /api/tags/{id}` - Update
- `PATCH /api/tags/{id}` - Partial update
- `DELETE /api/tags/{id}` - Delete
- `POST /api/tags/bulk` - Bulk create
- `DELETE /api/tags/bulk` - Bulk delete
- `GET /api/tags/count` - Count
- `GET /api/tags/exists/{id}` - Check existence

### 2. Extended Controller (DocumentsController)

```csharp
public class DocumentsController : GenericEntityController<Document>
{
    // Get all generic endpoints PLUS:

    [HttpPost("search")]
    public async Task<ActionResult<SemanticSearchResult>> SemanticSearch(...) { }

    [HttpPost("{id}/embed")]
    public async Task<ActionResult<Document>> GenerateEmbedding(...) { }
}
```

Inherits all generic functionality, adds custom semantic search.

### 3. Entity Base Classes

```csharp
// Simple entity with ID + timestamps
public class Note : EntityBase { ... }

// Entity with soft delete + RAG embeddings
public class Document : EmbeddableEntityBase
{
    public override string GetSearchableText() { ... }
}
```

## Try It Out

### 1. List all documents
```bash
curl https://localhost:5001/api/documents
```

### 2. Create a document
```bash
curl -X POST https://localhost:5001/api/documents \
  -H "Content-Type: application/json" \
  -d '{"title":"My Doc","content":"Some content","tags":"test"}'
```

### 3. Generate embeddings for all documents
```bash
curl -X POST https://localhost:5001/api/documents/embed-all
```

### 4. Semantic search
```bash
curl -X POST https://localhost:5001/api/documents/search \
  -H "Content-Type: application/json" \
  -d '{"query":"how to build APIs","topK":5}'
```

## For Production

Replace `MockEmbeddingGenerator` with `OpenAIEmbeddingGenerator`:

```csharp
// In Program.cs
builder.Services.AddSingleton<IEmbeddingGenerator>(sp =>
    new OpenAIEmbeddingGenerator(
        apiKey: Environment.GetEnvironmentVariable("OPENAI_API_KEY")!,
        model: "text-embedding-ada-002"
    ));
```

## Files

| File | Purpose |
|------|---------|
| `Entities/Document.cs` | Document, Note, Tag entity definitions |
| `Controllers/DocumentsController.cs` | CRUD + semantic search |
| `Controllers/NotesController.cs` | Basic CRUD with custom endpoints |
| `Controllers/TagsController.cs` | Minimal example (1 line!) |
| `Services/SemanticSearchService.cs` | Vector similarity search |
| `Data/DemoDbContext.cs` | SQLite database with seed data |

## Database

Uses SQLite (`demo.db`) - no external database required.

Delete `demo.db` to reset seed data.
