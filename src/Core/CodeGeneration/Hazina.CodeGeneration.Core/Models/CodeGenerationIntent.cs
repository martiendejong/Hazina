namespace Hazina.CodeGeneration.Core.Models;

/// <summary>
/// Represents the type of code generation intent
/// </summary>
public enum IntentType
{
    /// <summary>
    /// Generate a new method
    /// </summary>
    GenerateMethod,

    /// <summary>
    /// Generate a new class
    /// </summary>
    GenerateClass,

    /// <summary>
    /// Generate unit tests
    /// </summary>
    GenerateTests,

    /// <summary>
    /// Generate documentation
    /// </summary>
    GenerateDocumentation,

    /// <summary>
    /// Refactor existing code
    /// </summary>
    RefactorCode,

    /// <summary>
    /// Generate interface
    /// </summary>
    GenerateInterface,

    /// <summary>
    /// Generate data model
    /// </summary>
    GenerateModel
}

/// <summary>
/// Base class for all code generation intents
/// </summary>
public abstract class CodeGenerationIntent
{
    /// <summary>
    /// The type of intent
    /// </summary>
    public IntentType Type { get; set; }

    /// <summary>
    /// The original natural language prompt
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// The target namespace for the generated code
    /// </summary>
    public string? TargetNamespace { get; set; }

    /// <summary>
    /// The target file path for the generated code
    /// </summary>
    public string? TargetFilePath { get; set; }

    /// <summary>
    /// Additional context or constraints
    /// </summary>
    public Dictionary<string, string> Context { get; set; } = new();

    /// <summary>
    /// Confidence score of the intent parsing (0.0 to 1.0)
    /// </summary>
    public double Confidence { get; set; }
}
