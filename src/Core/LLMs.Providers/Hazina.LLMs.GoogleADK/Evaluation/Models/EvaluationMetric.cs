namespace Hazina.LLMs.GoogleADK.Evaluation.Models;

/// <summary>
/// Base class for evaluation metrics
/// </summary>
public abstract class EvaluationMetric
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double MinValue { get; set; } = 0.0;
    public double MaxValue { get; set; } = 1.0;

    /// <summary>
    /// Calculate metric value
    /// </summary>
    public abstract double Calculate(string expected, string actual);
}

/// <summary>
/// Exact match metric
/// </summary>
public class ExactMatchMetric : EvaluationMetric
{
    public ExactMatchMetric()
    {
        Name = "ExactMatch";
        Description = "Checks if output exactly matches expected";
    }

    public override double Calculate(string expected, string actual)
    {
        return expected == actual ? 1.0 : 0.0;
    }
}

/// <summary>
/// Contains match metric
/// </summary>
public class ContainsMetric : EvaluationMetric
{
    public ContainsMetric()
    {
        Name = "Contains";
        Description = "Checks if output contains expected substring";
    }

    public override double Calculate(string expected, string actual)
    {
        return actual.Contains(expected, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
    }
}

/// <summary>
/// Similarity metric using basic string similarity
/// </summary>
public class SimilarityMetric : EvaluationMetric
{
    public SimilarityMetric()
    {
        Name = "Similarity";
        Description = "Calculates string similarity (0-1)";
    }

    public override double Calculate(string expected, string actual)
    {
        if (string.IsNullOrEmpty(expected) && string.IsNullOrEmpty(actual))
            return 1.0;

        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(actual))
            return 0.0;

        // Simple Jaccard similarity on words
        var expectedWords = expected.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var actualWords = actual.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        var intersection = expectedWords.Intersect(actualWords).Count();
        var union = expectedWords.Union(actualWords).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }
}

/// <summary>
/// Length-based metric
/// </summary>
public class LengthMetric : EvaluationMetric
{
    public LengthMetric()
    {
        Name = "Length";
        Description = "Evaluates output length relative to expected";
        MinValue = 0.0;
        MaxValue = double.MaxValue;
    }

    public override double Calculate(string expected, string actual)
    {
        return actual.Length;
    }
}

/// <summary>
/// Performance metrics
/// </summary>
public class PerformanceMetrics
{
    public double LatencyMs { get; set; }
    public double TokensPerSecond { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int TotalTokens { get; set; }
    public double Cost { get; set; }
    public double MemoryUsageMb { get; set; }
}

/// <summary>
/// Aggregate metrics for benchmark
/// </summary>
public class BenchmarkMetrics
{
    public double AverageLatencyMs { get; set; }
    public double MedianLatencyMs { get; set; }
    public double P95LatencyMs { get; set; }
    public double P99LatencyMs { get; set; }
    public double AverageScore { get; set; }
    public double PassRate { get; set; }
    public int TotalTests { get; set; }
    public int PassedTests { get; set; }
    public int FailedTests { get; set; }
    public double TotalCost { get; set; }
    public int TotalTokens { get; set; }
}
