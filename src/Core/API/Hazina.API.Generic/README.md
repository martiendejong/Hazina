# Hazina.API.Generic

A convention-over-configuration generic API framework that eliminates boilerplate CRUD controller code.

## Why?

In a typical ASP.NET Core API, you write the same CRUD code over and over:
- **75 controllers** in client-manager, most doing the same thing
- Each controller has: List, Get, Create, Update, Delete
- Copy-paste errors, inconsistent behavior, hard to maintain

**Hazina.API.Generic solves this:**
- **One base class** gives you all CRUD operations
- **Convention-based routing** means less configuration
- **YAML/JSON entity definitions** for non-developers
- **Built-in pagination, filtering, sorting**
- **Soft delete, timestamps, multi-tenancy** out of the box

## Quick Start

### 1. Define your entity

```csharp
using Hazina.API.Generic.Entities;

public class Product : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Description { get; set; }
}
```

### 2. Create a controller (one line!)

```csharp
using Hazina.API.Generic.Controllers;

[Route("api/[controller]")]
public class ProductsController : GenericEntityController<Product>
{
    public ProductsController(IRepository<Product> repository) : base(repository) { }
}
```

That's it! You now have:
- `GET /api/products` - List with pagination
- `GET /api/products/{id}` - Get by ID
- `POST /api/products` - Create
- `PUT /api/products/{id}` - Full update
- `PATCH /api/products/{id}` - Partial update
- `DELETE /api/products/{id}` - Soft delete
- `POST /api/products/bulk` - Bulk create
- `DELETE /api/products/bulk` - Bulk delete
- `GET /api/products/count` - Count
- `GET /api/products/exists/{id}` - Check existence

### 3. Register services

```csharp
// In Program.cs
builder.Services.AddDbContext<AppDbContext>(options => ...);
builder.Services.AddGenericEntityApi<AppDbContext>();
builder.Services.AddScoped<ITenantContext, TenantContext>();

// In the pipeline
app.UseAuthentication();
app.UseGenericEntityApi(); // After auth
```

## Entity Base Classes

Choose the right base class for your needs:

| Base Class | Features |
|------------|----------|
| `EntityBase` | ID, CreatedAt, UpdatedAt |
| `SoftDeleteEntityBase` | + Soft delete |
| `TenantEntityBase` | + Multi-tenant |
| `OwnedEntityBase` | + User ownership |
| `FullEntityBase` | All features |
| `EmbeddableEntityBase` | + RAG embeddings |

## Configuration-Driven Entities (No Code!)

Define entities in YAML for your team:

```yaml
# entities.yaml
entities:
  - name: BlogPost
    description: A blog article
    fields:
      - name: title
        type: string
        required: true
        maxLength: 200
        searchable: true
      - name: content
        type: text
        required: true
        searchable: true
      - name: publishedAt
        type: dateTime
      - name: authorId
        type: reference
        referencesEntity: User
    features:
      crud: true
      search: true
      embedding: true
      softDelete: true
```

## API Features

### Pagination

```
GET /api/products?page=2&pageSize=10
```

Response:
```json
{
  "items": [...],
  "page": 2,
  "pageSize": 10,
  "totalCount": 150,
  "totalPages": 15,
  "hasPreviousPage": true,
  "hasNextPage": true
}
```

### Sorting

```
GET /api/products?sortBy=price&sortDesc=true
```

### Filtering

Override `BuildFilterExpression` in your controller for custom filtering:

```csharp
protected override Expression<Func<Product, bool>>? BuildFilterExpression(string filter)
{
    return p => p.Name.Contains(filter) || p.Description.Contains(filter);
}
```

### Bulk Operations

```http
POST /api/products/bulk
Content-Type: application/json

[
  { "name": "Product 1", "price": 10.00 },
  { "name": "Product 2", "price": 20.00 }
]
```

### Partial Updates (PATCH)

```http
PATCH /api/products/123
Content-Type: application/json

{
  "price": 15.00
}
```

Only the specified fields are updated.

## Multi-Tenancy

Entities implementing `IHasTenant` are automatically filtered by tenant:

```csharp
public class Product : TenantEntityBase
{
    public string Name { get; set; }
}
```

The `TenantFilterMiddleware` extracts tenant from:
1. JWT claim `tenant_id`
2. Header `X-Tenant-Id`

## Extending Controllers

Add custom endpoints alongside the generic ones:

```csharp
[Route("api/[controller]")]
public class ProductsController : GenericEntityController<Product>
{
    public ProductsController(IRepository<Product> repository) : base(repository) { }

    // Custom endpoint
    [HttpGet("featured")]
    public async Task<ActionResult<List<Product>>> GetFeatured()
    {
        var featured = await Repository.FindAsync(p => p.IsFeatured);
        return Ok(featured);
    }

    // Override default behavior
    protected override async Task<ActionResult<Product>> Create([FromBody] Product entity)
    {
        // Custom validation
        if (entity.Price < 0)
        {
            return BadRequest("Price cannot be negative");
        }

        return await base.Create(entity);
    }
}
```

## RAG Integration

For entities that should be searchable via embeddings:

```csharp
public class Document : EmbeddableEntityBase
{
    public string Title { get; set; }
    public string Content { get; set; }

    public override string GetSearchableText()
    {
        return $"{Title}\n\n{Content}";
    }
}
```

The embedding is computed automatically and stored in the `Embedding` column.

## Migration from Existing Controllers

1. **Identify CRUD-only controllers** - These can be replaced directly
2. **Keep custom logic** - Move to override methods or separate endpoints
3. **Update routes** - Ensure consistency with new convention

Before:
```csharp
// 200+ lines of code
public class ProductsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() { ... }
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id) { ... }
    // ... more boilerplate
}
```

After:
```csharp
// 5 lines of code
public class ProductsController : GenericEntityController<Product>
{
    public ProductsController(IRepository<Product> repository) : base(repository) { }
}
```

## Best Practices

1. **Use EntityBase** for simple entities
2. **Use SoftDeleteEntityBase** when you need audit trails
3. **Override methods** instead of writing from scratch
4. **Use YAML definitions** for entities managed by non-developers
5. **Keep custom logic minimal** - most CRUD should be generic

## License

MIT - Part of the Hazina framework
