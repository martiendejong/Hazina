namespace Hazina.CodeGeneration.Core.Models;

/// <summary>
/// Intent for generating a new method
/// </summary>
public class MethodGenerationIntent : CodeGenerationIntent
{
    /// <summary>
    /// The name of the method to generate
    /// </summary>
    public string MethodName { get; set; } = string.Empty;

    /// <summary>
    /// The return type of the method
    /// </summary>
    public string ReturnType { get; set; } = "void";

    /// <summary>
    /// The parameters for the method
    /// </summary>
    public List<MethodParameter> Parameters { get; set; } = new();

    /// <summary>
    /// The access modifier (public, private, protected, internal)
    /// </summary>
    public string AccessModifier { get; set; } = "public";

    /// <summary>
    /// Whether the method is static
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Whether the method is async
    /// </summary>
    public bool IsAsync { get; set; }

    /// <summary>
    /// Description of what the method should do
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The class name where the method should be added
    /// </summary>
    public string? TargetClassName { get; set; }

    public MethodGenerationIntent()
    {
        Type = IntentType.GenerateMethod;
    }
}

/// <summary>
/// Represents a method parameter
/// </summary>
public class MethodParameter
{
    /// <summary>
    /// Parameter name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Parameter type
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Whether the parameter is optional
    /// </summary>
    public bool IsOptional { get; set; }

    /// <summary>
    /// Default value if optional
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Parameter description
    /// </summary>
    public string? Description { get; set; }
}
