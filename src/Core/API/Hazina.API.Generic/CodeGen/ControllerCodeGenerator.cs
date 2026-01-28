using Hazina.API.Generic.Configuration;
using System.Text;

namespace Hazina.API.Generic.CodeGen;

/// <summary>
/// Generates ASP.NET Core controllers from YAML definitions.
/// Controllers inherit from GenericEntityController for full CRUD.
/// </summary>
public class ControllerCodeGenerator
{
    private readonly string _controllersNamespace;
    private readonly string _entitiesNamespace;

    public ControllerCodeGenerator(
        string controllersNamespace = "Generated.Controllers",
        string entitiesNamespace = "Generated.Entities")
    {
        _controllersNamespace = controllersNamespace;
        _entitiesNamespace = entitiesNamespace;
    }

    /// <summary>
    /// Generate a minimal controller that inherits all CRUD from GenericEntityController.
    /// </summary>
    public string GenerateMinimalController(EntityDefinition definition)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using Hazina.API.Generic.Controllers;");
        sb.AppendLine("using Hazina.API.Generic.Entities;");
        sb.AppendLine($"using {_entitiesNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_controllersNamespace};");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(definition.Description))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// Controller for {definition.Name} entities.");
            sb.AppendLine($"/// {EscapeXml(definition.Description)}");
            sb.AppendLine("/// </summary>");
        }

        // Minimal 1-line controller
        sb.AppendLine($"public class {definition.GetPluralName()}Controller : GenericEntityController<{definition.Name}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public {definition.GetPluralName()}Controller(IRepository<{definition.Name}> repository) : base(repository) {{ }}");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate a controller with additional custom endpoints based on field types.
    /// </summary>
    public string GenerateExtendedController(EntityDefinition definition)
    {
        var sb = new StringBuilder();

        sb.AppendLine("using Hazina.API.Generic.Controllers;");
        sb.AppendLine("using Hazina.API.Generic.Entities;");
        sb.AppendLine("using Microsoft.AspNetCore.Mvc;");
        sb.AppendLine($"using {_entitiesNamespace};");
        sb.AppendLine();
        sb.AppendLine($"namespace {_controllersNamespace};");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// Controller for {definition.Name} entities with custom endpoints.");
        if (!string.IsNullOrEmpty(definition.Description))
        {
            sb.AppendLine($"/// {EscapeXml(definition.Description)}");
        }
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"[Route(\"api/{definition.GetPluralName().ToLowerInvariant()}\")]");
        sb.AppendLine($"public class {definition.GetPluralName()}Controller : GenericEntityController<{definition.Name}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public {definition.GetPluralName()}Controller(IRepository<{definition.Name}> repository) : base(repository) {{ }}");

        // Generate custom endpoints based on searchable fields
        var searchableFields = definition.Fields.Where(f => f.Searchable).ToList();
        foreach (var field in searchableFields)
        {
            GenerateSearchByFieldEndpoint(sb, definition, field);
        }

        // Generate custom endpoints based on enum fields
        var enumFields = definition.Fields.Where(f => f.Type == FieldType.Enum).ToList();
        foreach (var field in enumFields)
        {
            GenerateFilterByEnumEndpoint(sb, definition, field);
        }

        // Generate custom endpoints based on boolean fields
        var boolFields = definition.Fields.Where(f => f.Type == FieldType.Bool).ToList();
        foreach (var field in boolFields)
        {
            GenerateFilterByBoolEndpoint(sb, definition, field);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    private void GenerateSearchByFieldEndpoint(StringBuilder sb, EntityDefinition definition, FieldDefinition field)
    {
        var pascalName = ToPascalCase(field.Name);
        var camelName = ToCamelCase(field.Name);

        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Search {definition.GetPluralName().ToLowerInvariant()} by {field.Name}.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    [HttpGet(\"by-{field.Name.ToLowerInvariant()}\")]");
        sb.AppendLine($"    public async Task<ActionResult<List<{definition.Name}>>> GetBy{pascalName}([FromQuery] string {camelName})");
        sb.AppendLine("    {");
        sb.AppendLine($"        var results = await Repository.Query()");
        sb.AppendLine($"            .Where(e => e.{pascalName} != null && e.{pascalName}.Contains({camelName}))");
        sb.AppendLine($"            .Take(20)");
        sb.AppendLine($"            .ToListAsync();");
        sb.AppendLine($"        return Ok(results);");
        sb.AppendLine("    }");
    }

    private void GenerateFilterByEnumEndpoint(StringBuilder sb, EntityDefinition definition, FieldDefinition field)
    {
        var pascalName = ToPascalCase(field.Name);
        var camelName = ToCamelCase(field.Name);

        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Get {definition.GetPluralName().ToLowerInvariant()} by {field.Name}.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    [HttpGet(\"by-{field.Name.ToLowerInvariant()}/{{value}}\")]");
        sb.AppendLine($"    public async Task<ActionResult<List<{definition.Name}>>> GetBy{pascalName}(string value)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var results = await Repository.Query()");
        sb.AppendLine($"            .Where(e => e.{pascalName} == value)");
        sb.AppendLine($"            .ToListAsync();");
        sb.AppendLine($"        return Ok(results);");
        sb.AppendLine("    }");
    }

    private void GenerateFilterByBoolEndpoint(StringBuilder sb, EntityDefinition definition, FieldDefinition field)
    {
        var pascalName = ToPascalCase(field.Name);

        sb.AppendLine();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Get {definition.GetPluralName().ToLowerInvariant()} where {field.Name} is true.");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    [HttpGet(\"{field.Name.ToLowerInvariant()}\")]");
        sb.AppendLine($"    public async Task<ActionResult<List<{definition.Name}>>> Get{pascalName}()");
        sb.AppendLine("    {");
        sb.AppendLine($"        var results = await Repository.Query()");
        sb.AppendLine($"            .Where(e => e.{pascalName})");
        sb.AppendLine($"            .ToListAsync();");
        sb.AppendLine($"        return Ok(results);");
        sb.AppendLine("    }");
    }

    /// <summary>
    /// Generate all controllers from a list of definitions.
    /// </summary>
    public Dictionary<string, string> GenerateAllControllers(
        IEnumerable<EntityDefinition> definitions,
        bool extended = false)
    {
        return definitions.ToDictionary(
            d => $"{d.GetPluralName()}Controller.cs",
            d => extended ? GenerateExtendedController(d) : GenerateMinimalController(d));
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name.Substring(1);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
