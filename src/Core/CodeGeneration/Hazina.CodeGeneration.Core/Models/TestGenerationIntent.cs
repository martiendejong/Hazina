namespace Hazina.CodeGeneration.Core.Models;

/// <summary>
/// Intent for generating unit tests
/// </summary>
public class TestGenerationIntent : CodeGenerationIntent
{
    /// <summary>
    /// The class or method to generate tests for
    /// </summary>
    public string TargetCode { get; set; } = string.Empty;

    /// <summary>
    /// The name of the class being tested
    /// </summary>
    public string TargetClassName { get; set; } = string.Empty;

    /// <summary>
    /// The name of the test class to generate
    /// </summary>
    public string TestClassName { get; set; } = string.Empty;

    /// <summary>
    /// Test framework to use (xUnit, NUnit, MSTest)
    /// </summary>
    public string TestFramework { get; set; } = "xUnit";

    /// <summary>
    /// Assertion library to use (FluentAssertions, Assert)
    /// </summary>
    public string AssertionLibrary { get; set; } = "FluentAssertions";

    /// <summary>
    /// Whether to use mocking framework
    /// </summary>
    public bool UseMocking { get; set; } = true;

    /// <summary>
    /// Mocking framework to use (Moq, NSubstitute)
    /// </summary>
    public string MockingFramework { get; set; } = "Moq";

    /// <summary>
    /// Specific test scenarios to cover
    /// </summary>
    public List<TestScenario> TestScenarios { get; set; } = new();

    /// <summary>
    /// Whether to generate edge case tests
    /// </summary>
    public bool GenerateEdgeCases { get; set; } = true;

    /// <summary>
    /// Whether to generate exception tests
    /// </summary>
    public bool GenerateExceptionTests { get; set; } = true;

    public TestGenerationIntent()
    {
        Type = IntentType.GenerateTests;
    }
}

/// <summary>
/// Represents a specific test scenario
/// </summary>
public class TestScenario
{
    /// <summary>
    /// Scenario name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Scenario description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Expected outcome
    /// </summary>
    public string ExpectedOutcome { get; set; } = string.Empty;

    /// <summary>
    /// Test inputs
    /// </summary>
    public Dictionary<string, string> Inputs { get; set; } = new();

    /// <summary>
    /// Whether this is a happy path scenario
    /// </summary>
    public bool IsHappyPath { get; set; } = true;
}
