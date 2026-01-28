using Hazina.AgenticOrchestration.Extensions;
using Hazina.AgenticOrchestration.Services;
using Hazina.Demo.AgenticOrchestration.Authentication;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════
// CONFIGURATION
// ═══════════════════════════════════════════════════════════════
var config = builder.Configuration.GetSection("AgenticOrchestration");
var dbPath = config["DatabasePath"] ?? @"C:\scripts\_machine\agent-activity.db";
var logsPath = config["LogsPath"] ?? @"C:\scripts\logs";

// Terminal configuration
var terminalConfig = config.GetSection("Terminal");
var defaultCommand = terminalConfig["DefaultCommand"] ?? "claude";
var defaultWorkingDirectory = terminalConfig["DefaultWorkingDirectory"];
var defaultArguments = terminalConfig.GetSection("DefaultArguments").Get<string[]>() ?? Array.Empty<string>();
var defaultColumns = int.TryParse(terminalConfig["DefaultColumns"], out var cols) ? cols : 120;
var defaultRows = int.TryParse(terminalConfig["DefaultRows"], out var rows) ? rows : 30;
var maxSessions = int.TryParse(terminalConfig["MaxConcurrentSessions"], out var max) ? max : 10;
var sessionTimeout = int.TryParse(terminalConfig["SessionTimeoutMinutes"], out var timeout) ? timeout : 60;

// Authentication configuration
var authConfig = builder.Configuration.GetSection("Authentication");
var authEnabled = bool.TryParse(authConfig["Enabled"], out var enabled) && enabled;
var authUsername = authConfig["Username"] ?? "admin";
var authPassword = authConfig["Password"] ?? "changeme";
var authRealm = authConfig["Realm"] ?? "Hazina Agentic Orchestration";

Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║     HAZINA AGENTIC ORCHESTRATION - Demo Application              ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"║  Database: {dbPath,-50} ║");
Console.WriteLine($"║  Logs: {logsPath,-54} ║");
Console.WriteLine($"║  Terminal Command: {defaultCommand,-42} ║");
Console.WriteLine($"║  Working Directory: {defaultWorkingDirectory ?? "(current)",-41} ║");
Console.WriteLine($"║  Authentication: {(authEnabled ? $"ENABLED (user: {authUsername})" : "DISABLED"),-44} ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");

// ═══════════════════════════════════════════════════════════════
// SERVICE REGISTRATION - Hazina Declarative Style
// ═══════════════════════════════════════════════════════════════

// Hazina Agentic Orchestration (one-liner declarative registration)
builder.Services.AddHazinaAgenticOrchestration(options =>
{
    options.DatabasePath = dbPath;
    options.LogsPath = logsPath;
    options.EnableSignalR = true;
    options.EnableTerminalStreaming = true;
    // Terminal defaults from config
    options.DefaultCommand = defaultCommand;
    options.DefaultWorkingDirectory = defaultWorkingDirectory;
    options.DefaultArguments = defaultArguments;
    options.DefaultTerminalColumns = defaultColumns;
    options.DefaultTerminalRows = defaultRows;
    options.MaxConcurrentSessions = maxSessions;
    options.SessionTimeoutMinutes = sessionTimeout;
});
Console.WriteLine("✅ Hazina Agentic Orchestration services registered (declarative)");

// Authentication
builder.Services.AddAuthentication(BasicAuthenticationExtensions.SchemeName)
    .AddBasicAuthentication(options =>
    {
        options.Enabled = authEnabled;
        options.Username = authUsername;
        options.Password = authPassword;
        options.Realm = authRealm;
    });

builder.Services.AddAuthorization();
Console.WriteLine($"✅ Basic Authentication configured (enabled: {authEnabled})");

// ASP.NET Core Services
builder.Services.AddControllers()
    .AddApplicationPart(typeof(Hazina.AgenticOrchestration.Controllers.TerminalController).Assembly);
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

    // Add Basic Authentication to Swagger
    if (authEnabled)
    {
        c.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "basic",
            Description = "Basic HTTP Authentication. Enter your username and password."
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Basic"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
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

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Serve React SPA static files
app.UseDefaultFiles();
app.UseStaticFiles();

// ═══════════════════════════════════════════════════════════════
// ENDPOINT MAPPING - Hazina Declarative Style
// ═══════════════════════════════════════════════════════════════

// Map Controllers (REST API) - require authentication if enabled
app.MapControllers().RequireAuthorization();

// Map SignalR Hubs (declarative one-liner)
// Note: SignalR hub authorization is handled via [Authorize] attribute on hub classes
app.MapHazinaAgenticHubs();

// ═══════════════════════════════════════════════════════════════
// MINIMAL API ENDPOINTS
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


// API root - list available endpoints (requires auth)
app.MapGet("/api", () => Results.Ok(new
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
        Terminal = new
        {
            Create = "POST /api/terminal/sessions",
            List = "GET /api/terminal/sessions",
            Details = "GET /api/terminal/sessions/{sessionId}",
            Terminate = "DELETE /api/terminal/sessions/{sessionId}",
            SignalR = "/hubs/terminal"
        },
        SignalR = new
        {
            AgenticHub = "/hubs/agentic",
            TerminalHub = "/hubs/terminal"
        }
    }
}))
.WithName("ApiRoot")
.WithOpenApi()
.RequireAuthorization();

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
.WithOpenApi()
.RequireAuthorization();

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
.WithOpenApi()
.RequireAuthorization();

// ═══════════════════════════════════════════════════════════════
// STARTUP MESSAGE
// ═══════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║                    🚀 SERVER STARTED                             ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Listening on: (see URLs below from Kestrel)                     ║");
Console.WriteLine("║  Swagger UI:   /swagger                                          ║");
Console.WriteLine("║  React UI:     /                                                 ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  SignalR Hubs:                                                   ║");
Console.WriteLine("║    /hubs/agentic   (Instance management)                         ║");
Console.WriteLine("║    /hubs/terminal  (Real-time terminal)                          ║");
Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
Console.WriteLine("║  Terminal API:                                                   ║");
Console.WriteLine("║    POST   /api/terminal/sessions      - Create session           ║");
Console.WriteLine("║    GET    /api/terminal/sessions      - List sessions            ║");
Console.WriteLine("║    DELETE /api/terminal/sessions/{id} - Terminate session        ║");
if (authEnabled)
{
    Console.WriteLine("╠══════════════════════════════════════════════════════════════════╣");
    Console.WriteLine("║  🔐 AUTHENTICATION REQUIRED                                      ║");
    Console.WriteLine($"║     Username: {authUsername,-51} ║");
    Console.WriteLine("║     Password: (configured in appsettings.json)                   ║");
}
Console.WriteLine("╚══════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// SPA fallback - serve index.html for client-side routing
app.MapFallbackToFile("index.html");

app.Run();
