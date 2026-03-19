using Hazina.LLMs.Resilience;
using System.Net;

namespace Hazina.LLMs.Client.Tests.Resilience;

public class ExponentialBackoffStrategyTests
{
    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        var strategy = new ExponentialBackoffStrategy(
            maxRetries: 5,
            baseDelay: TimeSpan.FromMilliseconds(500),
            maxDelay: TimeSpan.FromSeconds(10),
            jitterFactor: 0.1);

        Assert.Equal(5, strategy.MaxRetries);
    }

    [Fact]
    public void Constructor_WithNegativeMaxRetries_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExponentialBackoffStrategy(maxRetries: -1));
    }

    [Fact]
    public void Constructor_WithInvalidJitterFactor_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ExponentialBackoffStrategy(jitterFactor: 1.5));
    }

    [Fact]
    public void ShouldRetry_WithRateLimitError_ReturnsTrue()
    {
        var strategy = new ExponentialBackoffStrategy();
        var exception = new HttpRequestException(
            "Rate limit exceeded",
            null,
            HttpStatusCode.TooManyRequests);

        Assert.True(strategy.ShouldRetry(exception, 1));
    }

    [Fact]
    public void ShouldRetry_WithServerError_ReturnsTrue()
    {
        var strategy = new ExponentialBackoffStrategy();
        var exception = new HttpRequestException(
            "Server error",
            null,
            HttpStatusCode.InternalServerError);

        Assert.True(strategy.ShouldRetry(exception, 1));
    }

    [Fact]
    public void ShouldRetry_WithClientError_ReturnsFalse()
    {
        var strategy = new ExponentialBackoffStrategy();
        var exception = new HttpRequestException(
            "Bad request",
            null,
            HttpStatusCode.BadRequest);

        Assert.False(strategy.ShouldRetry(exception, 1));
    }

    [Fact]
    public void ShouldRetry_ExceedingMaxRetries_ReturnsFalse()
    {
        var strategy = new ExponentialBackoffStrategy(maxRetries: 3);
        var exception = new HttpRequestException(
            "Rate limit exceeded",
            null,
            HttpStatusCode.TooManyRequests);

        Assert.False(strategy.ShouldRetry(exception, 4));
    }

    [Fact]
    public void GetDelay_FollowsExponentialPattern()
    {
        var strategy = new ExponentialBackoffStrategy(
            baseDelay: TimeSpan.FromSeconds(1),
            jitterFactor: 0); // No jitter for predictable testing

        var delay1 = strategy.GetDelay(1);
        var delay2 = strategy.GetDelay(2);
        var delay3 = strategy.GetDelay(3);

        // Should approximately double each time
        Assert.InRange(delay1.TotalSeconds, 0.9, 1.1); // ~1s
        Assert.InRange(delay2.TotalSeconds, 1.9, 2.1); // ~2s
        Assert.InRange(delay3.TotalSeconds, 3.9, 4.1); // ~4s
    }

    [Fact]
    public void GetDelay_RespectsMaxDelay()
    {
        var strategy = new ExponentialBackoffStrategy(
            baseDelay: TimeSpan.FromSeconds(10),
            maxDelay: TimeSpan.FromSeconds(15),
            jitterFactor: 0);

        var delay10 = strategy.GetDelay(10); // Would be 10 * 2^9 = 5120s without cap

        Assert.True(delay10 <= TimeSpan.FromSeconds(15));
    }
}
