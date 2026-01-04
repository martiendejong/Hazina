using Hazina.CodeGeneration.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Hazina.CodeGeneration.Core.Templates;

/// <summary>
/// Template for generating methods
/// </summary>
public class MethodTemplate : ICodeTemplate
{
    private readonly ILogger _logger;

    public IntentType SupportedIntentType => IntentType.GenerateMethod;

    public MethodTemplate(ILogger logger)
    {
        _logger = logger;
    }

    public bool CanHandle(CodeGenerationIntent intent)
    {
        return intent is MethodGenerationIntent;
    }

    public Task<string> GenerateAsync(
        CodeGenerationIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (intent is not MethodGenerationIntent methodIntent)
        {
            throw new ArgumentException("Intent must be MethodGenerationIntent", nameof(intent));
        }

        var code = new StringBuilder();

        // Add XML documentation
        code.AppendLine("/// <summary>");
        code.AppendLine($"/// {methodIntent.Description}");
        code.AppendLine("/// </summary>");

        // Add parameter documentation
        foreach (var param in methodIntent.Parameters)
        {
            code.AppendLine($"/// <param name=\"{param.Name}\">{param.Description ?? param.Name}</param>");
        }

        // Add return documentation if not void
        if (methodIntent.ReturnType != "void" && !methodIntent.ReturnType.StartsWith("Task"))
        {
            code.AppendLine($"/// <returns>{methodIntent.Description}</returns>");
        }

        // Build method signature
        var signature = new StringBuilder();
        signature.Append(methodIntent.AccessModifier);
        signature.Append(' ');

        if (methodIntent.IsStatic)
        {
            signature.Append("static ");
        }

        if (methodIntent.IsAsync && !methodIntent.ReturnType.StartsWith("Task"))
        {
            signature.Append("async ");
        }

        signature.Append(methodIntent.ReturnType);
        signature.Append(' ');
        signature.Append(methodIntent.MethodName);
        signature.Append('(');

        // Add parameters
        var parameters = methodIntent.Parameters.Select(p =>
        {
            var param = new StringBuilder();
            param.Append(p.Type);
            param.Append(' ');
            param.Append(p.Name);

            if (p.IsOptional && p.DefaultValue != null)
            {
                param.Append(" = ");
                param.Append(p.DefaultValue);
            }

            return param.ToString();
        });

        signature.Append(string.Join(", ", parameters));
        signature.Append(')');

        code.AppendLine(signature.ToString());
        code.AppendLine("{");

        // Add method body
        if (methodIntent.ReturnType != "void" && !methodIntent.ReturnType.Contains("Task"))
        {
            code.AppendLine($"    throw new NotImplementedException();");
        }
        else if (methodIntent.IsAsync)
        {
            code.AppendLine($"    await Task.CompletedTask;");
            code.AppendLine($"    throw new NotImplementedException();");
        }
        else
        {
            code.AppendLine($"    throw new NotImplementedException();");
        }

        code.AppendLine("}");

        return Task.FromResult(code.ToString());
    }
}
