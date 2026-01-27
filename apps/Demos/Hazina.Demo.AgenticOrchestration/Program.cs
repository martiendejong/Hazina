using Hazina.AgenticOrchestration.Data;
using Hazina.AgenticOrchestration.Hubs;
using Hazina.AgenticOrchestration.Services;
using Hazina.AgenticOrchestration.Terminal;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════
// CONFIGURATION
// ═══════════════════════════════════════════════════════════════
var config = builder.Configuration.GetSection("AgenticOrchestration");
var dbPath = config["DatabasePath"] ?? @"C:\scripts\_machine\agent-activity.db";
var logsPath = config["LogsPath"] ?? @"C:\scripts\logs";

Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     HAZINA AGENTIC ORCHESTRATION - Demo Application              ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  Database: {dbPath,-50} ║");
Console.WriteLine($"║  Logs: {logsPath,-54} ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");

// ═══════════════════════════════════════════════════════════════
// DATABASE INITIALIZATION
// ═══════════════════════════════════════════════════════════════
var dbInitializer = new DatabaseInitializer(dbPath);
dbInitializer.Initialize();
Console.WriteLine("✅ Database initialized");

// ═══════════════════════════════════════════════════════════════
// SERVICE REGISTRATION - Hazina Declarative Style
// ═══════════════════════════════════════════════════════════════

// Core Agentic Orchestration Services
builder.Services.AddSingleton<IClaudeInstanceManager>(
    new ClaudeInstanceManager(dbPath));

builder.Services.AddSingleton<IOutputCaptureService>(sp =>
    new OutputCaptureService(
        sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<ClaudeOrchestrationHub>>(),
        logsPath));

builder.Services.AddSingleton<IInteractionService>(sp =>
    new InteractionService(
        sp.GetRequiredService<Microsoft.AspNetCore.SignalR.IHubContext<ClaudeOrchestrationHub>>(),
        dbPath));

// Terminal Services (real-time process control)
builder.Services.AddSingleton<ITerminalSessionManager, TerminalSessionManager>();
Console.WriteLine("✅ Terminal services registered");

// ASP.NET Core Services
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();

// Swagger
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = builder.Configuration["Swagger:Title"] ?? "Hazina Agentic Orchestration API",
        Description = builder.Configuration["Swagger:Description"] ?? "Web API for managing Claude Code CLI instances",
        Version = builder.Configuration["Swagger:Version"] ?? "v1",
        Contact = new OpenApiContact
        {
            Name = "Hazina Framework",
            Url = new Uri("https://github.com/martiendejong/Hazina")
        }
    });
});

// CORS for frontend development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });

    options.AddPolicy("SignalR", policy =>
    {
        policy.WithOrigins("http://localhost:3000", "http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════
// MIDDLEWARE PIPELINE
// ═══════════════════════════════════════════════════════════════

// Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hazina Agentic Orchestration v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors();
app.UseRouting();

// Serve React SPA static files
app.UseDefaultFiles();
app.UseStaticFiles();

// ═══════════════════════════════════════════════════════════════
// ENDPOINT MAPPING
// ═══════════════════════════════════════════════════════════════

// Map Controllers (REST API)
app.MapControllers();

// Map SignalR Hubs (Real-time)
app.MapHub<ClaudeOrchestrationHub>("/hubs/agentic");
app.MapHub<TerminalHub>("/hubs/terminal");

// ═══════════════════════════════════════════════════════════════
// MINIMAL API ENDPOINTS - Hazina Declarative Style
// ═══════════════════════════════════════════════════════════════

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    Status = "healthy",
    Service = "Hazina Agentic Orchestration",
    Timestamp = DateTime.UtcNow
}))
.WithName("HealthCheck")
.WithOpenApi();

// API root - list available endpoints
app.MapGet("/", () => Results.Ok(new
{
    Service = "Hazina Agentic Orchestration API",
    Version = "1.0.0",
    Documentation = "/swagger",
    Endpoints = new
    {
        Instances = new
        {
            List = "GET /api/agentic/instances",
            Details = "GET /api/agentic/instances/{sessionId}",
            Output = "GET /api/agentic/instances/{sessionId}/output"
        },
        Interactions = new
        {
            AwaitInput = "POST /api/agentic/instances/{sessionId}/await-input",
            CheckResponse = "GET /api/agentic/instances/{sessionId}/interactions/{id}/response",
            Respond = "POST /api/agentic/instances/{sessionId}/interactions/{id}/respond",
            AllPending = "GET /api/agentic/interactions/pending"
        },
        SignalR = new
        {
            Hub = "/hubs/agentic",
            Events = new[] { "ReceiveOutput", "InputRequired", "ResponseReceived", "GlobalInputRequired" }
        },
        Terminal = new
        {
            Create = "POST /api/terminal/sessions",
            List = "GET /api/terminal/sessions",
            Details = "GET /api/terminal/sessions/{sessionId}",
            Terminate = "DELETE /api/terminal/sessions/{sessionId}",
            SignalR = "/hubs/terminal"
        }
    }
}))
.WithName("ApiRoot")
.WithOpenApi();

// ═══════════════════════════════════════════════════════════════
// CUSTOM CONVENIENCE ENDPOINTS
// ═══════════════════════════════════════════════════════════════

// Quick endpoint to get active instance count
app.MapGet("/api/stats", async (IClaudeInstanceManager instanceManager) =>
{
    var instances = await instanceManager.GetActiveInstancesAsync();
    return Results.Ok(new
    {
        ActiveInstances = instances.Count,
        Instances = instances.Select(i => new
        {
            i.SessionId,
            i.AgentName,
            i.Status,
            i.CurrentTask,
            Runtime = (DateTime.UtcNow - i.StartTime).ToString(@"hh\:mm\:ss")
        })
    });
})
.WithName("GetStats")
.WithOpenApi();

// Pending interaction count
app.MapGet("/api/interactions/count", async (IInteractionService interactionService) =>
{
    var pending = await interactionService.GetPendingInteractionsAsync();
    return Results.Ok(new
    {
        PendingCount = pending.Count,
        OldestPending = pending.OrderBy(p => p.CreatedAt).FirstOrDefault()?.CreatedAt
    });
})
.WithName("GetInteractionCount")
.WithOpenApi();

// ═══════════════════════════════════════════════════════════════
// STARTUP MESSAGE
// ═══════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    🚀 SERVER STARTED                             ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  REST API:    http://localhost:5000                              ║");
Console.WriteLine("║  Swagger UI:  http://localhost:5000/swagger                      ║");
Console.WriteLine("║  SignalR Hub: ws://localhost:5000/hubs/agentic                   ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Endpoints:                                                      ║");
Console.WriteLine("║    GET  /api/agentic/instances          - List active instances  ║");
Console.WriteLine("║    GET  /api/agentic/instances/{id}     - Instance details       ║");
Console.WriteLine("║    GET  /api/agentic/instances/{id}/output - Get output          ║");
Console.WriteLine("║    POST /api/agentic/instances/{id}/await-input - Request input  ║");
Console.WriteLine("║    POST /api/agentic/.../respond        - Submit response        ║");
Console.WriteLine("║    GET  /api/agentic/interactions/pending - All pending          ║");
Console.WriteLine("║  Terminal Hub: ws://localhost:5000/hubs/terminal                 ║");
Console.WriteLine("║  React UI:     http://localhost:5000                             ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// SPA fallback - serve index.html for client-side routing
app.MapFallbackToFile("index.html");

app.Run();
