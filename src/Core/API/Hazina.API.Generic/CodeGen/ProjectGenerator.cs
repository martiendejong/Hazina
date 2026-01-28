using Hazina.API.Generic.Configuration;
using System.Text;

namespace Hazina.API.Generic.CodeGen;

/// <summary>
/// Generates a complete ASP.NET Core project from YAML entity definitions.
/// Creates a ready-to-run API project with entities, controllers, and configuration.
/// </summary>
public class ProjectGenerator
{
    private readonly EntityCodeGenerator _entityGenerator;
    private readonly ControllerCodeGenerator _controllerGenerator;
    private readonly string _projectName;
    private readonly string _namespace;

    public ProjectGenerator(string projectName, string? @namespace = null)
    {
        _projectName = projectName;
        _namespace = @namespace ?? projectName;
        _entityGenerator = new EntityCodeGenerator($"{_namespace}.Entities");
        _controllerGenerator = new ControllerCodeGenerator($"{_namespace}.Controllers", $"{_namespace}.Entities");
    }

    /// <summary>
    /// Generate all project files.
    /// </summary>
    public Dictionary<string, string> GenerateProject(IEnumerable<EntityDefinition> definitions, bool extendedControllers = false)
    {
        var files = new Dictionary<string, string>();

        // Generate .csproj
        files[$"{_projectName}.csproj"] = GenerateCsproj();

        // Generate Program.cs
        files["Program.cs"] = GenerateProgram(definitions);

        // Generate appsettings.json
        files["appsettings.json"] = GenerateAppSettings();

        // Generate entity classes
        foreach (var (filename, content) in _entityGenerator.GenerateAllEntityClasses(definitions))
        {
            files[$"Entities/{filename}"] = content;
        }

        // Generate controller classes
        foreach (var (filename, content) in _controllerGenerator.GenerateAllControllers(definitions, extendedControllers))
        {
            files[$"Controllers/{filename}"] = content;
        }

        // Generate DbContext
        files["Data/AppDbContext.cs"] = GenerateDbContext(definitions);

        return files;
    }

    private string GenerateCsproj()
    {
        return $@"<Project Sdk=""Microsoft.NET.Sdk.Web"">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include=""Hazina.API.Generic"" Version=""*"" />
    <PackageReference Include=""Microsoft.EntityFrameworkCore.Sqlite"" Version=""9.0.0"" />
    <PackageReference Include=""Swashbuckle.AspNetCore"" Version=""6.5.0"" />
  </ItemGroup>

</Project>
";
    }

    private string GenerateProgram(IEnumerable<EntityDefinition> definitions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using Hazina.API.Generic;");
        sb.AppendLine("using Hazina.API.Generic.Extensions;");
        sb.AppendLine($"using {_namespace}.Data;");
        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine();
        sb.AppendLine("var builder = WebApplication.CreateBuilder(args);");
        sb.AppendLine();
        sb.AppendLine("// Add services");
        sb.AppendLine("builder.Services.AddControllers();");
        sb.AppendLine("builder.Services.AddEndpointsApiExplorer();");
        sb.AppendLine("builder.Services.AddSwaggerGen();");
        sb.AppendLine();
        sb.AppendLine("// Add database");
        sb.AppendLine("builder.Services.AddDbContext<AppDbContext>(options =>");
        sb.AppendLine("    options.UseSqlite(\"Data Source=app.db\"));");
        sb.AppendLine();
        sb.AppendLine("// Register repositories for each entity");

        foreach (var definition in definitions)
        {
            sb.AppendLine($"builder.Services.AddHazinaEntity<{_namespace}.Entities.{definition.Name}, AppDbContext>();");
        }

        sb.AppendLine();
        sb.AppendLine("var app = builder.Build();");
        sb.AppendLine();
        sb.AppendLine("// Ensure database is created");
        sb.AppendLine("using (var scope = app.Services.CreateScope())");
        sb.AppendLine("{");
        sb.AppendLine("    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();");
        sb.AppendLine("    db.Database.EnsureCreated();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("// Configure pipeline");
        sb.AppendLine("if (app.Environment.IsDevelopment())");
        sb.AppendLine("{");
        sb.AppendLine("    app.UseSwagger();");
        sb.AppendLine("    app.UseSwaggerUI();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("app.UseHttpsRedirection();");
        sb.AppendLine("app.UseAuthorization();");
        sb.AppendLine("app.MapControllers();");
        sb.AppendLine();
        sb.AppendLine("app.Run();");

        return sb.ToString();
    }

    private string GenerateAppSettings()
    {
        return @"{
  ""Logging"": {
    ""LogLevel"": {
      ""Default"": ""Information"",
      ""Microsoft.AspNetCore"": ""Warning""
    }
  },
  ""AllowedHosts"": ""*""
}
";
    }

    private string GenerateDbContext(IEnumerable<EntityDefinition> definitions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using Microsoft.EntityFrameworkCore;");
        sb.AppendLine($"using {_namespace}.Entities;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_namespace}.Data;");
        sb.AppendLine();
        sb.AppendLine("public class AppDbContext : DbContext");
        sb.AppendLine("{");
        sb.AppendLine("    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }");
        sb.AppendLine();

        foreach (var definition in definitions)
        {
            sb.AppendLine($"    public DbSet<{definition.Name}> {definition.GetPluralName()} {{ get; set; }}");
        }

        sb.AppendLine();
        sb.AppendLine("    protected override void OnModelCreating(ModelBuilder modelBuilder)");
        sb.AppendLine("    {");
        sb.AppendLine("        base.OnModelCreating(modelBuilder);");
        sb.AppendLine();

        foreach (var definition in definitions)
        {
            sb.AppendLine($"        modelBuilder.Entity<{definition.Name}>(entity =>");
            sb.AppendLine("        {");
            sb.AppendLine($"            entity.ToTable(\"{definition.GetPluralName()}\");");
            sb.AppendLine("            entity.HasKey(e => e.Id);");

            // Add indexes for searchable fields
            var searchableFields = definition.Fields.Where(f => f.Searchable && !IsSystemField(f.Name)).ToList();
            foreach (var field in searchableFields)
            {
                sb.AppendLine($"            entity.HasIndex(e => e.{ToPascalCase(field.Name)});");
            }

            sb.AppendLine("        });");
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static bool IsSystemField(string name)
    {
        var systemFields = new[]
        {
            "id", "createdAt", "updatedAt", "isDeleted", "deletedAt",
            "embeddingText", "embedding", "tenantId", "ownerId"
        };
        return systemFields.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }
}
