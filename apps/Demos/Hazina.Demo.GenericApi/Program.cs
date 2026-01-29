using Hazina.API.Generic.Extensions;
using Hazina.API.Generic.Middleware;
using Hazina.Demo.GenericApi.Data;
using Hazina.Demo.GenericApi.Services;
using Hazina.Store.EmbeddingStore;
using Hazina.Tools.Data.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ============================================
// Database Configuration
// ============================================

// Use SQLite for simplicity - no external database needed
var dbPath = Path.Combine(builder.Environment.ContentRootPath, "demo.db");
builder.Services.AddDbContext<DemoDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

// ============================================
// Generic API Services
// ============================================

// Register generic repository for all entity types
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

// Register tenant context (required for middleware)
builder.Services.AddScoped<ITenantContext, TenantContext>();

// ============================================
// Semantic Search Services
// ============================================

// Use mock embedding generator for demo
// For production: use OpenAIEmbeddingGenerator with your API key
builder.Services.AddSingleton<IEmbeddingGenerator, MockEmbeddingGenerator>();
builder.Services.AddScoped<ISemanticSearchService, SemanticSearchService>();

// ============================================
// API Configuration
// ============================================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Swagger/OpenAPI with grouping
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hazina Generic API Demo",
        Version = "v1",
        Description = @"
## Demo API showing Hazina.API.Generic in action

This API demonstrates:
- **Documents** - Full CRUD + semantic search with embeddings
- **Notes** - Simple CRUD with custom endpoints
- **Tags** - Minimal generic controller (just 1 line of code!)

### Quick Start

1. **Create some documents** via `POST /api/documents`
2. **Generate embeddings** via `POST /api/documents/embed-all`
3. **Search semantically** via `POST /api/documents/search`

### Code Example

```csharp
// This controller gives you 10 endpoints automatically!
public class TagsController : GenericEntityController<Tag>
{
    public TagsController(IRepository<Tag> repository)
        : base(repository) { }
}
```
",
        Contact = new OpenApiContact
        {
            Name = "Hazina Team",
            Url = new Uri("https://github.com/martiendejong/hazina")
        }
    });

    // Enable XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

// CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ============================================
// Database Initialization
// ============================================

// Ensure database is created with seed data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DemoDbContext>();
    db.Database.EnsureCreated();

    Console.WriteLine($"Database initialized at: {dbPath}");
    Console.WriteLine($"Documents: {db.Documents.Count()}");
    Console.WriteLine($"Notes: {db.Notes.Count()}");
    Console.WriteLine($"Tags: {db.Tags.Count()}");
}

// ============================================
// Middleware Pipeline
// ============================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hazina Generic API Demo v1");
        c.RoutePrefix = string.Empty; // Swagger at root
        c.DocumentTitle = "Hazina API Demo";
    });
}

app.UseHttpsRedirection();
app.UseCors();

// Generic API middleware (tenant filtering)
app.UseGenericEntityApi();

app.MapControllers();

// ============================================
// Startup Banner
// ============================================

Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════╗
║           Hazina.API.Generic Demo Server                  ║
╠═══════════════════════════════════════════════════════════╣
║  Swagger UI: https://localhost:5001                       ║
║  API Base:   https://localhost:5001/api                   ║
╠═══════════════════════════════════════════════════════════╣
║  Endpoints:                                               ║
║    /api/documents - CRUD + Semantic Search                ║
║    /api/notes     - Simple CRUD                           ║
║    /api/tags      - Minimal Generic Controller            ║
╚═══════════════════════════════════════════════════════════╝
");

app.Run();
