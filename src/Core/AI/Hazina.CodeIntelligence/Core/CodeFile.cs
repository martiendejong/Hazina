namespace Hazina.CodeIntelligence.Core;

/// <summary>
/// Represents a code file in the project
/// </summary>
public class CodeFile
{
    /// <summary>
    /// File path relative to project root
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// File content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Programming language
    /// </summary>
    public string Language { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Line count
    /// </summary>
    public int LineCount { get; set; }

    /// <summary>
    /// Dependencies (other files this file references)
    /// </summary>
    public List<string> Dependencies { get; set; } = new();

    /// <summary>
    /// Dependents (other files that reference this file)
    /// </summary>
    public List<string> Dependents { get; set; } = new();

    /// <summary>
    /// Symbols defined in this file (classes, functions, etc.)
    /// </summary>
    public List<CodeSymbol> Symbols { get; set; } = new();

    /// <summary>
    /// File metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Represents a code symbol (class, function, variable, etc.)
/// </summary>
public class CodeSymbol
{
    /// <summary>
    /// Symbol name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Symbol type (class, function, variable, etc.)
    /// </summary>
    public SymbolType Type { get; set; }

    /// <summary>
    /// File where symbol is defined
    /// </summary>
    public string DefinedIn { get; set; } = string.Empty;

    /// <summary>
    /// Line number where symbol starts
    /// </summary>
    public int StartLine { get; set; }

    /// <summary>
    /// Line number where symbol ends
    /// </summary>
    public int EndLine { get; set; }

    /// <summary>
    /// Symbol visibility (public, private, etc.)
    /// </summary>
    public string Visibility { get; set; } = string.Empty;

    /// <summary>
    /// References to this symbol from other files
    /// </summary>
    public List<SymbolReference> References { get; set; } = new();

    /// <summary>
    /// Symbol metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Symbol types
/// </summary>
public enum SymbolType
{
    Class,
    Interface,
    Function,
    Method,
    Property,
    Field,
    Variable,
    Constant,
    Enum,
    Namespace,
    Module
}

/// <summary>
/// Reference to a symbol
/// </summary>
public class SymbolReference
{
    /// <summary>
    /// File containing the reference
    /// </summary>
    public string File { get; set; } = string.Empty;

    /// <summary>
    /// Line number of reference
    /// </summary>
    public int Line { get; set; }

    /// <summary>
    /// Context of the reference
    /// </summary>
    public string Context { get; set; } = string.Empty;
}

/// <summary>
/// Project context with code files and dependencies
/// </summary>
public class ProjectContext
{
    /// <summary>
    /// Project root path
    /// </summary>
    public string RootPath { get; set; } = string.Empty;

    /// <summary>
    /// Project name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// All code files in the project
    /// </summary>
    public List<CodeFile> Files { get; set; } = new();

    /// <summary>
    /// All symbols in the project
    /// </summary>
    public Dictionary<string, CodeSymbol> Symbols { get; set; } = new();

    /// <summary>
    /// Dependency graph
    /// </summary>
    public Dictionary<string, List<string>> DependencyGraph { get; set; } = new();

    /// <summary>
    /// Project patterns and conventions
    /// </summary>
    public List<ProjectPattern> Patterns { get; set; } = new();

    /// <summary>
    /// Architectural insights
    /// </summary>
    public ArchitecturalInsights? Architecture { get; set; }
}

/// <summary>
/// Project pattern or convention
/// </summary>
public class ProjectPattern
{
    /// <summary>
    /// Pattern name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Pattern description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Pattern category
    /// </summary>
    public PatternCategory Category { get; set; }

    /// <summary>
    /// Examples of this pattern
    /// </summary>
    public List<string> Examples { get; set; } = new();

    /// <summary>
    /// Confidence in this pattern (0-1)
    /// </summary>
    public double Confidence { get; set; }
}

/// <summary>
/// Pattern categories
/// </summary>
public enum PatternCategory
{
    NamingConvention,
    ArchitecturalPattern,
    DesignPattern,
    CodingStyle,
    ErrorHandling,
    Testing,
    Documentation
}

/// <summary>
/// Architectural insights about the project
/// </summary>
public class ArchitecturalInsights
{
    /// <summary>
    /// Identified architectural pattern (MVC, MVVM, Clean Architecture, etc.)
    /// </summary>
    public string? ArchitecturalPattern { get; set; }

    /// <summary>
    /// Project structure description
    /// </summary>
    public string? StructureDescription { get; set; }

    /// <summary>
    /// Key components and their roles
    /// </summary>
    public Dictionary<string, string> KeyComponents { get; set; } = new();

    /// <summary>
    /// Layer separation (if applicable)
    /// </summary>
    public List<string> Layers { get; set; } = new();

    /// <summary>
    /// Dependencies between layers/components
    /// </summary>
    public List<ComponentDependency> ComponentDependencies { get; set; } = new();
}

/// <summary>
/// Dependency between components
/// </summary>
public class ComponentDependency
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public DependencyType Type { get; set; }
    public int Strength { get; set; } // Number of references
}

/// <summary>
/// Dependency types
/// </summary>
public enum DependencyType
{
    Direct,
    Indirect,
    Circular
}
