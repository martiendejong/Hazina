namespace Hazina.CodeGeneration.Core.Models;

/// <summary>
/// Intent for generating a new class
/// </summary>
public class ClassGenerationIntent : CodeGenerationIntent
{
    /// <summary>
    /// The name of the class to generate
    /// </summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>
    /// The access modifier (public, internal)
    /// </summary>
    public string AccessModifier { get; set; } = "public";

    /// <summary>
    /// Whether the class is static
    /// </summary>
    public bool IsStatic { get; set; }

    /// <summary>
    /// Whether the class is abstract
    /// </summary>
    public bool IsAbstract { get; set; }

    /// <summary>
    /// Whether the class is sealed
    /// </summary>
    public bool IsSealed { get; set; }

    /// <summary>
    /// Base class to inherit from
    /// </summary>
    public string? BaseClass { get; set; }

    /// <summary>
    /// Interfaces to implement
    /// </summary>
    public List<string> Interfaces { get; set; } = new();

    /// <summary>
    /// Properties to include in the class
    /// </summary>
    public List<ClassProperty> Properties { get; set; } = new();

    /// <summary>
    /// Methods to include in the class
    /// </summary>
    public List<MethodGenerationIntent> Methods { get; set; } = new();

    /// <summary>
    /// Description of what the class represents
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether to generate a constructor
    /// </summary>
    public bool GenerateConstructor { get; set; } = true;

    public ClassGenerationIntent()
    {
        Type = IntentType.GenerateClass;
    }
}

/// <summary>
/// Represents a class property
/// </summary>
public class ClassProperty
{
    /// <summary>
    /// Property name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Property type
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Access modifier
    /// </summary>
    public string AccessModifier { get; set; } = "public";

    /// <summary>
    /// Whether the property has a getter
    /// </summary>
    public bool HasGetter { get; set; } = true;

    /// <summary>
    /// Whether the property has a setter
    /// </summary>
    public bool HasSetter { get; set; } = true;

    /// <summary>
    /// Whether the property is required
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Default value
    /// </summary>
    public string? DefaultValue { get; set; }

    /// <summary>
    /// Property description
    /// </summary>
    public string? Description { get; set; }
}
