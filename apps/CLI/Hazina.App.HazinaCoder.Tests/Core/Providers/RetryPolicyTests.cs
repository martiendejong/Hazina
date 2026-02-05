using Xunit;
using FluentAssertions;
using Hazina.App.HazinaCoder.Core.Providers;

namespace Hazina.App.HazinaCoder.Tests.Core.Providers;

public class RetryPolicyTests
{
    [Fact]
    public async Task RetryPolicy_SuccessfulOnFirstAttempt_ShouldNotRetry()
    {
        var policy = new RetryPolicy(maxRetries: 3);
        var attempts = 0;

        var result = await policy.ExecuteAsync(async () =>
        {
            attempts++;
            await Task.CompletedTask;
            return "success";
        });

        result.Should().Be("success");
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task RetryPolicy_FailsThenSucceeds_ShouldRetry()
    {
        var policy = new RetryPolicy(maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(10));
        var attempts = 0;

        var result = await policy.ExecuteAsync(async () =>
        {
            attempts++;
            await Task.CompletedTask;

            if (attempts < 3)
                throw new Exception("Transient error");

            return "success";
        });

        result.Should().Be("success");
        attempts.Should().Be(3);
    }

    [Fact]
    public async Task RetryPolicy_AlwaysFails_ShouldThrowAfterMaxRetries()
    {
        var policy = new RetryPolicy(maxRetries: 3, initialDelay: TimeSpan.FromMilliseconds(10));
        var attempts = 0;

        var act = async () => await policy.ExecuteAsync(async () =>
        {
            attempts++;
            await Task.CompletedTask;
            throw new Exception("Permanent error");
        });

        await act.Should().ThrowAsync<Exception>();
        attempts.Should().Be(3);
    }
}

public class RateLimiterTests
{
    [Fact]
    public async Task RateLimiter_BelowLimit_ShouldNotWait()
    {
        var limiter = new RateLimiter(maxRequests: 5, timeWindow: TimeSpan.FromSeconds(1));

        var sw = System.Diagnostics.Stopwatch.StartNew();

        for (int i = 0; i < 3; i++)
            await limiter.WaitAsync();

        sw.Stop();
        sw.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Fact]
    public async Task RateLimiter_ExceedsLimit_ShouldWait()
    {
        var limiter = new RateLimiter(maxRequests: 2, timeWindow: TimeSpan.FromMilliseconds(500));

        await limiter.WaitAsync();
        await limiter.WaitAsync();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await limiter.WaitAsync(); // Should wait ~500ms
        sw.Stop();

        sw.ElapsedMilliseconds.Should().BeGreaterThan(400);
    }
}
