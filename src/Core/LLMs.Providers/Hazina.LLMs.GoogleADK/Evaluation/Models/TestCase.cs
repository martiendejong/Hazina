namespace Hazina.LLMs.GoogleADK.Evaluation.Models;

/// <summary>
/// A test case for evaluating agent performance
/// </summary>
public class TestCase
{
    public string TestId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string? ExpectedOutput { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public TestCaseDifficulty Difficulty { get; set; } = TestCaseDifficulty.Medium;
}

/// <summary>
/// Test case difficulty level
/// </summary>
public enum TestCaseDifficulty
{
    Easy,
    Medium,
    Hard,
    Expert
}

/// <summary>
/// Result of executing a test case
/// </summary>
public class TestCaseResult
{
    public string TestId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string? ActualOutput { get; set; }
    public string? ExpectedOutput { get; set; }
    public bool Passed { get; set; }
    public double Score { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, double> Metrics { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Collection of test cases
/// </summary>
public class TestSuite
{
    public string SuiteId { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<TestCase> TestCases { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Result of running a test suite
/// </summary>
public class TestSuiteResult
{
    public string SuiteId { get; set; } = string.Empty;
    public string AgentId { get; set; } = string.Empty;
    public List<TestCaseResult> Results { get; set; } = new();
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public double PassRate { get; set; }
    public double AverageScore { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
}
