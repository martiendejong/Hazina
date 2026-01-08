using Hazina.Tools.ContextCompression.Interfaces;
using Hazina.Tools.ContextCompression.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

namespace Hazina.Tools.ContextCompression.Strategies;

/// <summary>
/// AST-based compression that extracts code structure without implementation details
/// </summary>
public class AstCompressor : IContextCompressor
{
    private readonly ITokenCounter _tokenCounter;

    public AstCompressor(ITokenCounter tokenCounter)
    {
        _tokenCounter = tokenCounter;
    }

    public bool SupportsStrategy(CompressionStrategy strategy)
    {
        return strategy == CompressionStrategy.Ast ||
               strategy == CompressionStrategy.AstWithSymbols;
    }

    public async Task<CompressedContext> CompressAsync(
        CompressionRequest request,
        CompressionOptions options)
    {
        var result = new CompressedContext
        {
            StrategyUsed = CompressionStrategy.Ast,
            IncludedFiles = new List<string>(request.FilePaths)
        };

        var contentBuilder = new StringBuilder();
        int originalTokenCount = 0;

        foreach (var filePath in request.FilePaths)
        {
            if (!File.Exists(filePath))
                continue;

            var code = await File.ReadAllTextAsync(filePath);
            originalTokenCount += _tokenCounter.Count(code);

            var compressed = CompressCSharpFile(code, filePath, options);
            contentBuilder.AppendLine(compressed);
        }

        result.Content = contentBuilder.ToString();
        result.OriginalTokens = originalTokenCount;
        result.CompressedTokens = _tokenCounter.Count(result.Content);

        return result;
    }

    private string CompressCSharpFile(string code, string filePath, CompressionOptions options)
    {
        var tree = CSharpSyntaxTree.ParseText(code);
        var root = tree.GetRoot();

        var output = new StringBuilder();

        if (options.IncludeFilePaths)
        {
            output.AppendLine($"// File: {filePath}");
        }

        // Extract namespaces
        var namespaces = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>();
        foreach (var ns in namespaces)
        {
            output.AppendLine($"namespace {ns.Name};");
        }

        // Extract using directives
        var usings = root.DescendantNodes().OfType<UsingDirectiveSyntax>();
        foreach (var usingDirective in usings.Take(5)) // Limit to top 5
        {
            output.AppendLine(usingDirective.ToString());
        }

        if (usings.Count() > 5)
        {
            output.AppendLine($"// ... and {usings.Count() - 5} more using directives");
        }

        output.AppendLine();

        // Extract classes
        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var cls in classes)
        {
            ExtractClassStructure(cls, output, options);
        }

        // Extract interfaces
        var interfaces = root.DescendantNodes().OfType<InterfaceDeclarationSyntax>();
        foreach (var iface in interfaces)
        {
            ExtractInterfaceStructure(iface, output, options);
        }

        // Extract enums
        var enums = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
        foreach (var enumDecl in enums)
        {
            output.AppendLine(enumDecl.ToString());
            output.AppendLine();
        }

        return output.ToString();
    }

    private void ExtractClassStructure(ClassDeclarationSyntax cls, StringBuilder output, CompressionOptions options)
    {
        // Class declaration with attributes
        var attributes = cls.AttributeLists.ToString();
        if (!string.IsNullOrWhiteSpace(attributes))
        {
            output.AppendLine(attributes);
        }

        var modifiers = cls.Modifiers.ToString();
        output.AppendLine($"{modifiers} class {cls.Identifier}{cls.TypeParameterList}");

        if (cls.BaseList != null)
        {
            output.AppendLine($"    {cls.BaseList}");
        }

        output.AppendLine("{");

        // Extract properties
        var properties = cls.Members.OfType<PropertyDeclarationSyntax>();
        foreach (var prop in properties)
        {
            if (!options.IncludePrivateMembers && prop.Modifiers.Any(SyntaxKind.PrivateKeyword))
                continue;

            var propModifiers = prop.Modifiers.ToString();
            output.AppendLine($"    {propModifiers} {prop.Type} {prop.Identifier} {{ get; set; }}");
        }

        // Extract fields
        var fields = cls.Members.OfType<FieldDeclarationSyntax>();
        foreach (var field in fields)
        {
            if (!options.IncludePrivateMembers && field.Modifiers.Any(SyntaxKind.PrivateKeyword))
                continue;

            var fieldModifiers = field.Modifiers.ToString();
            output.AppendLine($"    {fieldModifiers} {field.Declaration.Type} {field.Declaration.Variables};");
        }

        // Extract method signatures
        var methods = cls.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            if (!options.IncludePrivateMembers && method.Modifiers.Any(SyntaxKind.PrivateKeyword))
                continue;

            ExtractMethodSignature(method, output, options);
        }

        // Extract constructors
        var constructors = cls.Members.OfType<ConstructorDeclarationSyntax>();
        foreach (var ctor in constructors)
        {
            if (!options.IncludePrivateMembers && ctor.Modifiers.Any(SyntaxKind.PrivateKeyword))
                continue;

            var ctorModifiers = ctor.Modifiers.ToString();
            output.AppendLine($"    {ctorModifiers} {ctor.Identifier}{ctor.ParameterList}");
        }

        output.AppendLine("}");
        output.AppendLine();
    }

    private void ExtractInterfaceStructure(InterfaceDeclarationSyntax iface, StringBuilder output, CompressionOptions options)
    {
        var modifiers = iface.Modifiers.ToString();
        output.AppendLine($"{modifiers} interface {iface.Identifier}{iface.TypeParameterList}");

        if (iface.BaseList != null)
        {
            output.AppendLine($"    {iface.BaseList}");
        }

        output.AppendLine("{");

        // Extract method signatures
        var methods = iface.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            ExtractMethodSignature(method, output, options);
        }

        // Extract properties
        var properties = iface.Members.OfType<PropertyDeclarationSyntax>();
        foreach (var prop in properties)
        {
            output.AppendLine($"    {prop.Type} {prop.Identifier} {{ get; set; }}");
        }

        output.AppendLine("}");
        output.AppendLine();
    }

    private void ExtractMethodSignature(MethodDeclarationSyntax method, StringBuilder output, CompressionOptions options)
    {
        // Include XML documentation if comments are enabled
        if (options.IncludeComments)
        {
            var leadingTrivia = method.GetLeadingTrivia();
            var xmlComment = leadingTrivia.FirstOrDefault(t => t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
            if (xmlComment != default)
            {
                var commentText = xmlComment.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(commentText))
                {
                    output.AppendLine($"    {commentText}");
                }
            }
        }

        var methodModifiers = method.Modifiers.ToString();
        var returnType = method.ReturnType.ToString();
        var parameters = method.ParameterList.ToString();

        output.AppendLine($"    {methodModifiers} {returnType} {method.Identifier}{parameters}");
    }
}
