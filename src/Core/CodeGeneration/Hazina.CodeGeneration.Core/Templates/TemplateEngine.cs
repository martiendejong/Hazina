using Hazina.CodeGeneration.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Hazina.CodeGeneration.Core.Templates;

/// <summary>
/// Template engine for generating code from intents
/// </summary>
public class TemplateEngine : ITemplateEngine
{
    private readonly ILogger<TemplateEngine> _logger;
    private readonly List<ICodeTemplate> _templates = new();

    public TemplateEngine(ILogger<TemplateEngine> logger)
    {
        _logger = logger;

        // Register built-in templates
        RegisterBuiltInTemplates();
    }

    /// <inheritdoc/>
    public void RegisterTemplate(ICodeTemplate template)
    {
        _logger.LogInformation("[TEMPLATE ENGINE] Registering template for intent type: {IntentType}",
            template.SupportedIntentType);
        _templates.Add(template);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateCodeAsync(
        CodeGenerationIntent intent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[TEMPLATE ENGINE] Generating code for intent type: {IntentType}", intent.Type);

        // Find appropriate template
        var template = _templates.FirstOrDefault(t => t.CanHandle(intent));

        if (template == null)
        {
            _logger.LogWarning("[TEMPLATE ENGINE] No template found for intent type: {IntentType}", intent.Type);
            throw new InvalidOperationException($"No template registered for intent type: {intent.Type}");
        }

        // Generate code using template
        var code = await template.GenerateAsync(intent, cancellationToken);

        // Format the generated code
        var formattedCode = FormatCode(code);

        _logger.LogInformation("[TEMPLATE ENGINE] Code generation complete. Length: {Length} characters",
            formattedCode.Length);

        return formattedCode;
    }

    /// <inheritdoc/>
    public string FormatCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return code;

        // Basic code formatting
        var lines = code.Split('\n');
        var formatted = new StringBuilder();
        var indentLevel = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Decrease indent for closing braces
            if (trimmed.StartsWith('}'))
            {
                indentLevel = Math.Max(0, indentLevel - 1);
            }

            // Add line with proper indentation
            if (!string.IsNullOrWhiteSpace(trimmed))
            {
                formatted.AppendLine(new string(' ', indentLevel * 4) + trimmed);
            }
            else
            {
                formatted.AppendLine();
            }

            // Increase indent for opening braces
            if (trimmed.EndsWith('{'))
            {
                indentLevel++;
            }
        }

        return formatted.ToString().TrimEnd();
    }

    private void RegisterBuiltInTemplates()
    {
        // Register method template
        RegisterTemplate(new MethodTemplate(_logger));

        // Register class template
        RegisterTemplate(new ClassTemplate(_logger));

        // Register test template
        RegisterTemplate(new TestTemplate(_logger));
    }
}
