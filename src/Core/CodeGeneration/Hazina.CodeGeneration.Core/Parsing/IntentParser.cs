using Hazina.CodeGeneration.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Hazina.CodeGeneration.Core.Parsing;

/// <summary>
/// Parses natural language prompts into code generation intents
/// </summary>
public class IntentParser : IIntentParser
{
    private readonly ILogger<IntentParser> _logger;

    // Pattern dictionaries for intent detection
    private static readonly Dictionary<IntentType, List<string>> IntentKeywords = new()
    {
        { IntentType.GenerateMethod, new() { "create method", "generate method", "add method", "write method", "method that", "function that" } },
        { IntentType.GenerateClass, new() { "create class", "generate class", "add class", "write class", "new class" } },
        { IntentType.GenerateTests, new() { "create test", "generate test", "write test", "test for", "unit test", "tests for" } },
        { IntentType.GenerateDocumentation, new() { "document", "add documentation", "generate docs", "write documentation" } },
        { IntentType.RefactorCode, new() { "refactor", "improve", "optimize", "clean up" } },
        { IntentType.GenerateInterface, new() { "create interface", "generate interface", "add interface", "new interface" } },
        { IntentType.GenerateModel, new() { "create model", "generate model", "data model", "dto", "entity" } }
    };

    public IntentParser(ILogger<IntentParser> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CodeGenerationIntent> ParseAsync(
        string prompt,
        Dictionary<string, string>? context = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("[INTENT PARSER] Parsing prompt: {Prompt}", prompt);

        // Detect intent type
        var intentType = DetectIntentType(prompt);
        _logger.LogInformation("[INTENT PARSER] Detected intent type: {IntentType}", intentType);

        // Parse based on intent type
        var intent = intentType switch
        {
            IntentType.GenerateMethod => await ParseMethodIntentAsync(prompt, context, cancellationToken),
            IntentType.GenerateClass => await ParseClassIntentAsync(prompt, context, cancellationToken),
            IntentType.GenerateTests => await ParseTestIntentAsync(prompt, context, cancellationToken),
            IntentType.GenerateInterface => await ParseInterfaceIntentAsync(prompt, context, cancellationToken),
            IntentType.GenerateModel => await ParseModelIntentAsync(prompt, context, cancellationToken),
            _ => new MethodGenerationIntent { Prompt = prompt, Confidence = 0.5 }
        };

        // Add context if provided
        if (context != null)
        {
            foreach (var (key, value) in context)
            {
                intent.Context[key] = value;
            }
        }

        _logger.LogInformation("[INTENT PARSER] Parsed intent with confidence: {Confidence}", intent.Confidence);
        return intent;
    }

    /// <inheritdoc/>
    public IntentType DetectIntentType(string prompt)
    {
        var lowerPrompt = prompt.ToLower();

        // Check each intent type's keywords
        var scores = new Dictionary<IntentType, int>();

        foreach (var (intentType, keywords) in IntentKeywords)
        {
            var score = keywords.Count(keyword => lowerPrompt.Contains(keyword));
            scores[intentType] = score;
        }

        // Return the intent type with highest score
        var maxScore = scores.Values.Max();
        if (maxScore > 0)
        {
            return scores.First(kvp => kvp.Value == maxScore).Key;
        }

        // Default to method generation
        return IntentType.GenerateMethod;
    }

    /// <inheritdoc/>
    public bool ValidateIntent(CodeGenerationIntent intent)
    {
        // Check confidence threshold
        if (intent.Confidence < 0.3)
        {
            _logger.LogWarning("[INTENT PARSER] Low confidence intent: {Confidence}", intent.Confidence);
            return false;
        }

        // Validate based on type
        return intent switch
        {
            MethodGenerationIntent method => !string.IsNullOrWhiteSpace(method.MethodName),
            ClassGenerationIntent @class => !string.IsNullOrWhiteSpace(@class.ClassName),
            TestGenerationIntent test => !string.IsNullOrWhiteSpace(test.TargetClassName),
            _ => true
        };
    }

    private Task<CodeGenerationIntent> ParseMethodIntentAsync(
        string prompt,
        Dictionary<string, string>? context,
        CancellationToken cancellationToken)
    {
        var intent = new MethodGenerationIntent
        {
            Prompt = prompt,
            Confidence = 0.8
        };

        // Extract method name
        var methodNameMatch = Regex.Match(prompt, @"(?:called|named)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase);
        if (methodNameMatch.Success)
        {
            intent.MethodName = methodNameMatch.Groups[1].Value;
            intent.Confidence = 0.9;
        }
        else
        {
            // Try to infer from action words
            var actionMatch = Regex.Match(prompt, @"(calculate|get|set|find|create|update|delete|process|validate|parse)\s+(\w+)", RegexOptions.IgnoreCase);
            if (actionMatch.Success)
            {
                var action = ToPascalCase(actionMatch.Groups[1].Value);
                var subject = ToPascalCase(actionMatch.Groups[2].Value);
                intent.MethodName = action + subject;
            }
        }

        // Detect async
        if (prompt.Contains("async", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("await", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("asynchronous", StringComparison.OrdinalIgnoreCase))
        {
            intent.IsAsync = true;
            if (!string.IsNullOrWhiteSpace(intent.ReturnType) && intent.ReturnType != "void")
            {
                intent.ReturnType = $"Task<{intent.ReturnType}>";
            }
            else
            {
                intent.ReturnType = "Task";
            }
        }

        // Detect return type
        var returnTypeMatch = Regex.Match(prompt, @"returns?\s+(?:a\s+)?([A-Za-z_][A-Za-z0-9_<>]*)", RegexOptions.IgnoreCase);
        if (returnTypeMatch.Success)
        {
            intent.ReturnType = returnTypeMatch.Groups[1].Value;
        }

        // Extract description
        intent.Description = prompt;

        return Task.FromResult<CodeGenerationIntent>(intent);
    }

    private Task<CodeGenerationIntent> ParseClassIntentAsync(
        string prompt,
        Dictionary<string, string>? context,
        CancellationToken cancellationToken)
    {
        var intent = new ClassGenerationIntent
        {
            Prompt = prompt,
            Confidence = 0.8
        };

        // Extract class name
        var classNameMatch = Regex.Match(prompt, @"(?:called|named)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase);
        if (classNameMatch.Success)
        {
            intent.ClassName = classNameMatch.Groups[1].Value;
            intent.Confidence = 0.9;
        }

        // Detect inheritance
        var inheritsMatch = Regex.Match(prompt, @"inherits?\s+(?:from\s+)?([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase);
        if (inheritsMatch.Success)
        {
            intent.BaseClass = inheritsMatch.Groups[1].Value;
        }

        // Detect interfaces
        var implementsMatch = Regex.Match(prompt, @"implements?\s+([A-Za-z_,\s]+)", RegexOptions.IgnoreCase);
        if (implementsMatch.Success)
        {
            var interfaces = implementsMatch.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries);
            intent.Interfaces.AddRange(interfaces);
        }

        // Detect modifiers
        if (prompt.Contains("static", StringComparison.OrdinalIgnoreCase))
        {
            intent.IsStatic = true;
        }
        if (prompt.Contains("abstract", StringComparison.OrdinalIgnoreCase))
        {
            intent.IsAbstract = true;
        }
        if (prompt.Contains("sealed", StringComparison.OrdinalIgnoreCase))
        {
            intent.IsSealed = true;
        }

        intent.Description = prompt;

        return Task.FromResult<CodeGenerationIntent>(intent);
    }

    private Task<CodeGenerationIntent> ParseTestIntentAsync(
        string prompt,
        Dictionary<string, string>? context,
        CancellationToken cancellationToken)
    {
        var intent = new TestGenerationIntent
        {
            Prompt = prompt,
            Confidence = 0.8
        };

        // Extract target class name
        var targetMatch = Regex.Match(prompt, @"(?:test|tests)\s+(?:for|the)\s+([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.IgnoreCase);
        if (targetMatch.Success)
        {
            intent.TargetClassName = targetMatch.Groups[1].Value;
            intent.TestClassName = intent.TargetClassName + "Tests";
            intent.Confidence = 0.9;
        }

        // Detect test framework
        if (prompt.Contains("nunit", StringComparison.OrdinalIgnoreCase))
        {
            intent.TestFramework = "NUnit";
        }
        else if (prompt.Contains("mstest", StringComparison.OrdinalIgnoreCase))
        {
            intent.TestFramework = "MSTest";
        }

        // Detect mocking preference
        if (prompt.Contains("no mock", StringComparison.OrdinalIgnoreCase) ||
            prompt.Contains("without mock", StringComparison.OrdinalIgnoreCase))
        {
            intent.UseMocking = false;
        }

        return Task.FromResult<CodeGenerationIntent>(intent);
    }

    private Task<CodeGenerationIntent> ParseInterfaceIntentAsync(
        string prompt,
        Dictionary<string, string>? context,
        CancellationToken cancellationToken)
    {
        // Parse as class but mark as interface
        var classIntent = ParseClassIntentAsync(prompt, context, cancellationToken).Result as ClassGenerationIntent;
        if (classIntent != null)
        {
            classIntent.Type = IntentType.GenerateInterface;
            // Interface names typically start with I
            if (!classIntent.ClassName.StartsWith("I"))
            {
                classIntent.ClassName = "I" + classIntent.ClassName;
            }
        }

        return Task.FromResult<CodeGenerationIntent>(classIntent!);
    }

    private Task<CodeGenerationIntent> ParseModelIntentAsync(
        string prompt,
        Dictionary<string, string>? context,
        CancellationToken cancellationToken)
    {
        var intent = ParseClassIntentAsync(prompt, context, cancellationToken).Result as ClassGenerationIntent;
        if (intent != null)
        {
            intent.Type = IntentType.GenerateModel;
            // Models typically don't have methods
            intent.Methods.Clear();
        }

        return Task.FromResult<CodeGenerationIntent>(intent!);
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return char.ToUpper(input[0]) + input[1..].ToLower();
    }
}
