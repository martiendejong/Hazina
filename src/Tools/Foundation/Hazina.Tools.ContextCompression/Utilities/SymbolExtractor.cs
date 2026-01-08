using Hazina.Tools.ContextCompression.Interfaces;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hazina.Tools.ContextCompression.Utilities;

/// <summary>
/// Extracts symbols (classes, methods, etc.) from code files
/// </summary>
public class SymbolExtractor : ISymbolExtractor
{
    public async Task<Dictionary<string, string>> ExtractSymbolsAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return new Dictionary<string, string>();

        var code = await File.ReadAllTextAsync(filePath);
        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        var language = extension switch
        {
            ".cs" => "csharp",
            ".js" => "javascript",
            ".ts" => "typescript",
            _ => "unknown"
        };

        var lineNumbers = await ExtractSymbolsFromCodeAsync(code, language);

        // Convert line numbers to "file:line" format
        return lineNumbers.ToDictionary(
            kvp => kvp.Key,
            kvp => $"{filePath}:{kvp.Value}"
        );
    }

    public async Task<Dictionary<string, int>> ExtractSymbolsFromCodeAsync(string code, string language)
    {
        return language.ToLowerInvariant() switch
        {
            "csharp" or "cs" => await ExtractCSharpSymbolsAsync(code),
            _ => new Dictionary<string, int>()
        };
    }

    private Task<Dictionary<string, int>> ExtractCSharpSymbolsAsync(string code)
    {
        var symbols = new Dictionary<string, int>();
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        // Extract classes
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var cls in classes)
        {
            var lineNumber = tree.GetLineSpan(cls.Span).StartLinePosition.Line + 1;
            symbols[$"class {cls.Identifier}"] = lineNumber;

            // Extract methods
            var methods = cls.Members.OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var methodLine = tree.GetLineSpan(method.Span).StartLinePosition.Line + 1;
                symbols[$"{cls.Identifier}.{method.Identifier}"] = methodLine;
            }

            // Extract properties
            var properties = cls.Members.OfType<PropertyDeclarationSyntax>();
            foreach (var prop in properties)
            {
                var propLine = tree.GetLineSpan(prop.Span).StartLinePosition.Line + 1;
                symbols[$"{cls.Identifier}.{prop.Identifier}"] = propLine;
            }
        }

        // Extract interfaces
        var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        foreach (var iface in interfaces)
        {
            var lineNumber = tree.GetLineSpan(iface.Span).StartLinePosition.Line + 1;
            symbols[$"interface {iface.Identifier}"] = lineNumber;
        }

        // Extract enums
        var enums = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
        foreach (var enumDecl in enums)
        {
            var lineNumber = tree.GetLineSpan(enumDecl.Span).StartLinePosition.Line + 1;
            symbols[$"enum {enumDecl.Identifier}"] = lineNumber;
        }

        return Task.FromResult(symbols);
    }
}
