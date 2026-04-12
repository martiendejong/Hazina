using System.Diagnostics;
using System.Text;
using Hazina.AgenticOrchestration.Extensions;
using Hazina.Core.Plugins.Lua.DI;
using Hazina.AgenticOrchestration.Services;
using Hazina.Demo.AgenticOrchestration;
using Hazina.Demo.AgenticOrchestration.Authentication;
using Hazina.LLMs;
using Hazina.LLMs.OpenAI;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// ═══════════════════════════════════════════════════════════════
// REDIRECT CONSOLE OUTPUT TO LOG FILE (WinExe has no console)
// ═══════════════════════════════════════════════════════════════
var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "HazinaOrchestration", "logs");
Directory.CreateDirectory(logDir);
var logFile = Path.Combine(logDir, $"orchestration-{DateTime.Now:yyyy-MM-dd}.log");
var logWriter = new StreamWriter(logFile, append: true) { AutoFlush = true };
Console.SetOut(logWriter);
Console.SetError(logWriter);

// ═══════════════════════════════════════════════════════════════
// BUILD WEB APPLICATION
// ═══════════════════════════════════════════════════════════════
var builder = WebApplication.CreateBuilder(args);

// Enable Windows Service hosting
builder.Host.UseWindowsService();

// Load secrets file (not committed to git)
builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: true);

// ═══════════════════════════════════════════════════════════════
// CONFIGURATION
// ═══════════════════════════════════════════════════════════════
var config = builder.Configuration.GetSection("AgenticOrchestration");
var dbPath = config["DatabasePath"] ?? @"C:\scripts\_machine\agent-activity.db";
var logsPath = config["LogsPath"] ?? @"C:\scripts\logs";

// Ensure data directories exist (important for fresh installations)
try
{
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath) ?? "data");
    Directory.CreateDirectory(logsPath);
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: Could not create directories: {ex.Message}");
}

// Terminal configuration
var terminalConfig = config.GetSection("Terminal");
var defaultCommand = terminalConfig["DefaultCommand"] ?? "claude";
var defaultWorkingDirectory = terminalConfig["DefaultWorkingDirectory"];
var defaultArguments = terminalConfig.GetSection("DefaultArguments").Get<string[]>() ?? Array.Empty<string>();
var defaultColumns = int.TryParse(terminalConfig["DefaultColumns"], out var cols) ? cols : 120;
var defaultRows = int.TryParse(terminalConfig["DefaultRows"], out var rows) ? rows : 30;
var maxSessions = int.TryParse(terminalConfig["MaxConcurrentSessions"], out var max) ? max : 10;
var sessionTimeout = int.TryParse(terminalConfig["SessionTimeoutMinutes"], out var timeout) ? timeout : 60;

// Session logging configuration
var sessionLoggingConfig = config.GetSection("SessionLogging");
var sessionLoggingEnabled = !bool.TryParse(sessionLoggingConfig["Enabled"], out var logEnabled) || logEnabled; // Default: true
var sessionLogsBasePath = sessionLoggingConfig["BasePath"] ?? @"C:\scripts\logs\agent-sessions";

// Ensure session logs directory exists
try { Directory.CreateDirectory(sessionLogsBasePath); }
catch (Exception ex) { Console.WriteLine($"Warning: Could not create session logs directory: {ex.Message}"); }

// Uploads configuration
var uploadsConfig = config.GetSection("Uploads");
var uploadsPath = uploadsConfig["Path"] ?? "uploads";
var maxUploadFileSizeMB = int.TryParse(uploadsConfig["MaxFileSizeMB"], out var uploadSize) ? uploadSize : 50;

// Ensure uploads directory exists
try { Directory.CreateDirectory(uploadsPath); }
catch (Exception ex) { Console.WriteLine($"Warning: Could not create uploads directory: {ex.Message}"); }

// Authentication configuration
var authConfig = builder.Configuration.GetSection("Authentication");
var authEnabled = bool.TryParse(authConfig["Enabled"], out var enabled) && enabled;
var authUsername = authConfig["Username"] ?? "admin";
var authPassword = authConfig["Password"] ?? "changeme";
var authRealm = authConfig["Realm"] ?? "Hazina Agentic Orchestration";

Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ╔══════════════════════════════════════════════════════════════════╗");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ║     HAZINA AGENTIC ORCHESTRATION - Desktop Tray App              ║");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ╠══════════════════════════════════════════════════════════════════╣");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ║  Database: {dbPath,-50} ║");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ║  Logs: {logsPath,-54} ║");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ║  Terminal Command: {defaultCommand,-42} ║");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ║  Working Directory: {defaultWorkingDirectory ?? "(current)",-41} ║");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ║  Session Logging: {(sessionLoggingEnabled ? "ENABLED" : "DISABLED"),-43} ║");
if (sessionLoggingEnabled)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ║  Session Logs: {sessionLogsBasePath,-46} ║");
}
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ║  Authentication: {(authEnabled ? $"ENABLED (user: {authUsername})" : "DISABLED"),-44} ║");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ╚══════════════════════════════════════════════════════════════════╝");

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
    // Session logging
    options.EnableSessionLogging = sessionLoggingEnabled;
    options.AgentSessionLogsPath = sessionLogsBasePath;
    // Uploads
    options.UploadsPath = uploadsPath;
    options.MaxUploadFileSizeMB = maxUploadFileSizeMB;
});
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Hazina Agentic Orchestration services registered (declarative)");

// OpenAI LLM Client for Chat Agent (optional - won't crash if API key is missing)
try
{
    var openAIConfig = OpenAIConfig.FromConfiguration(builder.Configuration);
    if (!string.IsNullOrWhiteSpace(openAIConfig.ApiKey) && openAIConfig.ApiKey != "YOUR_OPENAI_API_KEY_HERE")
    {
        builder.Services.AddSingleton<ILLMClient>(new OpenAIClientWrapper(openAIConfig));
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] OpenAI LLM Client registered (model: {openAIConfig.Model})");
    }
    else
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] OpenAI LLM Client skipped (no API key configured)");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] OpenAI LLM Client skipped: {ex.Message}");
}

// Lua Scripting Layer — hot-reloadable agent behavior scripts
builder.Services.AddLuaScripting(options =>
{
    options.ScriptsDirectory = "lua-scripts";
    options.HotReloadEnabled = true;
    options.ExecutionTimeout = TimeSpan.FromSeconds(30);
});
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Lua scripting layer registered");

// ClickUp Orchestration Service (monitors internal projects for TODO/REVIEW/BACKLOG tasks)
builder.Services.AddHttpClient();
builder.Services.AddHostedService<Hazina.AgenticOrchestration.Services.ClickUpOrchestrationService>();
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] ClickUp Orchestration Service registered");

// JWT Configuration
var jwtConfig = authConfig.GetSection("Jwt");
var jwtEnabled = bool.TryParse(jwtConfig["Enabled"], out var jwtEn) && jwtEn;
var jwtSecretKey = jwtConfig["SecretKey"] ?? string.Empty;
var jwtIssuer = jwtConfig["Issuer"] ?? "HazinaOrchestration";
var jwtAudience = jwtConfig["Audience"] ?? "HazinaOrchestrationClient";

// Register JWT services
if (jwtEnabled)
{
    builder.Services.AddSingleton<JwtService>();
    builder.Services.AddSingleton<RefreshTokenStore>();
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] JWT services registered");
}

// Authentication - support both Basic and JWT Bearer
builder.Services.AddAuthentication(options =>
{
    // Default to JWT if enabled, otherwise Basic Auth
    options.DefaultAuthenticateScheme = jwtEnabled ? JwtBearerDefaults.AuthenticationScheme : BasicAuthenticationExtensions.SchemeName;
    options.DefaultChallengeScheme = jwtEnabled ? JwtBearerDefaults.AuthenticationScheme : BasicAuthenticationExtensions.SchemeName;
})
.AddBasicAuthentication(options =>
{
    options.Enabled = authEnabled;
    options.Username = authUsername;
    options.Password = authPassword;
    options.Realm = authRealm;
});

// Add JWT Bearer authentication if enabled
if (jwtEnabled && !string.IsNullOrEmpty(jwtSecretKey))
{
    builder.Services.AddAuthentication()
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };
        });
}

builder.Services.AddAuthorization();
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Authentication configured - Basic: {authEnabled}, JWT: {jwtEnabled}");

// ASP.NET Core Services
builder.Services.AddControllers()
    .AddApplicationPart(typeof(Hazina.AgenticOrchestration.Controllers.TerminalController).Assembly);
builder.Services.AddEndpointsApiExplorer();

// Configure max upload size for multipart form data
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = (long)maxUploadFileSizeMB * 1024 * 1024;
});

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

    // Add authentication schemes to Swagger
    if (jwtEnabled)
    {
        c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Authorization header using the Bearer scheme. Enter your JWT token."
        });

        c.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    }

    if (authEnabled)
    {
        c.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "basic",
            Description = "Basic HTTP Authentication. Enter your username and password."
        });

        // Only add Basic to requirements if JWT is not enabled
        if (!jwtEnabled)
        {
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

// Swagger UI - always enabled (desktop app always needs it)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Hazina Agentic Orchestration v1");
    c.RoutePrefix = "swagger";
});

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
app.MapHazinaAgenticHubs();

// ═══════════════════════════════════════════════════════════════
// MINIMAL API ENDPOINTS
// ═══════════════════════════════════════════════════════════════

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    Status = "healthy",
    Service = "Hazina Agentic Orchestration",
    Mode = "Desktop Tray App",
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
// STARTUP LOG
// ═══════════════════════════════════════════════════════════════

Console.WriteLine();
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] SERVER STARTED - Desktop Tray App");
Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Swagger: /swagger | React UI: / | SignalR: /hubs/agentic, /hubs/terminal");
if (authEnabled)
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Authentication: ENABLED (user: {authUsername})");
Console.WriteLine();

// SPA fallback - serve index.html for client-side routing
app.MapFallbackToFile("index.html");

// ═══════════════════════════════════════════════════════════════
// START: Web host (service mode) OR tray app (desktop mode)
// ═══════════════════════════════════════════════════════════════

// Detect if running as Windows Service (non-interactive) or desktop app (interactive)
var isServiceMode = !Environment.UserInteractive;

if (isServiceMode)
{
    // ═══════════════════════════════════════════════════════════════
    // SERVICE MODE: Run as background service without UI
    // ═══════════════════════════════════════════════════════════════
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Running in SERVICE MODE (no UI)");

    try
    {
        await app.RunAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Web host error: {ex.Message}");
    }
    finally
    {
        logWriter.Flush();
        logWriter.Dispose();
    }
}
else
{
    // ═══════════════════════════════════════════════════════════════
    // DESKTOP MODE: Run with system tray UI
    // ═══════════════════════════════════════════════════════════════
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Running in DESKTOP MODE (with system tray)");

    // Capture URLs BEFORE starting the web host to avoid ObjectDisposedException
    var baseUrl = app.Urls.FirstOrDefault(u => u.StartsWith("https://"))
                  ?? app.Urls.FirstOrDefault()
                  ?? "https://localhost:5123";

    // Normalize 0.0.0.0 and [::] to localhost for browser URLs
    baseUrl = baseUrl.Replace("://0.0.0.0:", "://localhost:")
                     .Replace("://[::]:", "://localhost:");

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Base URL: {baseUrl}");

    // Start the ASP.NET Core web host in the background
    _ = Task.Run(async () =>
    {
        try
        {
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Web host error: {ex.Message}");
        }
    });

    // Give the web host a moment to start listening
    await Task.Delay(500);

    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Starting system tray application...");

    // Run WinForms on the main thread (required for message pump)
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.Run(new TrayApplicationContext(app, baseUrl));

    // Cleanup after tray app exits
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Application shutting down...");
    logWriter.Flush();
    logWriter.Dispose();
}
