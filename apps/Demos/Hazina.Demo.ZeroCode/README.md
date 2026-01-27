# Hazina Zero-Code API Demo

Build a complete REST API without writing any C# entity classes or controllers!

## Two Approaches

### Option A: Dynamic API (Zero Code)

**No C# code needed at all!** Define entities in `entities.yaml` and you get a full REST API:

```yaml
# entities.yaml
- name: Document
  fields:
    - name: title
      type: String
      required: true
    - name: content
      type: Text
```

```csharp
// Program.cs - This is ALL you need:
builder.Services.AddHazinaDynamicApi("entities.yaml", "app.db");
```

**Pros:**
- Zero C# code
- Instant changes - edit YAML, restart
- Perfect for prototypes and simple APIs

**Cons:**
- No compile-time type safety
- No IntelliSense for entities


### Option B: Code Generator

Generate C# classes from YAML for compile-time safety:

```csharp
// Generate once:
await HazinaCodeGen.GenerateProjectAsync(
    yamlPath: "entities.yaml",
    outputPath: "./Generated",
    projectName: "MyApi"
);
```

This creates:
- `Entities/Document.cs` - C# entity class
- `Controllers/DocumentsController.cs` - API controller
- `Program.cs` - Configured startup
- `AppDbContext.cs` - EF Core context

**Pros:**
- Full compile-time type safety
- IntelliSense and IDE support
- Custom logic can be added

**Cons:**
- Requires regeneration after YAML changes


## Running This Demo

```bash
# From this directory:
dotnet run

# Open browser to:
# https://localhost:5001
```

## API Endpoints

For each entity in `entities.yaml`:

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/{entity}` | List all (paginated) |
| GET | `/api/{entity}/{id}` | Get by ID |
| POST | `/api/{entity}` | Create |
| PUT | `/api/{entity}/{id}` | Update |
| DELETE | `/api/{entity}/{id}` | Soft delete |
| GET | `/api/{entity}/count` | Count |
| GET | `/api/{entity}/search?q=term` | Search |
| GET | `/api/{entity}/schema` | Get field definitions |
| GET | `/api/_entities` | List all entity types |
| POST | `/api/chat` | **RAG-powered AI chat** |

## YAML Schema

```yaml
- name: EntityName          # Required: singular name
  pluralName: EntityNames   # Optional: for routes
  description: "..."        # Optional: for docs

  fields:
    - name: fieldName       # Required
      type: String          # See types below
      required: true        # Optional
      maxLength: 200        # Optional
      searchable: true      # Optional: include in search
      description: "..."    # Optional

  features:
    crud: true              # Default: true
    search: true            # Enable search endpoint
    embedding: true         # RAG embedding support
    softDelete: true        # Default: true
    pagination: true        # Default: true
```

### Field Types

| Type | Description |
|------|-------------|
| `String` | Short text (max 8000 chars) |
| `Text` | Long text (unlimited) |
| `Int` | 32-bit integer |
| `Long` | 64-bit integer |
| `Decimal` | Precise decimal |
| `Double` | Floating point |
| `Bool` | True/false |
| `DateTime` | Date and time |
| `DateOnly` | Date only |
| `TimeOnly` | Time only |
| `Guid` | UUID |
| `Json` | JSON object/array |
| `Enum` | String enum |
| `Reference` | Foreign key (Guid) |
| `List` | Collection (JSON) |
| `Binary` | Byte array |

## RAG-Powered Chat Endpoint

**NEW!** Ask questions about your documents using AI-powered Retrieval-Augmented Generation.

### How It Works

1. **Create Documents** - Add content via `POST /api/Document`
2. **Semantic Search** - System finds relevant documents using embeddings
3. **AI Response** - Ollama (local LLM) generates answers using document context

### Configuration

The API uses your local Ollama instance (no API costs, full privacy):

```json
{
  "Ollama": {
    "Endpoint": "http://85.215.217.154:5555",
    "Model": "phi3:mini",
    "EmbeddingModel": "nomic-embed-text",
    "Password": "Th1s1sSp4rt4!"
  }
}
```

### Simple Chat Request

```bash
curl -X POST https://localhost:49238/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "message": "How does the Zero-Code API work?",
    "topK": 3
  }'
```

**Response:**
```json
{
  "answer": "Based on the documents, the Zero-Code API allows you to define entities in YAML without writing any C# code. It automatically generates CRUD endpoints, search functionality, and supports embeddings for RAG...",
  "documentsUsed": [
    {
      "id": "550e8400-e29b-41d4-a716-446655440000",
      "title": "Zero-Code API Features",
      "relevance": 0.0
    }
  ],
  "tokenUsage": {
    "promptTokens": 250,
    "completionTokens": 45,
    "totalTokens": 295
  }
}
```

### Conversation Mode (Multi-Turn)

```bash
curl -X POST https://localhost:49238/api/chat \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {
        "role": "user",
        "content": "What is Hazina?"
      },
      {
        "role": "assistant",
        "content": "Hazina is a framework for building enterprise applications with built-in RAG capabilities."
      },
      {
        "role": "user",
        "content": "Tell me more about the RAG features"
      }
    ],
    "topK": 3
  }'
```

### Test Script

Run the included test script to see RAG in action:

```powershell
.\test-chat.ps1
```

This will:
1. Create sample documents
2. Send a chat question
3. Display AI-generated response with sources

### Benefits of Ollama (Local LLM)

- ✅ **No API costs** - Run everything locally
- ✅ **Privacy** - Your data never leaves your server
- ✅ **Fast** - Low latency with local inference (~2-3s first request, <1s after)
- ✅ **Offline** - Works without internet connection

For detailed chat documentation, see **[CHAT_GUIDE.md](./CHAT_GUIDE.md)**.

## Discovering API Details

### Get Complete API Guide
**NEW!** Get schemas, examples, and endpoints for all entities in one call:
```bash
GET https://localhost:5001/api/_guide
```

This returns:
- All entity definitions
- Required vs optional fields
- Example request bodies for each entity
- All available endpoints

### Get Schema for Specific Entity
Before creating entities, check the schema to see required fields:
```bash
GET https://localhost:5001/api/Document/schema
```

**Response shows:**
- Field names and types
- Which fields are required
- Max lengths and validation rules
- Searchable fields
- Enabled features

## Example Requests

### Document Entity

**Check schema first:**
```bash
curl https://localhost:5001/api/Document/schema
```

**Create a Document:**
```bash
curl -X POST https://localhost:5001/api/Document \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Getting Started with Hazina",
    "content": "This is a comprehensive guide to using the Hazina framework for building enterprise applications. It covers authentication, multi-tenancy, RAG search, and more...",
    "category": "Documentation",
    "tags": ["tutorial", "framework", "getting-started"],
    "author": "John Doe"
  }'
```

**⚠️ Common Mistake:** Don't break JSON strings across lines:
```bash
# ❌ WRONG - will cause parse error:
"content": "This is
  broken"

# ✅ CORRECT - keep on one line:
"content": "This is on one line with all the content..."
```

### Task Entity

**Create a Task:**
```bash
curl -X POST https://localhost:5001/api/Task \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Complete project documentation",
    "description": "Write comprehensive API docs with examples",
    "priority": "high",
    "dueDate": "2026-01-30T12:00:00Z",
    "completed": false
  }'
```

### Contact Entity

**Create a Contact:**
```bash
curl -X POST https://localhost:5001/api/Contact \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Jane",
    "lastName": "Smith",
    "email": "jane.smith@example.com",
    "phone": "+1234567890",
    "company": "Acme Corp",
    "notes": "Met at tech conference 2026"
  }'
```

### List Entities
```bash
# Get first page (20 items)
curl https://localhost:5001/api/Document?page=1&pageSize=20

# Get second page (50 items per page)
curl https://localhost:5001/api/Document?page=2&pageSize=50
```

### Search
```bash
# Search documents for keyword
curl https://localhost:5001/api/Document/search?q=tutorial&limit=10

# Search contacts
curl https://localhost:5001/api/Contact/search?q=jane
```

### Get by ID
```bash
curl https://localhost:5001/api/Document/550e8400-e29b-41d4-a716-446655440000
```

### Update
```bash
curl -X PUT https://localhost:5001/api/Document/550e8400-e29b-41d4-a716-446655440000 \
  -H "Content-Type: application/json" \
  -d '{
    "title": "Updated Title",
    "content": "Updated content goes here..."
  }'
```

### Delete (Soft)
```bash
curl -X DELETE https://localhost:5001/api/Document/550e8400-e29b-41d4-a716-446655440000
```

### Count
```bash
curl https://localhost:5001/api/Document/count
```

## Tips for Creating Entities

### 1. Always Check Schema First
```bash
GET /api/{entity}/schema
```
Shows you exactly what fields are required and their types.

### 2. Don't Include Auto-Generated Fields
The following fields are auto-generated - **DO NOT** include them in POST/PUT:
- `id` - Auto-generated GUID
- `createdAt` - Auto-generated timestamp
- `updatedAt` - Auto-generated timestamp
- `isDeleted` - Auto-managed soft delete flag

### 3. Use Proper JSON Formatting
- Keep strings on one line (or escape properly)
- Use double quotes for property names and string values
- Use ISO 8601 format for dates: `"2026-01-30T12:00:00Z"`
- Arrays: `["item1", "item2"]`
- Booleans: `true` or `false` (no quotes)
- Numbers: `42` or `99.99` (no quotes)

### 4. JSON Field Types
For `Json` type fields, you can pass any valid JSON:
```json
{
  "tags": ["tag1", "tag2", "tag3"],
  "metadata": {
    "category": "tutorial",
    "difficulty": "beginner",
    "estimatedTime": 30
  }
}
```
