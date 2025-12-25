using Common.Models.DTO;
using FluentAssertions;

namespace DevGPT.GenerationTools.Common.Models.Tests;

public class ResultTests
{
    [Fact]
    public void Constructor_ShouldCreateResult()
    {
        // Arrange & Act
        var result = new Result<string>();

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeNull();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Success_ShouldBeTrue_WhenSuccessIsSet()
    {
        // Arrange & Act
        var result = new Result<string>
        {
            Success = true,
            Value = "test value"
        };

        // Assert
        result.Success.Should().BeTrue();
        result.Value.Should().Be("test value");
    }

    [Fact]
    public void Errors_ShouldContainErrors_WhenAdded()
    {
        // Arrange
        var result = new Result<string>();

        // Act
        result.Errors.Add("Error 1");
        result.Errors.Add("Error 2");

        // Assert
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain("Error 1");
        result.Errors.Should().Contain("Error 2");
    }

    [Fact]
    public void FailureResult_ShouldHaveSuccessFalse()
    {
        // Arrange & Act
        var result = new Result<int>
        {
            Success = false,
            Errors = new List<string> { "Failure occurred" }
        };

        // Assert
        result.Success.Should().BeFalse();
        result.Errors.Should().HaveCount(1);
        result.Errors[0].Should().Be("Failure occurred");
    }
}
