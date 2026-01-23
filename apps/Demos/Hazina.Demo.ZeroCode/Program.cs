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
        Description = "API auto-generated from entities.yaml - no C# code needed!"
    });
});

// Load entities from YAML and set up the dynamic API
// Database is auto-created at the specified path
builder.Services.AddHazinaDynamicApi(
    yamlPath: "entities.yaml",
    databasePath: "app.db"
);

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
Console.WriteLine("    - /api/documents");
Console.WriteLine("    - /api/tasks");
Console.WriteLine("    - /api/contacts");
Console.WriteLine();
Console.WriteLine("=================================================");
Console.WriteLine();

app.Run();
