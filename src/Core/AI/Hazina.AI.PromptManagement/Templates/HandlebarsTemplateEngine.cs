using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HandlebarsDotNet;
using Hazina.AI.PromptManagement.Core;

namespace Hazina.AI.PromptManagement.Templates;

/// <summary>
/// Handlebars template engine implementation
/// </summary>
public class HandlebarsTemplateEngine : ITemplateEngine
{
    private static readonly IHandlebars _handlebars = Handlebars.Create();

    public string Name => "Handlebars";

    public Task<string> RenderAsync(string template, Dictionary<string, object> variables)
    {
        try
        {
            var compiledTemplate = _handlebars.Compile(template);
            var result = compiledTemplate(variables);
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to render Handlebars template: {ex.Message}", ex);
        }
    }

    public Task<bool> ValidateAsync(string template)
    {
        try
        {
            _handlebars.Compile(template);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<List<string>> ExtractVariablesAsync(string template)
    {
        // Regex to match Handlebars variables: {{variableName}} or {{#if variableName}}
        var regex = new Regex(@"\{\{[#/]?([a-zA-Z_][a-zA-Z0-9_\.]*)", RegexOptions.Compiled);
        var matches = regex.Matches(template);

        var variables = matches
            .Cast<Match>()
            .Select(m => m.Groups[1].Value.Split('.').First()) // Get root variable name
            .Where(v => !IsHandlebarsKeyword(v))
            .Distinct()
            .ToList();

        return Task.FromResult(variables);
    }

    private static bool IsHandlebarsKeyword(string name)
    {
        var keywords = new HashSet<string> { "if", "unless", "each", "with", "this", "else" };
        return keywords.Contains(name.ToLowerInvariant());
    }
}

/// <summary>
/// Template engine factory implementation
/// </summary>
public class TemplateEngineFactory : ITemplateEngineFactory
{
    private readonly Dictionary<string, ITemplateEngine> _engines = new();

    public TemplateEngineFactory()
    {
        // Register default engines
        RegisterEngine(new HandlebarsTemplateEngine());
    }

    public ITemplateEngine GetEngine(string engineName)
    {
        if (_engines.TryGetValue(engineName, out var engine))
        {
            return engine;
        }

        throw new ArgumentException($"Template engine '{engineName}' not found. Available engines: {string.Join(", ", _engines.Keys)}");
    }

    public void RegisterEngine(ITemplateEngine engine)
    {
        _engines[engine.Name] = engine;
    }
}
