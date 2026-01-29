using System.Text.RegularExpressions;

namespace Hazina.CLI.Models;

/// <summary>
/// Extracts code symbols (classes, methods, interfaces) from source code.
/// Uses regex patterns for language-agnostic extraction.
/// </summary>
public static partial class SymbolExtractor
{
    // C# patterns
    [GeneratedRegex(@"(?:public|private|protected|internal)?\s*(?:static\s+)?(?:partial\s+)?class\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex CSharpClassPattern();

    [GeneratedRegex(@"(?:public|private|protected|internal)?\s*(?:static\s+)?interface\s+(I\w+)", RegexOptions.Multiline)]
    private static partial Regex CSharpInterfacePattern();

    [GeneratedRegex(@"(?:public|private|protected|internal)?\s*(?:static\s+)?(?:async\s+)?(?:virtual\s+)?(?:override\s+)?(?:abstract\s+)?(?:Task<?\w*>?|void|string|int|bool|object|\w+)\s+(\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex CSharpMethodPattern();

    [GeneratedRegex(@"(?:public|private|protected|internal)?\s*record\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex CSharpRecordPattern();

    [GeneratedRegex(@"(?:public|private|protected|internal)?\s*enum\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex CSharpEnumPattern();

    // TypeScript/JavaScript patterns
    [GeneratedRegex(@"(?:export\s+)?(?:default\s+)?(?:abstract\s+)?class\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TypeScriptClassPattern();

    [GeneratedRegex(@"(?:export\s+)?interface\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TypeScriptInterfacePattern();

    [GeneratedRegex(@"(?:export\s+)?(?:async\s+)?function\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TypeScriptFunctionPattern();

    [GeneratedRegex(@"(?:const|let|var)\s+(\w+)\s*=\s*(?:async\s+)?\(", RegexOptions.Multiline)]
    private static partial Regex TypeScriptArrowFunctionPattern();

    [GeneratedRegex(@"(?:export\s+)?type\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TypeScriptTypePattern();

    [GeneratedRegex(@"(?:export\s+)?enum\s+(\w+)", RegexOptions.Multiline)]
    private static partial Regex TypeScriptEnumPattern();

    // React component patterns
    [GeneratedRegex(@"(?:export\s+)?(?:default\s+)?(?:const|function)\s+(\w+)\s*[=:]?\s*(?:\(|React\.FC)", RegexOptions.Multiline)]
    private static partial Regex ReactComponentPattern();

    // Reference patterns (what this code calls/uses)
    [GeneratedRegex(@"new\s+(\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex NewInstancePattern();

    [GeneratedRegex(@":\s*(I\w+)(?:\s*,|\s*\{|\s*$)", RegexOptions.Multiline)]
    private static partial Regex ImplementsPattern();

    [GeneratedRegex(@"(?:await\s+)?(\w+)\.(\w+)\s*\(", RegexOptions.Multiline)]
    private static partial Regex MethodCallPattern();

    [GeneratedRegex(@"import\s+(?:type\s+)?{?\s*([^}]+?)}\s+from", RegexOptions.Multiline)]
    private static partial Regex ImportPattern();

    [GeneratedRegex(@"using\s+(\w+(?:\.\w+)*);", RegexOptions.Multiline)]
    private static partial Regex UsingPattern();

    /// <summary>
    /// Extract symbols defined in this code.
    /// </summary>
    public static HashSet<string> ExtractDefinedSymbols(string content, string language)
    {
        var symbols = new HashSet<string>(StringComparer.Ordinal);

        switch (language)
        {
            case "csharp":
                ExtractMatches(content, CSharpClassPattern(), symbols);
                ExtractMatches(content, CSharpInterfacePattern(), symbols);
                ExtractMatches(content, CSharpMethodPattern(), symbols);
                ExtractMatches(content, CSharpRecordPattern(), symbols);
                ExtractMatches(content, CSharpEnumPattern(), symbols);
                break;

            case "typescript":
            case "javascript":
                ExtractMatches(content, TypeScriptClassPattern(), symbols);
                ExtractMatches(content, TypeScriptInterfacePattern(), symbols);
                ExtractMatches(content, TypeScriptFunctionPattern(), symbols);
                ExtractMatches(content, TypeScriptArrowFunctionPattern(), symbols);
                ExtractMatches(content, TypeScriptTypePattern(), symbols);
                ExtractMatches(content, TypeScriptEnumPattern(), symbols);
                ExtractMatches(content, ReactComponentPattern(), symbols);
                break;
        }

        // Filter out common noise
        symbols.RemoveWhere(s =>
            s.Length < 2 ||
            s.Equals("get", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("set", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("if", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("for", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("while", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("return", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("void", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("null", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("false", StringComparison.OrdinalIgnoreCase));

        return symbols;
    }

    /// <summary>
    /// Extract symbols referenced/called from this code.
    /// </summary>
    public static HashSet<string> ExtractReferences(string content, string language)
    {
        var references = new HashSet<string>(StringComparer.Ordinal);

        // New instances
        foreach (Match match in NewInstancePattern().Matches(content))
        {
            var name = match.Groups[1].Value;
            if (IsValidSymbolName(name))
                references.Add(name);
        }

        // Interface implementations
        foreach (Match match in ImplementsPattern().Matches(content))
        {
            var name = match.Groups[1].Value;
            if (IsValidSymbolName(name))
                references.Add(name);
        }

        // Method calls (ClassName.MethodName)
        foreach (Match match in MethodCallPattern().Matches(content))
        {
            var className = match.Groups[1].Value;
            var methodName = match.Groups[2].Value;
            if (IsValidSymbolName(className) && IsValidSymbolName(methodName))
            {
                references.Add(className);
                references.Add($"{className}.{methodName}");
            }
        }

        // Imports (TypeScript/JavaScript)
        if (language == "typescript" || language == "javascript")
        {
            foreach (Match match in ImportPattern().Matches(content))
            {
                var imports = match.Groups[1].Value;
                foreach (var import in imports.Split(','))
                {
                    var name = import.Trim().Split(' ')[0]; // Handle "X as Y"
                    if (IsValidSymbolName(name))
                        references.Add(name);
                }
            }
        }

        // Filter out noise
        references.RemoveWhere(s =>
            s.Length < 2 ||
            s.StartsWith("get", StringComparison.OrdinalIgnoreCase) ||
            s.StartsWith("set", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("Console", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("String", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("Object", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("Array", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("Math", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("Date", StringComparison.OrdinalIgnoreCase));

        return references;
    }

    private static void ExtractMatches(string content, Regex pattern, HashSet<string> symbols)
    {
        foreach (Match match in pattern.Matches(content))
        {
            var name = match.Groups[1].Value;
            if (IsValidSymbolName(name))
                symbols.Add(name);
        }
    }

    private static bool IsValidSymbolName(string name)
    {
        return !string.IsNullOrWhiteSpace(name) &&
               name.Length >= 2 &&
               char.IsLetter(name[0]) &&
               !char.IsLower(name[0]) && // Start with uppercase (convention for types)
               name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}

/// <summary>
/// Represents the symbol graph for a store.
/// </summary>
public class SymbolGraph
{
    /// <summary>
    /// Map from symbol name to its metadata.
    /// </summary>
    public Dictionary<string, SymbolInfo> Symbols { get; set; } = new();

    /// <summary>
    /// Add or update a symbol definition.
    /// </summary>
    public void AddDefinition(string symbol, string file, string chunkId)
    {
        if (!Symbols.TryGetValue(symbol, out var info))
        {
            info = new SymbolInfo { Name = symbol };
            Symbols[symbol] = info;
        }

        info.DefinedIn.Add(file);
        info.ChunkIds.Add(chunkId);
    }

    /// <summary>
    /// Add a reference from one symbol to another.
    /// </summary>
    public void AddReference(string fromSymbol, string toSymbol, string file)
    {
        if (!Symbols.TryGetValue(fromSymbol, out var fromInfo))
        {
            fromInfo = new SymbolInfo { Name = fromSymbol };
            Symbols[fromSymbol] = fromInfo;
        }

        if (!Symbols.TryGetValue(toSymbol, out var toInfo))
        {
            toInfo = new SymbolInfo { Name = toSymbol };
            Symbols[toSymbol] = toInfo;
        }

        fromInfo.Calls.Add(toSymbol);
        toInfo.CalledBy.Add(fromSymbol);
    }

    /// <summary>
    /// Get all symbols that reference the given symbol.
    /// </summary>
    public HashSet<string> GetCallers(string symbol)
    {
        return Symbols.TryGetValue(symbol, out var info) ? info.CalledBy : new();
    }

    /// <summary>
    /// Get all symbols that the given symbol references.
    /// </summary>
    public HashSet<string> GetCallees(string symbol)
    {
        return Symbols.TryGetValue(symbol, out var info) ? info.Calls : new();
    }

    /// <summary>
    /// Get chunk IDs where a symbol is defined.
    /// </summary>
    public HashSet<string> GetChunksForSymbol(string symbol)
    {
        return Symbols.TryGetValue(symbol, out var info) ? info.ChunkIds : new();
    }
}

/// <summary>
/// Metadata for a code symbol.
/// </summary>
public class SymbolInfo
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = ""; // "class", "interface", "method", "function"
    public HashSet<string> DefinedIn { get; set; } = new();
    public HashSet<string> ChunkIds { get; set; } = new();
    public HashSet<string> Implements { get; set; } = new();
    public HashSet<string> Calls { get; set; } = new();
    public HashSet<string> CalledBy { get; set; } = new();
}
