# Hazina Code Generation Core

AI-powered code generation system for the Hazina framework.

## Overview

The Code Generation Core provides intelligent parsing of natural language prompts into structured code generation intents, enabling automated code creation, testing, and documentation.

## Features

### Intent Parser

Analyzes natural language prompts and converts them into structured intents for code generation.

**Supported Intent Types**:
- **GenerateMethod**: Create new methods
- **GenerateClass**: Create new classes
- **GenerateTests**: Generate unit tests
- **GenerateDocumentation**: Generate code documentation
- **RefactorCode**: Refactor existing code
- **GenerateInterface**: Create interfaces
- **GenerateModel**: Create data models

### Template Engine

Converts parsed intents into actual C# code using template-based generation.

**Built-in Templates**:
- **MethodTemplate**: Generates methods with XML docs, parameters, and async support
- **ClassTemplate**: Generates classes with properties, methods, constructors, and inheritance
- **TestTemplate**: Generates xUnit test classes with test scenarios and assertions

**Features**:
- Automatic code formatting and indentation
- XML documentation generation
- Support for async/await patterns
- Constructor generation for classes
- Property initialization
- Test framework selection (xUnit, NUnit, MSTest)
- Assertion library selection (FluentAssertions, Assert)
- Mocking framework integration (Moq, NSubstitute)

## Intent Models

### CodeGenerationIntent (Base)

All intents inherit from this base class:

```csharp
public abstract class CodeGenerationIntent
{
    public IntentType Type { get; set; }
    public string Prompt { get; set; }
    public string? TargetNamespace { get; set; }
    public string? TargetFilePath { get; set; }
    public Dictionary<string, string> Context { get; set; }
    public double Confidence { get; set; }
}
```

### MethodGenerationIntent

For generating methods:

```csharp
public class MethodGenerationIntent : CodeGenerationIntent
{
    public string MethodName { get; set; }
    public string ReturnType { get; set; }
    public List<MethodParameter> Parameters { get; set; }
    public string AccessModifier { get; set; }
    public bool IsStatic { get; set; }
    public bool IsAsync { get; set; }
    public string Description { get; set; }
    public string? TargetClassName { get; set; }
}
```

### ClassGenerationIntent

For generating classes:

```csharp
public class ClassGenerationIntent : CodeGenerationIntent
{
    public string ClassName { get; set; }
    public string AccessModifier { get; set; }
    public bool IsStatic { get; set; }
    public bool IsAbstract { get; set; }
    public bool IsSealed { get; set; }
    public string? BaseClass { get; set; }
    public List<string> Interfaces { get; set; }
    public List<ClassProperty> Properties { get; set; }
    public List<MethodGenerationIntent> Methods { get; set; }
    public string Description { get; set; }
    public bool GenerateConstructor { get; set; }
}
```

### TestGenerationIntent

For generating unit tests:

```csharp
public class TestGenerationIntent : CodeGenerationIntent
{
    public string TargetCode { get; set; }
    public string TargetClassName { get; set; }
    public string TestClassName { get; set; }
    public string TestFramework { get; set; } // xUnit, NUnit, MSTest
    public string AssertionLibrary { get; set; } // FluentAssertions, Assert
    public bool UseMocking { get; set; }
    public string MockingFramework { get; set; } // Moq, NSubstitute
    public List<TestScenario> TestScenarios { get; set; }
    public bool GenerateEdgeCases { get; set; }
    public bool GenerateExceptionTests { get; set; }
}
```

## Usage

### End-to-End Code Generation

```csharp
using Hazina.CodeGeneration.Core.Parsing;
using Hazina.CodeGeneration.Core.Templates;
using Microsoft.Extensions.Logging;

// Create services
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var parser = new IntentParser(loggerFactory.CreateLogger<IntentParser>());
var templateEngine = new TemplateEngine(loggerFactory.CreateLogger<TemplateEngine>());

// Parse intent from natural language
var intent = await parser.ParseAsync("Create a method called CalculateTotal that returns decimal");

// Generate code from intent
var code = await templateEngine.GenerateCodeAsync(intent);

Console.WriteLine(code);
// Output:
// /// <summary>
// /// Create a method called CalculateTotal that returns decimal
// /// </summary>
// /// <returns>Create a method called CalculateTotal that returns decimal</returns>
// public decimal CalculateTotal()
// {
//     throw new NotImplementedException();
// }
```

### Basic Intent Parsing

```csharp
using Hazina.CodeGeneration.Core.Parsing;
using Microsoft.Extensions.Logging;

// Create parser
var logger = LoggerFactory.Create(builder => builder.AddConsole())
    .CreateLogger<IntentParser>();
var parser = new IntentParser(logger);

// Parse a method generation prompt
var intent = await parser.ParseAsync("Create a method called CalculateTotal that returns decimal");

if (intent is MethodGenerationIntent methodIntent)
{
    Console.WriteLine($"Method: {methodIntent.MethodName}");
    Console.WriteLine($"Return Type: {methodIntent.ReturnType}");
    Console.WriteLine($"Confidence: {methodIntent.Confidence}");
}
```

### Generating Methods with Template Engine

```csharp
using Hazina.CodeGeneration.Core.Models;
using Hazina.CodeGeneration.Core.Templates;

var templateEngine = new TemplateEngine(logger);

// Create method intent programmatically
var methodIntent = new MethodGenerationIntent
{
    MethodName = "ValidateEmail",
    ReturnType = "bool",
    AccessModifier = "public",
    Description = "Validates an email address format",
    Parameters = new List<MethodParameter>
    {
        new() { Name = "email", Type = "string", Description = "The email to validate" }
    }
};

// Generate code
var code = await templateEngine.GenerateCodeAsync(methodIntent);
Console.WriteLine(code);
```

### Generating Classes with Template Engine

```csharp
var classIntent = new ClassGenerationIntent
{
    ClassName = "UserDto",
    TargetNamespace = "MyApp.Models",
    Description = "Data transfer object for user information",
    Properties = new List<ClassProperty>
    {
        new() { Name = "Id", Type = "int", Description = "User identifier" },
        new() { Name = "Email", Type = "string", IsRequired = true },
        new() { Name = "Name", Type = "string", IsRequired = true }
    }
};

var code = await templateEngine.GenerateCodeAsync(classIntent);
// Generates a complete class with namespace, properties, and constructor
```

### Parsing Class Generation

```csharp
var intent = await parser.ParseAsync(
    "Create a class called UserService that implements IUserService");

if (intent is ClassGenerationIntent classIntent)
{
    Console.WriteLine($"Class: {classIntent.ClassName}");
    Console.WriteLine($"Implements: {string.Join(", ", classIntent.Interfaces)}");
}
```

### Parsing Test Generation

```csharp
var intent = await parser.ParseAsync(
    "Generate unit tests for the UserService class using xUnit and Moq");

if (intent is TestGenerationIntent testIntent)
{
    Console.WriteLine($"Target: {testIntent.TargetClassName}");
    Console.WriteLine($"Test Class: {testIntent.TestClassName}");
    Console.WriteLine($"Framework: {testIntent.TestFramework}");
    Console.WriteLine($"Use Mocking: {testIntent.UseMocking}");
}
```

### Detecting Intent Type

```csharp
var intentType = parser.DetectIntentType("Create a method that validates email addresses");
// Returns: IntentType.GenerateMethod

var intentType2 = parser.DetectIntentType("Generate tests for the EmailValidator class");
// Returns: IntentType.GenerateTests
```

### Validating Intents

```csharp
var intent = await parser.ParseAsync("Create a method");

if (parser.ValidateIntent(intent))
{
    Console.WriteLine("Intent is valid and ready for code generation");
}
else
{
    Console.WriteLine("Intent is missing required information");
}
```

## Intent Detection Patterns

The parser uses keyword matching to detect intent types:

**Method Generation Keywords**:
- "create method", "generate method", "add method", "write method"
- "method that", "function that"

**Class Generation Keywords**:
- "create class", "generate class", "add class", "write class"
- "new class"

**Test Generation Keywords**:
- "create test", "generate test", "write test"
- "test for", "unit test", "tests for"

**Model Generation Keywords**:
- "create model", "generate model"
- "data model", "dto", "entity"

## Advanced Features

### Context Passing

```csharp
var context = new Dictionary<string, string>
{
    { "namespace", "MyApp.Services" },
    { "file_path", "src/Services/UserService.cs" }
};

var intent = await parser.ParseAsync(
    "Create a method to validate user input",
    context);

// Intent will include the context
foreach (var (key, value) in intent.Context)
{
    Console.WriteLine($"{key}: {value}");
}
```

### Async Method Detection

The parser automatically detects when methods should be async:

```csharp
var intent = await parser.ParseAsync(
    "Create an async method to fetch user data from the database");

if (intent is MethodGenerationIntent methodIntent)
{
    Console.WriteLine($"Is Async: {methodIntent.IsAsync}");
    // Output: Is Async: True

    Console.WriteLine($"Return Type: {methodIntent.ReturnType}");
    // Output: Return Type: Task
}
```

### Property and Parameter Extraction

The parser can extract structured information from natural language:

```csharp
var intent = await parser.ParseAsync(
    "Create a class User with properties Name (string), Email (string), and Age (int)");

if (intent is ClassGenerationIntent classIntent)
{
    // Properties would be extracted and structured
    foreach (var prop in classIntent.Properties)
    {
        Console.WriteLine($"{prop.Type} {prop.Name}");
    }
}
```

## Extension Points

### Custom Intent Types

You can extend the system with custom intent types:

```csharp
public class CustomIntent : CodeGenerationIntent
{
    public string CustomProperty { get; set; }

    public CustomIntent()
    {
        Type = (IntentType)100; // Custom type
    }
}
```

### Custom Parsers

Implement `IIntentParser` for custom parsing logic:

```csharp
public class CustomIntentParser : IIntentParser
{
    public async Task<CodeGenerationIntent> ParseAsync(
        string prompt,
        Dictionary<string, string>? context = null,
        CancellationToken cancellationToken = default)
    {
        // Custom parsing logic
        return new CustomIntent { Prompt = prompt };
    }

    public IntentType DetectIntentType(string prompt)
    {
        // Custom detection logic
        return IntentType.GenerateMethod;
    }

    public bool ValidateIntent(CodeGenerationIntent intent)
    {
        // Custom validation logic
        return true;
    }
}
```

## Integration with Hazina

The Code Generation Core integrates with other Hazina components:

- **Observability**: All parsing operations are logged and tracked
- **LLM Providers**: Can use LLMs for advanced intent understanding
- **NeuroChain**: Multi-layer intent analysis for complex prompts

## Performance

- Intent parsing: < 10ms for simple prompts
- Confidence scoring: Automatic based on keyword matches and pattern recognition
- Caching: Future support for intent caching

## Testing

Run unit tests:

```bash
dotnet test tests/Core/CodeGeneration/Hazina.CodeGeneration.Core.Tests/
```

### Code Generation Pipeline

End-to-end pipeline that integrates intent parsing and code generation:

```csharp
using Hazina.CodeGeneration.Core.Pipeline;

// Create pipeline (can use dependency injection)
var pipeline = new CodeGenerationPipeline(
    intentParser,
    templateEngine,
    logger);

// Generate code from natural language
var result = await pipeline.GenerateFromPromptAsync(
    "Create a method called CalculateTotal that returns decimal");

if (result.Success)
{
    Console.WriteLine("Generated Code:");
    Console.WriteLine(result.GeneratedCode);
    Console.WriteLine($"\nConfidence: {result.Confidence:P}");
}
else
{
    Console.WriteLine("Errors:");
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"  - {error}");
    }
}

// Generate and save to file
var saveResult = await pipeline.GenerateAndSaveAsync(
    "Create a class called UserService",
    "src/Services/UserService.cs");
```

**Pipeline Features**:
- Integrated intent parsing and code generation
- Automatic validation
- Error and warning collection
- Confidence scoring
- Metadata tracking
- File creation with directory support

**CodeGenerationResult**:
- `Success`: Whether generation succeeded
- `GeneratedCode`: The generated code
- `Confidence`: Confidence score (0.0 to 1.0)
- `Errors`: List of errors encountered
- `Warnings`: List of warnings (e.g., low confidence)
- `Metadata`: Additional information (intent type, code length, file path)

## Architecture

The code generation system consists of three main components:

1. **Intent Parser**: Analyzes natural language and extracts structured intent
2. **Template Engine**: Converts intents into formatted C# code
3. **Pipeline**: Orchestrates the end-to-end process with error handling

```
Natural Language Prompt
         ↓
   Intent Parser
         ↓
   Code Intent (validated)
         ↓
   Template Engine
         ↓
   Generated Code (formatted)
         ↓
   CodeGenerationResult
```

## Dependency Injection

Register services with DI container:

```csharp
services.AddSingleton<IIntentParser, IntentParser>();
services.AddSingleton<ITemplateEngine, TemplateEngine>();
services.AddSingleton<ICodeGenerationPipeline, CodeGenerationPipeline>();
```

## Next Steps

Future enhancements for the code generation system:

1. **LLM Integration**: Use LLMs for advanced intent understanding
2. **Semantic Analysis**: Deep code analysis for context-aware generation
3. **Incremental Updates**: Add code to existing files intelligently
4. **Refactoring Support**: Advanced code transformation capabilities
5. **Custom Templates**: User-defined code generation templates
6. **Multi-Language Support**: Generate code in multiple languages

## License

Part of the Hazina AI Framework

---

**Version**: 1.0.0
**Status**: Phase 3 Complete ✅
- Intent Parser: ✅
- Template Engine: ✅
- Pipeline Integration: ✅
