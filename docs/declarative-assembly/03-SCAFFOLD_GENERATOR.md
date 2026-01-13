# Scaffold Generator - Implementation Plan

**Parent Document:** [README.md](./README.md)
**Status:** Planning
**Created:** 2026-01-13

---

## Overview

The Scaffold Generator is the **code generation engine** that transforms Assembly Specifications into complete, runnable .NET projects. It bridges the gap between declarative configuration and working code.

### Key Responsibilities

1. **Project Structure Generation** - Create folders, files, solution
2. **DI Configuration** - Wire up all services from spec
3. **Controller Generation** - Create API endpoints from modules
4. **Configuration Files** - appsettings.json, Docker, etc.
5. **Template Application** - Apply code templates with substitutions

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                      ScaffoldGenerator                               │
└─────────────────────────────────────────────────────────────────────┘
                                  │
        ┌─────────────────────────┼─────────────────────────┐
        │                         │                         │
        ▼                         ▼                         ▼
┌──────────────┐        ┌──────────────┐        ┌──────────────┐
│   Project    │        │   Service    │        │   Config     │
│  Generator   │        │  Generator   │        │  Generator   │
│              │        │              │        │              │
│ .csproj      │        │ Controllers  │        │ appsettings  │
│ Directory.   │        │ Services     │        │ Docker       │
│  Build.props │        │ Models       │        │ Startup      │
│ Solution     │        │ Middleware   │        │ Secrets      │
└──────────────┘        └──────────────┘        └──────────────┘
        │                         │                         │
        └─────────────────────────┴─────────────────────────┘
                                  │
                                  ▼
                    ┌──────────────────────────┐
                    │     Template Engine      │
                    │                          │
                    │  Scriban / Razor Light   │
                    │  Variable substitution   │
                    │  Conditional blocks      │
                    │  Loop constructs         │
                    └──────────────────────────┘
                                  │
                                  ▼
                    ┌──────────────────────────┐
                    │     Output Manager       │
                    │                          │
                    │  File writing            │
                    │  Directory creation      │
                    │  Conflict resolution     │
                    │  Formatting              │
                    └──────────────────────────┘
```

---

## Generated Project Structure

### Clean Architecture Style (Default)

```
MyRagApp/
├── MyRagApp.sln
├── Directory.Build.props
├── src/
│   └── MyRagApp.Api/
│       ├── MyRagApp.Api.csproj
│       ├── Program.cs
│       ├── Controllers/
│       │   ├── QueryController.cs
│       │   ├── IngestController.cs
│       │   └── HealthController.cs
│       ├── Services/
│       │   ├── RagQueryService.cs
│       │   ├── DocumentIngestService.cs
│       │   └── Interfaces/
│       │       ├── IRagQueryService.cs
│       │       └── IDocumentIngestService.cs
│       ├── Models/
│       │   ├── QueryRequest.cs
│       │   ├── QueryResponse.cs
│       │   ├── IngestRequest.cs
│       │   └── IngestResponse.cs
│       ├── Configuration/
│       │   ├── HazinaConfiguration.cs
│       │   ├── ProviderConfiguration.cs
│       │   └── ServiceCollectionExtensions.cs
│       ├── Middleware/
│       │   ├── ErrorHandlingMiddleware.cs
│       │   └── RateLimitMiddleware.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       ├── appsettings.Production.json
│       └── Properties/
│           └── launchSettings.json
├── tests/
│   └── MyRagApp.Api.Tests/
│       ├── MyRagApp.Api.Tests.csproj
│       └── Controllers/
│           └── QueryControllerTests.cs
├── Dockerfile
├── docker-compose.yml
├── .dockerignore
├── .gitignore
├── README.md
└── hazina-app.assembly.yaml     # Original spec (for reference)
```

### Minimal Style

```
MyRagApp/
├── MyRagApp.csproj
├── Program.cs
├── Endpoints/
│   ├── QueryEndpoints.cs
│   └── IngestEndpoints.cs
├── appsettings.json
├── Dockerfile
└── .gitignore
```

---

## Generator Components

### 1. Project Generator

Generates the project file and solution structure.

```csharp
// ProjectGenerator.cs
namespace Hazina.AI.Assembly.Generator;

public class ProjectGenerator
{
    private readonly TemplateEngine _templateEngine;

    public async Task GenerateAsync(
        AssemblySpec spec,
        string outputPath,
        GeneratorOptions options)
    {
        var projectName = spec.Output?.Project?.Name ?? spec.Metadata.Name;
        var projectPath = Path.Combine(outputPath, "src", $"{projectName}.Api");

        // Create directory structure
        CreateDirectoryStructure(projectPath, options.Style);

        // Generate .csproj file
        await GenerateProjectFile(spec, projectPath, projectName);

        // Generate Directory.Build.props
        await GenerateDirectoryBuildProps(outputPath);

        // Generate solution file
        await GenerateSolutionFile(spec, outputPath, projectName);
    }

    private async Task GenerateProjectFile(
        AssemblySpec spec,
        string projectPath,
        string projectName)
    {
        var nugetPackages = GetRequiredPackages(spec);

        var template = await _templateEngine.LoadAsync("csproj.scriban");
        var content = await template.RenderAsync(new
        {
            ProjectName = projectName,
            Sdk = spec.Output?.Project?.Sdk ?? "net9.0",
            Nullable = spec.Output?.Project?.Nullable ?? true,
            ImplicitUsings = spec.Output?.Project?.ImplicitUsings ?? true,
            Packages = nugetPackages
        });

        await File.WriteAllTextAsync(
            Path.Combine(projectPath, $"{projectName}.Api.csproj"),
            content);
    }

    private List<NuGetPackage> GetRequiredPackages(AssemblySpec spec)
    {
        var packages = new List<NuGetPackage>
        {
            new("Hazina.AI.FluentAPI", "2.0.0"),
        };

        // Add packages based on providers
        if (spec.Providers.Llm?.Primary?.Type == "llm.openai")
            packages.Add(new("Hazina.LLMs.OpenAI", "2.0.0"));

        if (spec.Providers.Llm?.Fallback?.Any(f => f.Type == "llm.anthropic") == true)
            packages.Add(new("Hazina.LLMs.Anthropic", "2.0.0"));

        // Add packages based on modules
        if (spec.Modules?.Any(m => m.Type == "module.rag-query") == true)
            packages.Add(new("Hazina.AI.RAG", "2.0.0"));

        // Add packages based on features
        if (spec.Features?.Observability?.Metrics?.Enabled == true)
            packages.Add(new("Hazina.Production.Monitoring", "2.0.0"));

        return packages;
    }
}
```

### 2. Service Generator

Generates controllers, services, and models.

```csharp
// ServiceGenerator.cs
namespace Hazina.AI.Assembly.Generator;

public class ServiceGenerator
{
    private readonly TemplateEngine _templateEngine;
    private readonly IComponentRegistry _registry;

    public async Task GenerateControllersAsync(
        AssemblySpec spec,
        string projectPath)
    {
        var controllersPath = Path.Combine(projectPath, "Controllers");
        Directory.CreateDirectory(controllersPath);

        foreach (var module in spec.Modules ?? [])
        {
            if (!module.Enabled) continue;

            var controller = await GenerateController(spec, module);
            var fileName = $"{GetControllerName(module.Type)}Controller.cs";

            await File.WriteAllTextAsync(
                Path.Combine(controllersPath, fileName),
                controller);
        }
    }

    private async Task<string> GenerateController(
        AssemblySpec spec,
        ModuleSpec module)
    {
        var template = module.Type switch
        {
            "module.rag-query" => await _templateEngine.LoadAsync("controllers/QueryController.scriban"),
            "module.document-ingest" => await _templateEngine.LoadAsync("controllers/IngestController.scriban"),
            "module.search" => await _templateEngine.LoadAsync("controllers/SearchController.scriban"),
            "module.chat" => await _templateEngine.LoadAsync("controllers/ChatController.scriban"),
            "module.health" => await _templateEngine.LoadAsync("controllers/HealthController.scriban"),
            _ => throw new UnsupportedModuleException(module.Type)
        };

        var config = module.Config ?? new Dictionary<string, object>();
        var endpoint = config.GetValueOrDefault("endpoint", GetDefaultEndpoint(module.Type));

        return await template.RenderAsync(new
        {
            Namespace = GetNamespace(spec),
            ControllerName = GetControllerName(module.Type),
            Endpoint = endpoint,
            Options = config,
            HasStreaming = config.GetValueOrDefault("streaming", false),
            HasCitations = config.GetValueOrDefault("citations", true),
            RequiresAuth = module.Config?.ContainsKey("auth") == true
        });
    }

    public async Task GenerateServicesAsync(
        AssemblySpec spec,
        string projectPath)
    {
        var servicesPath = Path.Combine(projectPath, "Services");
        var interfacesPath = Path.Combine(servicesPath, "Interfaces");
        Directory.CreateDirectory(interfacesPath);

        foreach (var module in spec.Modules ?? [])
        {
            if (!module.Enabled) continue;

            // Generate interface
            var interfaceContent = await GenerateServiceInterface(spec, module);
            await File.WriteAllTextAsync(
                Path.Combine(interfacesPath, $"I{GetServiceName(module.Type)}.cs"),
                interfaceContent);

            // Generate implementation
            var serviceContent = await GenerateServiceImplementation(spec, module);
            await File.WriteAllTextAsync(
                Path.Combine(servicesPath, $"{GetServiceName(module.Type)}.cs"),
                serviceContent);
        }
    }

    public async Task GenerateModelsAsync(
        AssemblySpec spec,
        string projectPath)
    {
        var modelsPath = Path.Combine(projectPath, "Models");
        Directory.CreateDirectory(modelsPath);

        // Generate common models
        await GenerateCommonModels(spec, modelsPath);

        // Generate module-specific models
        foreach (var module in spec.Modules ?? [])
        {
            await GenerateModuleModels(spec, module, modelsPath);
        }
    }
}
```

### 3. Configuration Generator

Generates startup configuration and settings files.

```csharp
// ConfigurationGenerator.cs
namespace Hazina.AI.Assembly.Generator;

public class ConfigurationGenerator
{
    private readonly TemplateEngine _templateEngine;
    private readonly IComponentRegistry _registry;

    public async Task GenerateProgramCsAsync(
        AssemblySpec spec,
        string projectPath)
    {
        var template = await _templateEngine.LoadAsync("Program.scriban");

        var content = await template.RenderAsync(new
        {
            Namespace = GetNamespace(spec),
            HasSwagger = spec.Features?.Output?.Documentation?.Swagger?.Enabled ?? true,
            HasCors = spec.Features?.Security?.Cors?.Enabled ?? false,
            HasAuth = spec.Features?.Auth != null,
            AuthType = spec.Features?.Auth?.GetValueOrDefault("type"),
            HasMetrics = spec.Features?.Observability?.Metrics?.Enabled ?? false,
            HasHealthChecks = spec.Modules?.Any(m => m.Type == "module.health") ?? false,
            Modules = spec.Modules?.Where(m => m.Enabled).ToList()
        });

        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "Program.cs"),
            content);
    }

    public async Task GenerateServiceExtensionsAsync(
        AssemblySpec spec,
        string projectPath)
    {
        var configPath = Path.Combine(projectPath, "Configuration");
        Directory.CreateDirectory(configPath);

        var template = await _templateEngine.LoadAsync("ServiceCollectionExtensions.scriban");

        // Build DI registration code for each provider
        var registrations = BuildRegistrations(spec);

        var content = await template.RenderAsync(new
        {
            Namespace = GetNamespace(spec),
            Registrations = registrations,
            Providers = spec.Providers,
            Pipelines = spec.Pipelines,
            Modules = spec.Modules
        });

        await File.WriteAllTextAsync(
            Path.Combine(configPath, "ServiceCollectionExtensions.cs"),
            content);
    }

    public async Task GenerateAppSettingsAsync(
        AssemblySpec spec,
        string projectPath)
    {
        // Generate base appsettings.json
        var settings = BuildAppSettings(spec);
        await File.WriteAllTextAsync(
            Path.Combine(projectPath, "appsettings.json"),
            JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            }));

        // Generate environment-specific files
        foreach (var env in spec.Output?.Settings?.Environments ?? ["Development", "Production"])
        {
            var envSettings = BuildEnvironmentSettings(spec, env);
            await File.WriteAllTextAsync(
                Path.Combine(projectPath, $"appsettings.{env}.json"),
                JsonSerializer.Serialize(envSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
        }
    }

    public async Task GenerateDockerfilesAsync(
        AssemblySpec spec,
        string outputPath)
    {
        if (spec.Output?.Docker?.Enabled != true)
            return;

        var projectName = spec.Output?.Project?.Name ?? spec.Metadata.Name;

        // Generate Dockerfile
        var dockerTemplate = await _templateEngine.LoadAsync("Dockerfile.scriban");
        var dockerfile = await dockerTemplate.RenderAsync(new
        {
            ProjectName = projectName,
            BaseImage = spec.Output?.Docker?.BaseImage ?? "mcr.microsoft.com/dotnet/aspnet:9.0",
            SdkImage = "mcr.microsoft.com/dotnet/sdk:9.0",
            Port = spec.Output?.Docker?.ExposePort ?? 8080
        });

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "Dockerfile"),
            dockerfile);

        // Generate docker-compose.yml
        var composeTemplate = await _templateEngine.LoadAsync("docker-compose.scriban");
        var compose = await composeTemplate.RenderAsync(new
        {
            ProjectName = projectName,
            Port = spec.Output?.Docker?.ExposePort ?? 8080,
            HasDatabase = RequiresDatabase(spec),
            HasRedis = RequiresRedis(spec)
        });

        await File.WriteAllTextAsync(
            Path.Combine(outputPath, "docker-compose.yml"),
            compose);
    }

    private Dictionary<string, object> BuildAppSettings(AssemblySpec spec)
    {
        var settings = new Dictionary<string, object>
        {
            ["Logging"] = new Dictionary<string, object>
            {
                ["LogLevel"] = new Dictionary<string, object>
                {
                    ["Default"] = spec.Features?.Observability?.Logging?.Level ?? "Information",
                    ["Microsoft.AspNetCore"] = "Warning"
                }
            }
        };

        // Add Hazina configuration section
        var hazinaSection = new Dictionary<string, object>();

        // Add provider placeholders (actual values come from env vars)
        if (spec.Providers.Llm?.Primary?.Type == "llm.openai")
        {
            hazinaSection["OpenAI"] = new Dictionary<string, object>
            {
                ["ApiKey"] = "${OPENAI_API_KEY}",
                ["Model"] = spec.Providers.Llm.Primary.Config?.GetValueOrDefault("model", "gpt-4o")
            };
        }

        settings["Hazina"] = hazinaSection;

        return settings;
    }
}
```

### 4. Template Engine

Uses Scriban for template processing.

```csharp
// TemplateEngine.cs
namespace Hazina.AI.Assembly.Generator;

public class TemplateEngine
{
    private readonly string _templatesPath;
    private readonly Dictionary<string, Template> _cache = new();

    public TemplateEngine(string templatesPath)
    {
        _templatesPath = templatesPath;
    }

    public async Task<Template> LoadAsync(string templateName)
    {
        if (_cache.TryGetValue(templateName, out var cached))
            return cached;

        // Try embedded resource first
        var assembly = typeof(TemplateEngine).Assembly;
        var resourceName = $"Hazina.AI.Assembly.Templates.{templateName.Replace('/', '.')}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        string content;

        if (stream != null)
        {
            using var reader = new StreamReader(stream);
            content = await reader.ReadToEndAsync();
        }
        else
        {
            // Fall back to file system
            var filePath = Path.Combine(_templatesPath, templateName);
            content = await File.ReadAllTextAsync(filePath);
        }

        var template = Template.Parse(content);
        _cache[templateName] = template;
        return template;
    }
}
```

---

## Code Templates

### Program.cs Template

```scriban
// Program.cs.scriban
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using {{ namespace }}.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Add Hazina services
builder.Services.AddHazinaServices(builder.Configuration);

{{ if has_swagger }}
// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "{{ project_name }} API", Version = "v1" });
});
{{ end }}

{{ if has_cors }}
// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
{{ end }}

{{ if has_auth }}
// Add Authentication
{{ if auth_type == "auth.jwt" }}
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["Jwt:Authority"];
        options.Audience = builder.Configuration["Jwt:Audience"];
    });
{{ else if auth_type == "auth.apikey" }}
builder.Services.AddAuthentication("ApiKey")
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", null);
{{ end }}
builder.Services.AddAuthorization();
{{ end }}

builder.Services.AddControllers();

{{ if has_health_checks }}
builder.Services.AddHealthChecks()
    .AddCheck<HazinaHealthCheck>("hazina");
{{ end }}

var app = builder.Build();

{{ if has_swagger }}
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
{{ end }}

app.UseHttpsRedirection();

{{ if has_cors }}
app.UseCors();
{{ end }}

{{ if has_auth }}
app.UseAuthentication();
app.UseAuthorization();
{{ end }}

app.MapControllers();

{{ if has_health_checks }}
app.MapHealthChecks("/health");
{{ end }}

{{ if has_metrics }}
app.MapPrometheusScrapingEndpoint("/metrics");
{{ end }}

app.Run();
```

### QueryController Template

```scriban
// controllers/QueryController.scriban
using Microsoft.AspNetCore.Mvc;
using {{ namespace }}.Models;
using {{ namespace }}.Services.Interfaces;
{{ if requires_auth }}
using Microsoft.AspNetCore.Authorization;
{{ end }}

namespace {{ namespace }}.Controllers;

[ApiController]
[Route("{{ endpoint | string.replace "/api/" "" }}")]
{{ if requires_auth }}
[Authorize]
{{ end }}
public class {{ controller_name }}Controller : ControllerBase
{
    private readonly IRagQueryService _queryService;
    private readonly ILogger<{{ controller_name }}Controller> _logger;

    public {{ controller_name }}Controller(
        IRagQueryService queryService,
        ILogger<{{ controller_name }}Controller> logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<QueryResponse>> Query(
        [FromBody] QueryRequest request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing query: {Query}", request.Query);

        try
        {
            var response = await _queryService.QueryAsync(request, cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing query");
            return StatusCode(500, new { error = "An error occurred processing your query" });
        }
    }

{{ if has_streaming }}
    [HttpPost("stream")]
    public async Task StreamQuery(
        [FromBody] QueryRequest request,
        CancellationToken cancellationToken)
    {
        Response.ContentType = "text/event-stream";

        await foreach (var chunk in _queryService.StreamQueryAsync(request, cancellationToken))
        {
            await Response.WriteAsync($"data: {chunk}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
{{ end }}
}
```

### ServiceCollectionExtensions Template

```scriban
// ServiceCollectionExtensions.scriban
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Hazina.AI.FluentAPI.Configuration;
using Hazina.AI.Providers;
{{ for registration in registrations }}
using {{ registration.namespace }};
{{ end }}

namespace {{ namespace }}.Configuration;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHazinaServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure provider orchestrator
        var orchestrator = ProviderOrchestrator.Create();

{{ if providers.llm.primary }}
        // Add primary LLM provider
        orchestrator.RegisterProvider("{{ providers.llm.primary.type }}", new {{ providers.llm.primary.type | to_class_name }}Config
        {
            ApiKey = configuration["Hazina:{{ providers.llm.primary.type | to_config_key }}:ApiKey"]
                ?? Environment.GetEnvironmentVariable("{{ providers.llm.primary.type | to_env_var }}_API_KEY"),
            Model = configuration["Hazina:{{ providers.llm.primary.type | to_config_key }}:Model"]
                ?? "{{ providers.llm.primary.config.model | default: "gpt-4o" }}"
        });
{{ end }}

{{ if providers.llm.fallback }}
{{ for fallback in providers.llm.fallback }}
        // Add fallback provider: {{ fallback.type }}
        orchestrator.RegisterProvider("{{ fallback.type }}", new {{ fallback.type | to_class_name }}Config
        {
            ApiKey = configuration["Hazina:{{ fallback.type | to_config_key }}:ApiKey"]
                ?? Environment.GetEnvironmentVariable("{{ fallback.type | to_env_var }}_API_KEY"),
            Model = "{{ fallback.config.model }}"
        });
{{ end }}
{{ end }}

        services.AddSingleton<IProviderOrchestrator>(orchestrator);

{{ if providers.embedding }}
        // Configure embedding generator
        services.AddSingleton<IEmbeddingGenerator>(sp =>
        {
            var orchestrator = sp.GetRequiredService<IProviderOrchestrator>();
            return new OpenAIEmbeddingGenerator(
                configuration["Hazina:OpenAI:ApiKey"]!,
                "{{ providers.embedding.config.model | default: "text-embedding-3-small" }}");
        });
{{ end }}

{{ if providers.storage.vectors }}
        // Configure vector store
{{ if providers.storage.vectors.type == "vector.memory" }}
        services.AddSingleton<IEmbeddingStore, InMemoryVectorStore>();
{{ else if providers.storage.vectors.type == "vector.supabase" }}
        services.AddSingleton<IEmbeddingStore>(sp =>
            new SupabaseEmbeddingStore(
                configuration["Hazina:Supabase:Url"]!,
                configuration["Hazina:Supabase:Key"]!));
{{ else if providers.storage.vectors.type == "vector.pgvector" }}
        services.AddSingleton<IEmbeddingStore>(sp =>
            new PgVectorStore(configuration.GetConnectionString("Postgres")!));
{{ end }}
{{ end }}

{{ if providers.storage.documents }}
        // Configure document store
{{ if providers.storage.documents.type == "storage.local" }}
        services.AddSingleton<IDocumentStore>(sp =>
            new FileDocumentStore("{{ providers.storage.documents.config.rootPath | default: "./data/documents" }}"));
{{ end }}
{{ end }}

        // Register module services
{{ for module in modules }}
{{ if module.enabled }}
{{ if module.type == "module.rag-query" }}
        services.AddScoped<IRagQueryService, RagQueryService>();
{{ else if module.type == "module.document-ingest" }}
        services.AddScoped<IDocumentIngestService, DocumentIngestService>();
{{ else if module.type == "module.search" }}
        services.AddScoped<ISearchService, SearchService>();
{{ end }}
{{ end }}
{{ end }}

        return services;
    }
}
```

---

## CLI Integration

```csharp
// AssembleCommand.cs
namespace Hazina.CLI.Commands;

[Command("assemble", Description = "Generate project from assembly specification")]
public class AssembleCommand : ICommand
{
    [CommandArgument(0, "<spec>")]
    public string SpecPath { get; set; } = "";

    [CommandOption("-o|--output")]
    public string OutputPath { get; set; } = ".";

    [CommandOption("--style")]
    public ProjectStyle Style { get; set; } = ProjectStyle.Clean;

    [CommandOption("--no-docker")]
    public bool NoDocker { get; set; }

    [CommandOption("--dry-run")]
    public bool DryRun { get; set; }

    public async Task<int> ExecuteAsync(IConsole console)
    {
        console.WriteLine($"Parsing specification: {SpecPath}");

        var parser = new SpecificationParser();
        var spec = await parser.ParseAsync(SpecPath);

        console.WriteLine($"Generating project: {spec.Metadata.Name}");

        if (DryRun)
        {
            console.WriteLine("[Dry Run] Would generate:");
            // Show what would be generated
            return 0;
        }

        var generator = new ScaffoldGenerator(new GeneratorOptions
        {
            Style = Style,
            IncludeDocker = !NoDocker
        });

        await generator.GenerateAsync(spec, OutputPath);

        console.WriteLine($"Project generated at: {OutputPath}");
        console.WriteLine();
        console.WriteLine("Next steps:");
        console.WriteLine("  cd " + Path.GetFileName(OutputPath));
        console.WriteLine("  dotnet restore");
        console.WriteLine("  dotnet run");

        return 0;
    }
}
```

---

## Implementation Tasks

### Week 1: Core Generator
- [ ] Create ScaffoldGenerator main class
- [ ] Implement TemplateEngine with Scriban
- [ ] Create project structure generation
- [ ] Generate basic .csproj file

### Week 2: Code Generation
- [ ] Implement controller templates
- [ ] Implement service templates
- [ ] Implement model templates
- [ ] Generate Program.cs and startup

### Week 3: Configuration
- [ ] Generate appsettings.json
- [ ] Generate ServiceCollectionExtensions
- [ ] Docker file generation
- [ ] Add NuGet package resolution

### Week 4: Integration
- [ ] CLI command implementation
- [ ] End-to-end tests
- [ ] Error handling and validation
- [ ] Documentation

---

## Success Criteria

- [ ] Generate complete project from spec in < 5 seconds
- [ ] Generated projects compile without errors
- [ ] Generated projects run and serve requests
- [ ] All providers correctly configured in DI
- [ ] Docker builds and runs successfully
- [ ] Unit test coverage > 80%

---

**Next Document:** [04-VS_TEMPLATE.md](./04-VS_TEMPLATE.md)
