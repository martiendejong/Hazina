using Hazina.CodeGeneration.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Hazina.CodeGeneration.Core.Templates;

/// <summary>
/// Template for generating classes
/// </summary>
public class ClassTemplate : ICodeTemplate
{
    private readonly ILogger _logger;
    private readonly MethodTemplate _methodTemplate;

    public IntentType SupportedIntentType => IntentType.GenerateClass;

    public ClassTemplate(ILogger logger)
    {
        _logger = logger;
        _methodTemplate = new MethodTemplate(logger);
    }

    public bool CanHandle(CodeGenerationIntent intent)
    {
        return intent is ClassGenerationIntent;
    }

    public async Task<string> GenerateAsync(
        CodeGenerationIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (intent is not ClassGenerationIntent classIntent)
        {
            throw new ArgumentException("Intent must be ClassGenerationIntent", nameof(intent));
        }

        var code = new StringBuilder();

        // Add namespace if provided
        if (!string.IsNullOrWhiteSpace(classIntent.TargetNamespace))
        {
            code.AppendLine($"namespace {classIntent.TargetNamespace};");
            code.AppendLine();
        }

        // Add XML documentation
        code.AppendLine("/// <summary>");
        code.AppendLine($"/// {classIntent.Description}");
        code.AppendLine("/// </summary>");

        // Build class signature
        var signature = new StringBuilder();
        signature.Append(classIntent.AccessModifier);
        signature.Append(' ');

        if (classIntent.IsStatic)
        {
            signature.Append("static ");
        }
        else if (classIntent.IsAbstract)
        {
            signature.Append("abstract ");
        }
        else if (classIntent.IsSealed)
        {
            signature.Append("sealed ");
        }

        signature.Append("class ");
        signature.Append(classIntent.ClassName);

        // Add inheritance and interfaces
        var inheritance = new List<string>();
        if (!string.IsNullOrWhiteSpace(classIntent.BaseClass))
        {
            inheritance.Add(classIntent.BaseClass);
        }
        inheritance.AddRange(classIntent.Interfaces);

        if (inheritance.Any())
        {
            signature.Append(" : ");
            signature.Append(string.Join(", ", inheritance));
        }

        code.AppendLine(signature.ToString());
        code.AppendLine("{");

        // Add properties
        foreach (var property in classIntent.Properties)
        {
            code.AppendLine(GenerateProperty(property));
        }

        // Add empty line if there are properties
        if (classIntent.Properties.Any())
        {
            code.AppendLine();
        }

        // Add constructor if requested
        if (classIntent.GenerateConstructor && !classIntent.IsStatic && classIntent.Properties.Any())
        {
            code.AppendLine(GenerateConstructor(classIntent));
            code.AppendLine();
        }

        // Add methods
        for (int i = 0; i < classIntent.Methods.Count; i++)
        {
            var method = classIntent.Methods[i];
            var methodCode = await _methodTemplate.GenerateAsync(method, cancellationToken);

            // Add indentation to method code
            var lines = methodCode.Split('\n');
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    code.AppendLine("    " + line.TrimEnd());
                }
            }

            // Add empty line between methods
            if (i < classIntent.Methods.Count - 1)
            {
                code.AppendLine();
            }
        }

        code.AppendLine("}");

        return code.ToString();
    }

    private string GenerateProperty(ClassProperty property)
    {
        var code = new StringBuilder();

        // Add XML documentation
        if (!string.IsNullOrWhiteSpace(property.Description))
        {
            code.AppendLine("    /// <summary>");
            code.AppendLine($"    /// {property.Description}");
            code.AppendLine("    /// </summary>");
        }

        // Build property
        code.Append("    ");
        code.Append(property.AccessModifier);
        code.Append(' ');

        if (property.IsRequired)
        {
            code.Append("required ");
        }

        code.Append(property.Type);
        code.Append(' ');
        code.Append(property.Name);
        code.Append(" { ");

        if (property.HasGetter)
        {
            code.Append("get; ");
        }

        if (property.HasSetter)
        {
            code.Append("set; ");
        }

        code.Append('}');

        if (!string.IsNullOrWhiteSpace(property.DefaultValue))
        {
            code.Append(" = ");
            code.Append(property.DefaultValue);
            code.Append(';');
        }

        return code.ToString();
    }

    private string GenerateConstructor(ClassGenerationIntent classIntent)
    {
        var code = new StringBuilder();

        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Initializes a new instance of the <see cref=\"{classIntent.ClassName}\"/> class");
        code.AppendLine("    /// </summary>");

        code.Append("    public ");
        code.Append(classIntent.ClassName);
        code.AppendLine("()");
        code.AppendLine("    {");

        // Initialize properties with default values
        foreach (var property in classIntent.Properties.Where(p => !string.IsNullOrWhiteSpace(p.DefaultValue)))
        {
            code.AppendLine($"        {property.Name} = {property.DefaultValue};");
        }

        code.AppendLine("    }");

        return code.ToString();
    }
}
