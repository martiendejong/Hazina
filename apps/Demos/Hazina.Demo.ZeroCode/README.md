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

## Example Requests

### Create a Document
```bash
curl -X POST https://localhost:5001/api/documents \
  -H "Content-Type: application/json" \
  -d '{
    "title": "My First Document",
    "content": "This is the content.",
    "category": "General"
  }'
```

### List Documents
```bash
curl https://localhost:5001/api/documents?page=1&pageSize=10
```

### Search
```bash
curl https://localhost:5001/api/documents/search?q=first
```

### Get Schema
```bash
curl https://localhost:5001/api/documents/schema
```
