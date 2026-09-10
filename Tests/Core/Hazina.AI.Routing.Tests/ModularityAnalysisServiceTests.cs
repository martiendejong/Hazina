using FluentAssertions;
using Hazina.AI.Routing.Services;

namespace Hazina.AI.Routing.Tests;

public class ModularityAnalysisServiceTests
{
    private readonly ModularityAnalysisService _service = new();

    [Fact]
    public void CalculateScore_SequentialTask_ScoresLow()
    {
        // "Write a blog post" is sequential creative work
        var score = _service.CalculateScore("Write a blog post about artificial intelligence");

        score.Should().BeLessThan(0.5);
    }

    [Fact]
    public void CalculateScore_ParallelTask_ScoresHigh()
    {
        // "Analyze 5 markets" has independent sub-tasks
        var score = _service.CalculateScore(
            "Analyze 5 different markets: US, EU, Asia, Africa, South America. " +
            "Compare each market independently and evaluate their growth potential.");

        score.Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void CalculateScore_EmptyInput_ReturnsZero()
    {
        _service.CalculateScore("").Should().Be(0.0);
        _service.CalculateScore("  ").Should().Be(0.0);
    }

    [Fact]
    public void CalculateScore_NumberedList_IncreasesScore()
    {
        var withList = _service.CalculateScore(
            "Analyze the following:\n1. Market size\n2. Competition\n3. Growth rate\n4. Entry barriers");

        var withoutList = _service.CalculateScore("Analyze the market.");

        withList.Should().BeGreaterThan(withoutList);
    }

    [Fact]
    public void CalculateScore_MultipleQuestions_IncreasesScore()
    {
        var multiQuestion = _service.CalculateScore(
            "What is the market size? What are the risks? Who are the competitors?");

        var singleQuestion = _service.CalculateScore("What is the market size?");

        multiQuestion.Should().BeGreaterThan(singleQuestion);
    }

    [Fact]
    public void CalculateScore_StepByStep_ScoresLow()
    {
        var score = _service.CalculateScore(
            "Step by step, first create the database, then build the API, " +
            "after that create the frontend, following with deployment.");

        score.Should().BeLessThan(0.4);
    }

    [Fact]
    public void CalculateScore_AlwaysReturnsWithinRange()
    {
        var inputs = new[]
        {
            "Simple task",
            "Analyze, compare, evaluate, review, assess multiple categories from different perspectives independently",
            "Step by step first then after that depends on based on the result following sequential in order write create generate compose draft pipeline workflow chain"
        };

        foreach (var input in inputs)
        {
            var score = _service.CalculateScore(input);
            score.Should().BeGreaterThanOrEqualTo(0.0);
            score.Should().BeLessThanOrEqualTo(1.0);
        }
    }
}
