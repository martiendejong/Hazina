using Hazina.CodeGeneration.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;

namespace Hazina.CodeGeneration.Core.Templates;

/// <summary>
/// Template for generating unit tests
/// </summary>
public class TestTemplate : ICodeTemplate
{
    private readonly ILogger _logger;

    public IntentType SupportedIntentType => IntentType.GenerateTests;

    public TestTemplate(ILogger logger)
    {
        _logger = logger;
    }

    public bool CanHandle(CodeGenerationIntent intent)
    {
        return intent is TestGenerationIntent;
    }

    public Task<string> GenerateAsync(
        CodeGenerationIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (intent is not TestGenerationIntent testIntent)
        {
            throw new ArgumentException("Intent must be TestGenerationIntent", nameof(intent));
        }

        var code = new StringBuilder();

        // Add using statements
        code.AppendLine("using Xunit;");

        if (testIntent.AssertionLibrary == "FluentAssertions")
        {
            code.AppendLine("using FluentAssertions;");
        }

        if (testIntent.UseMocking && testIntent.MockingFramework == "Moq")
        {
            code.AppendLine("using Moq;");
        }

        code.AppendLine();

        // Add namespace if provided
        if (!string.IsNullOrWhiteSpace(testIntent.TargetNamespace))
        {
            code.AppendLine($"namespace {testIntent.TargetNamespace}.Tests;");
            code.AppendLine();
        }

        // Add test class
        code.AppendLine("/// <summary>");
        code.AppendLine($"/// Unit tests for <see cref=\"{testIntent.TargetClassName}\"/>");
        code.AppendLine("/// </summary>");
        code.AppendLine($"public class {testIntent.TestClassName}");
        code.AppendLine("{");

        // Add setup field if using mocking
        if (testIntent.UseMocking)
        {
            code.AppendLine($"    private readonly {testIntent.TargetClassName}? _sut;");
            code.AppendLine();
        }

        // Add constructor if using mocking
        if (testIntent.UseMocking)
        {
            code.AppendLine($"    public {testIntent.TestClassName}()");
            code.AppendLine("    {");
            code.AppendLine($"        // Initialize system under test");
            code.AppendLine($"        _sut = new {testIntent.TargetClassName}();");
            code.AppendLine("    }");
            code.AppendLine();
        }

        // Add test scenarios
        if (testIntent.TestScenarios.Any())
        {
            foreach (var scenario in testIntent.TestScenarios)
            {
                code.AppendLine(GenerateTestMethod(scenario, testIntent));
                code.AppendLine();
            }
        }
        else
        {
            // Generate default happy path test
            code.AppendLine(GenerateDefaultTest(testIntent));
            code.AppendLine();

            // Generate edge case test if requested
            if (testIntent.GenerateEdgeCases)
            {
                code.AppendLine(GenerateEdgeCaseTest(testIntent));
                code.AppendLine();
            }

            // Generate exception test if requested
            if (testIntent.GenerateExceptionTests)
            {
                code.AppendLine(GenerateExceptionTest(testIntent));
            }
        }

        code.AppendLine("}");

        return Task.FromResult(code.ToString());
    }

    private string GenerateTestMethod(TestScenario scenario, TestGenerationIntent testIntent)
    {
        var code = new StringBuilder();

        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// {scenario.Description}");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    [Fact]");
        code.AppendLine($"    public void {SanitizeMethodName(scenario.Name)}()");
        code.AppendLine("    {");
        code.AppendLine("        // Arrange");
        code.AppendLine($"        // TODO: Setup test data");
        code.AppendLine();
        code.AppendLine("        // Act");
        code.AppendLine($"        // TODO: Execute the method under test");
        code.AppendLine();
        code.AppendLine("        // Assert");

        if (testIntent.AssertionLibrary == "FluentAssertions")
        {
            code.AppendLine($"        // result.Should().Be(expected);");
        }
        else
        {
            code.AppendLine($"        // Assert.Equal(expected, result);");
        }

        code.AppendLine("    }");

        return code.ToString();
    }

    private string GenerateDefaultTest(TestGenerationIntent testIntent)
    {
        var code = new StringBuilder();

        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Tests that {testIntent.TargetClassName} works correctly with valid input");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    [Fact]");
        code.AppendLine($"    public void {testIntent.TargetClassName}_WithValidInput_ShouldSucceed()");
        code.AppendLine("    {");
        code.AppendLine("        // Arrange");
        code.AppendLine($"        var sut = new {testIntent.TargetClassName}();");
        code.AppendLine();
        code.AppendLine("        // Act");
        code.AppendLine($"        // var result = sut.MethodUnderTest();");
        code.AppendLine();
        code.AppendLine("        // Assert");

        if (testIntent.AssertionLibrary == "FluentAssertions")
        {
            code.AppendLine($"        // result.Should().NotBeNull();");
        }
        else
        {
            code.AppendLine($"        // Assert.NotNull(result);");
        }

        code.AppendLine("    }");

        return code.ToString();
    }

    private string GenerateEdgeCaseTest(TestGenerationIntent testIntent)
    {
        var code = new StringBuilder();

        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Tests that {testIntent.TargetClassName} handles edge cases correctly");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    [Fact]");
        code.AppendLine($"    public void {testIntent.TargetClassName}_WithEdgeCase_ShouldHandleCorrectly()");
        code.AppendLine("    {");
        code.AppendLine("        // Arrange");
        code.AppendLine($"        var sut = new {testIntent.TargetClassName}();");
        code.AppendLine("        // TODO: Setup edge case data (null, empty, boundary values)");
        code.AppendLine();
        code.AppendLine("        // Act");
        code.AppendLine($"        // var result = sut.MethodUnderTest(edgeCaseInput);");
        code.AppendLine();
        code.AppendLine("        // Assert");
        code.AppendLine($"        // TODO: Verify edge case handling");
        code.AppendLine("    }");

        return code.ToString();
    }

    private string GenerateExceptionTest(TestGenerationIntent testIntent)
    {
        var code = new StringBuilder();

        code.AppendLine("    /// <summary>");
        code.AppendLine($"    /// Tests that {testIntent.TargetClassName} throws exception for invalid input");
        code.AppendLine("    /// </summary>");
        code.AppendLine("    [Fact]");
        code.AppendLine($"    public void {testIntent.TargetClassName}_WithInvalidInput_ShouldThrowException()");
        code.AppendLine("    {");
        code.AppendLine("        // Arrange");
        code.AppendLine($"        var sut = new {testIntent.TargetClassName}();");
        code.AppendLine();
        code.AppendLine("        // Act & Assert");

        if (testIntent.AssertionLibrary == "FluentAssertions")
        {
            code.AppendLine("        var act = () => sut.MethodUnderTest(invalidInput);");
            code.AppendLine("        act.Should().Throw<ArgumentException>();");
        }
        else
        {
            code.AppendLine("        Assert.Throws<ArgumentException>(() => sut.MethodUnderTest(invalidInput));");
        }

        code.AppendLine("    }");

        return code.ToString();
    }

    private string SanitizeMethodName(string name)
    {
        // Remove special characters and ensure valid method name
        var sanitized = new string(name.Where(c => char.IsLetterOrDigit(c) || c == '_').ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "TestScenario" : sanitized;
    }
}
