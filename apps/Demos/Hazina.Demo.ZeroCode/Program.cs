// ============================================================
// HAZINA ZERO-CODE API DEMO
// ============================================================
// This entire API is defined in entities.yaml - NO C# CODE NEEDED!
//
// Just define your entities in YAML and this Program.cs sets up:
// - REST API endpoints for all entities
// - SQLite database (auto-created)
// - Full CRUD operations
// - Search and pagination
// - Swagger documentation
//
// Available endpoints (for each entity defined in YAML):
//   GET    /api/{entity}           - List all (paginated)
//   GET    /api/{entity}/{id}      - Get by ID
//   POST   /api/{entity}           - Create
//   PUT    /api/{entity}/{id}      - Update
//   DELETE /api/{entity}/{id}      - Delete (soft)
//   GET    /api/{entity}/count     - Count
//   GET    /api/{entity}/search    - Search
//   GET    /api/{entity}/schema    - Get schema
//   GET    /api/_entities          - List all entity types
// ============================================================

using Hazina.API.Generic.Dynamic;
using Hazina.LLMs;

var builder = WebApplication.CreateBuilder(args);

// === THIS IS ALL THE CODE YOU NEED ===
// Everything else comes from the YAML file!

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "Zero-Code API",
        Version = "v1",
        Description = @"# API auto-generated from entities.yaml - no C# code needed!

## Available Entities
This API dynamically generates endpoints for all entities defined in `entities.yaml`.

## Entity Schemas
Each entity automatically gets these endpoints:
- `GET /api/{entity}` - List all (paginated)
- `GET /api/{entity}/{id}` - Get by ID
- `POST /api/{entity}` - Create new
- `PUT /api/{entity}/{id}` - Update existing
- `DELETE /api/{entity}/{id}` - Delete (soft by default)
- `GET /api/{entity}/count` - Count total
- `GET /api/{entity}/search?q={term}` - Full-text search
- `GET /api/{entity}/schema` - Get field definitions

## Quick Start
1. View entity schemas: `GET /api/{entity}/schema`
2. Create: `POST /api/{entity}` with JSON body
3. List: `GET /api/{entity}`
4. Search: `GET /api/{entity}/search?q=keyword`

## Current Entities
- **Document** - Searchable documents with RAG embedding
- **Task** - Todo items with priorities
- **Contact** - Contact information
"
    });

    // Enable detailed descriptions and examples
    c.EnableAnnotations();
    c.DescribeAllParametersInCamelCase();
});

// Load entities from YAML and set up the dynamic API
// Database is auto-created at the specified path
builder.Services.AddHazinaDynamicApi(
    yamlPath: "entities.yaml",
    databasePath: "app.db"
);

// Configure Ollama for RAG chat
var ollamaConfig = new OllamaConfig
{
    Endpoint = builder.Configuration["Ollama:Endpoint"] ?? "http://85.215.217.154:5555",
    Model = builder.Configuration["Ollama:Model"] ?? "phi3:mini",
    EmbeddingModel = builder.Configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text"
};

// Read password for HTTP Basic Auth
var ollamaPassword = builder.Configuration["Ollama:Password"];

var ollamaClient = new OllamaClientWrapper(ollamaConfig);

// Add HTTP Basic Authentication if password is configured
if (!string.IsNullOrWhiteSpace(ollamaPassword))
{
    var authValue = Convert.ToBase64String(
        System.Text.Encoding.ASCII.GetBytes($"ollama:{ollamaPassword}"));

    Console.WriteLine($"Setting up Ollama authentication with user: ollama");

    // Create a new HttpClient with auth header and replace the internal one
    var authenticatedHttpClient = new HttpClient
    {
        BaseAddress = new Uri(ollamaConfig.Endpoint?.TrimEnd('/') ?? "http://localhost:11434"),
        Timeout = TimeSpan.FromMinutes(5)
    };
    authenticatedHttpClient.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authValue);

    // Replace the internal HttpClient via reflection
    var httpClientField = typeof(OllamaClientWrapper).GetField("_http",
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

    if (httpClientField != null)
    {
        // Get and dispose the old client
        var oldHttpClient = (HttpClient?)httpClientField.GetValue(ollamaClient);
        oldHttpClient?.Dispose();

        // Set the new authenticated client
        httpClientField.SetValue(ollamaClient, authenticatedHttpClient);
        Console.WriteLine("Successfully injected authenticated HttpClient into OllamaClientWrapper");
    }
    else
    {
        Console.WriteLine("WARNING: Could not find _http field in OllamaClientWrapper - authentication may fail");
    }
}

builder.Services.AddSingleton<ILLMClient>(ollamaClient);

var app = builder.Build();

// Configure pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Zero-Code API v1");
    c.RoutePrefix = string.Empty; // Serve Swagger at root
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine("  HAZINA ZERO-CODE API");
Console.WriteLine("=================================================");
Console.WriteLine();
Console.WriteLine("  Entities loaded from: entities.yaml");
Console.WriteLine("  Database: app.db");
Console.WriteLine();
Console.WriteLine("  Open Swagger UI: https://localhost:5001");
Console.WriteLine();
Console.WriteLine("  Available entity endpoints:");
Console.WriteLine("    - /api/Document  - CRUD + RAG embeddings");
Console.WriteLine("    - /api/Task      - Simple todo items");
Console.WriteLine("    - /api/Contact   - Contact management");
Console.WriteLine();
Console.WriteLine("  RAG Chat endpoint:");
Console.WriteLine("    - /api/chat      - AI chat with document context");
Console.WriteLine();
Console.WriteLine("  Using Ollama at: " + ollamaConfig.Endpoint);
Console.WriteLine("  Model: " + ollamaConfig.Model);
Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine();

app.Run();
