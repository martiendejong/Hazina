using Hazina.API.Generic.Configuration;
using System.Text;

namespace Hazina.API.Generic.CodeGen;

/// <summary>
/// Generates C# entity classes from YAML definitions.
/// Produces strongly-typed entities that inherit from EntityBase.
/// </summary>
public class EntityCodeGenerator
{
    private readonly string _namespace;

    public EntityCodeGenerator(string @namespace = "Generated.Entities")
    {
        _namespace = @namespace;
    }

    /// <summary>
    /// Generate a complete C# entity class file.
    /// </summary>
    public string GenerateEntityClass(EntityDefinition definition)
    {
        var sb = new StringBuilder();

        // Using statements
        sb.AppendLine("using Hazina.API.Generic.Entities;");
        sb.AppendLine("using System.ComponentModel.DataAnnotations;");
        sb.AppendLine();

        // Namespace
        sb.AppendLine($"namespace {_namespace};");
        sb.AppendLine();

        // XML documentation
        if (!string.IsNullOrEmpty(definition.Description))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {EscapeXml(definition.Description)}");
            sb.AppendLine("/// </summary>");
        }

        // Determine base class
        var baseClass = GetBaseClass(definition);
        sb.AppendLine($"public class {definition.Name} : {baseClass}");
        sb.AppendLine("{");

        // Generate properties for each field
        foreach (var field in definition.Fields)
        {
            GenerateProperty(sb, field);
        }

        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generate all entity classes from a list of definitions.
    /// </summary>
    public Dictionary<string, string> GenerateAllEntityClasses(IEnumerable<EntityDefinition> definitions)
    {
        return definitions.ToDictionary(
            d => $"{d.Name}.cs",
            d => GenerateEntityClass(d));
    }

    private string GetBaseClass(EntityDefinition definition)
    {
        // Choose base class based on features
        if (definition.Features.Embedding)
            return "EmbeddableEntityBase";
        if (definition.Features.SoftDelete)
            return "SoftDeleteEntityBase";
        return "EntityBase";
    }

    private void GenerateProperty(StringBuilder sb, FieldDefinition field)
    {
        // Skip system fields (handled by base class)
        if (IsSystemField(field.Name))
            return;

        sb.AppendLine();

        // XML documentation
        if (!string.IsNullOrEmpty(field.Description))
        {
            sb.AppendLine($"    /// <summary>");
            sb.AppendLine($"    /// {EscapeXml(field.Description)}");
            sb.AppendLine($"    /// </summary>");
        }

        // Validation attributes
        if (field.Required)
        {
            sb.AppendLine("    [Required]");
        }
        if (field.MaxLength.HasValue)
        {
            sb.AppendLine($"    [MaxLength({field.MaxLength.Value})]");
        }

        // Property declaration
        var csharpType = GetCSharpType(field);
        var nullable = !field.Required && IsNullableType(field.Type) ? "?" : "";
        sb.AppendLine($"    public {csharpType}{nullable} {ToPascalCase(field.Name)} {{ get; set; }}");
    }

    private static string GetCSharpType(FieldDefinition field)
    {
        return field.Type switch
        {
            FieldType.String => "string",
            FieldType.Text => "string",
            FieldType.Int => "int",
            FieldType.Long => "long",
            FieldType.Decimal => "decimal",
            FieldType.Double => "double",
            FieldType.Bool => "bool",
            FieldType.DateTime => "DateTime",
            FieldType.DateOnly => "DateOnly",
            FieldType.TimeOnly => "TimeOnly",
            FieldType.Guid => "Guid",
            FieldType.Json => "string", // Store as JSON string
            FieldType.Enum => "string", // Stored as string
            FieldType.Reference => "Guid", // Foreign key
            FieldType.List => "string", // Stored as JSON
            FieldType.Binary => "byte[]",
            _ => "string"
        };
    }

    private static bool IsNullableType(FieldType type)
    {
        return type switch
        {
            FieldType.String => true,
            FieldType.Text => true,
            FieldType.Json => true,
            FieldType.List => true,
            FieldType.Binary => true,
            _ => false
        };
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

    private static string EscapeXml(string text)
    {
        return text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}
