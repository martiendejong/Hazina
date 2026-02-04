using Hazina.App.HazinaCoder.Core.Events;
using Hazina.App.HazinaCoder.Core.Providers;
using Spectre.Console;

namespace Hazina.App.HazinaCoder.Core.Testing;

/// <summary>
/// Automatic test generation system
/// Analyzes code and generates comprehensive unit tests
/// </summary>
public class TestGenerator
{
    private readonly ProviderRouter _providerRouter;
    private readonly IEventBus? _eventBus;
    private readonly TestFramework _framework;

    public TestGenerator(
        ProviderRouter providerRouter,
        TestFramework framework = TestFramework.XUnit,
        IEventBus? eventBus = null)
    {
        _providerRouter = providerRouter;
        _framework = framework;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Generate tests for a class
    /// </summary>
    public async Task<TestGenerationResult> GenerateTestsAsync(
        string sourceCode,
        TestGenerationOptions? options = null)
    {
        options ??= new TestGenerationOptions();

        var startTime = DateTime.UtcNow;
        _eventBus?.Publish(new TestGenerationStartedEvent(options.ClassName ?? "Unknown"));

        try
        {
            // Build prompt for LLM
            var prompt = BuildTestGenerationPrompt(sourceCode, options);

            // Generate tests using LLM
            var request = new LLMRequest
            {
                Model = options.Model ?? "claude-3-5-sonnet-20241022",
                MaxTokens = options.MaxTokens,
                Temperature = 0.3, // Lower temperature for more consistent code generation
                SystemPrompt = GetSystemPrompt(),
                Messages = new List<LLMMessage>
                {
                    new() { Role = "user", Content = prompt }
                }
            };

            var response = await _providerRouter.GenerateAsync(request);

            if (!response.Success)
            {
                return new TestGenerationResult
                {
                    Success = false,
                    Error = response.Error,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Extract test code from response
            var testCode = ExtractCodeFromResponse(response.Content);

            // Analyze generated tests
            var analysis = AnalyzeGeneratedTests(testCode);

            var result = new TestGenerationResult
            {
                Success = true,
                TestCode = testCode,
                TestCount = analysis.TestCount,
                CoverageEstimate = analysis.Coverage,
                Framework = _framework.ToString(),
                Duration = DateTime.UtcNow - startTime,
                LLMCost = response.Usage.Cost
            };

            _eventBus?.Publish(new TestGenerationCompletedEvent(
                options.ClassName ?? "Unknown",
                analysis.TestCount,
                result.Duration));

            return result;
        }
        catch (Exception ex)
        {
            _eventBus?.Publish(new TestGenerationFailedEvent(
                options.ClassName ?? "Unknown",
                ex));

            return new TestGenerationResult
            {
                Success = false,
                Error = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Generate tests for multiple classes
    /// </summary>
    public async Task<List<TestGenerationResult>> GenerateBatchTestsAsync(
        List<string> sourceFiles,
        TestGenerationOptions? options = null)
    {
        var results = new List<TestGenerationResult>();

        AnsiConsole.Progress()
            .Start(ctx =>
            {
                var task = ctx.AddTask($"Generating tests for {sourceFiles.Count} files");

                foreach (var file in sourceFiles)
                {
                    var sourceCode = File.ReadAllText(file);
                    var className = Path.GetFileNameWithoutExtension(file);

                    var fileOptions = options ?? new TestGenerationOptions();
                    fileOptions.ClassName = className;

                    var result = GenerateTestsAsync(sourceCode, fileOptions).Result;
                    results.Add(result);

                    task.Increment(100.0 / sourceFiles.Count);
                }

                return Task.CompletedTask;
            });

        return results;
    }

    /// <summary>
    /// Build test generation prompt
    /// </summary>
    private string BuildTestGenerationPrompt(string sourceCode, TestGenerationOptions options)
    {
        var frameworkInfo = _framework switch
        {
            TestFramework.XUnit => "xUnit with [Fact] and [Theory] attributes",
            TestFramework.NUnit => "NUnit with [Test] and [TestCase] attributes",
            TestFramework.MSTest => "MSTest with [TestMethod] and [DataTestMethod] attributes",
            _ => "xUnit"
        };

        var prompt = $@"Generate comprehensive unit tests for the following C# code using {frameworkInfo}.

Source Code:
```csharp
{sourceCode}
```

Requirements:
- Test framework: {frameworkInfo}
- Generate tests for ALL public methods
- Include edge cases (null, empty, boundary values)
- Use {(options.GenerateMocks ? "mocks for dependencies (Moq library)" : "real dependencies where possible")}
- {(options.IncludeParameterizedTests ? "Use parameterized tests (Theory/TestCase) for multiple scenarios" : "Use simple tests (Fact/Test)")}
- Test coverage goal: {options.TargetCoverage}%
- {(options.IncludeArrange ? "Follow Arrange-Act-Assert pattern" : "Keep tests concise")}

Output ONLY the complete test class code, no explanations.
Include all necessary using statements.
Use descriptive test names following the pattern: MethodName_Scenario_ExpectedResult.
";

        return prompt;
    }

    /// <summary>
    /// Get system prompt for test generation
    /// </summary>
    private string GetSystemPrompt()
    {
        return @"You are an expert software engineer specialized in writing high-quality unit tests.
You generate comprehensive, maintainable tests that follow best practices:
- Test one thing at a time
- Use descriptive test names
- Follow AAA (Arrange-Act-Assert) pattern
- Cover edge cases and error conditions
- Use appropriate assertions
- Mock external dependencies
- Write self-documenting tests

Generate production-ready test code that requires minimal manual editing.";
    }

    /// <summary>
    /// Extract code from LLM response
    /// </summary>
    private string ExtractCodeFromResponse(string response)
    {
        // Try to extract code from markdown code blocks
        var codeBlockStart = response.IndexOf("```csharp");
        if (codeBlockStart != -1)
        {
            var codeStart = response.IndexOf('\n', codeBlockStart) + 1;
            var codeEnd = response.IndexOf("```", codeStart);

            if (codeEnd != -1)
            {
                return response[codeStart..codeEnd].Trim();
            }
        }

        // If no code block, look for class definition
        var classStart = response.IndexOf("public class");
        if (classStart != -1)
        {
            return response[classStart..].Trim();
        }

        // Return as-is if no markers found
        return response.Trim();
    }

    /// <summary>
    /// Analyze generated tests
    /// </summary>
    private TestAnalysis AnalyzeGeneratedTests(string testCode)
    {
        // Count test methods
        var testCount = _framework switch
        {
            TestFramework.XUnit => CountOccurrences(testCode, "[Fact]") + CountOccurrences(testCode, "[Theory]"),
            TestFramework.NUnit => CountOccurrences(testCode, "[Test]") + CountOccurrences(testCode, "[TestCase"),
            TestFramework.MSTest => CountOccurrences(testCode, "[TestMethod]") + CountOccurrences(testCode, "[DataTestMethod]"),
            _ => 0
        };

        // Estimate coverage (rough heuristic based on test count and assertions)
        var assertionCount = CountOccurrences(testCode, "Assert.") +
                            CountOccurrences(testCode, ".Should()") +
                            CountOccurrences(testCode, "Verify(");

        var coverage = Math.Min(100, testCount * 10 + assertionCount * 2);

        return new TestAnalysis
        {
            TestCount = testCount,
            Coverage = coverage
        };
    }

    /// <summary>
    /// Count occurrences of substring
    /// </summary>
    private int CountOccurrences(string text, string substring)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(substring, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }
}

/// <summary>
/// Test framework
/// </summary>
public enum TestFramework
{
    XUnit,
    NUnit,
    MSTest
}

/// <summary>
/// Test generation options
/// </summary>
public class TestGenerationOptions
{
    public string? ClassName { get; set; }
    public string Model { get; set; } = "claude-3-5-sonnet-20241022";
    public int MaxTokens { get; set; } = 4000;
    public bool GenerateMocks { get; set; } = true;
    public bool IncludeParameterizedTests { get; set; } = true;
    public bool IncludeArrange { get; set; } = true;
    public int TargetCoverage { get; set; } = 80;
}

/// <summary>
/// Test generation result
/// </summary>
public class TestGenerationResult
{
    public bool Success { get; set; }
    public string TestCode { get; set; } = string.Empty;
    public int TestCount { get; set; }
    public int CoverageEstimate { get; set; }
    public string Framework { get; set; } = string.Empty;
    public string? Error { get; set; }
    public TimeSpan Duration { get; set; }
    public double LLMCost { get; set; }
}

/// <summary>
/// Test analysis result
/// </summary>
internal class TestAnalysis
{
    public int TestCount { get; set; }
    public int Coverage { get; set; }
}
