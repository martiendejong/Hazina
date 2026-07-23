using FluentAssertions;
using Hazina.App.HazinaCoder.Core.Events;

namespace Hazina.App.HazinaCoder.EventBusTests;

public class RetryTests
{
    [Fact]
    public async Task SubscribeAsyncWithRetry_HandlerFailsThenSucceeds_EventuallySucceedsWithinBudget()
    {
        using var bus = new EventBus();
        var attempts = 0;
        var succeeded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bus.SubscribeAsyncWithRetry<UserMessageReceivedEvent>(async _ =>
        {
            var attempt = Interlocked.Increment(ref attempts);
            if (attempt < 3)
            {
                throw new InvalidOperationException($"transient failure #{attempt}");
            }

            await Task.CompletedTask;
            succeeded.SetResult();
        }, maxRetries: 5, initialDelay: TimeSpan.FromMilliseconds(10));

        bus.Publish(new UserMessageReceivedEvent("s1", "u1", "hi"));

        await succeeded.Task.WaitAsync(TimeSpan.FromSeconds(10));

        attempts.Should().Be(3, "the handler should stop retrying as soon as it succeeds");
    }

    [Fact]
    public async Task SubscribeAsyncWithRetry_HandlerAlwaysFails_RetriesExactlyMaxTimesThenGivesUpWithoutCrashingBus()
    {
        using var bus = new EventBus();
        var attempts = 0;
        const int maxRetries = 3;
        var reachedFinalAttempt = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bus.SubscribeAsyncWithRetry<UserMessageReceivedEvent>(_ =>
        {
            var attempt = Interlocked.Increment(ref attempts);
            if (attempt == maxRetries)
            {
                reachedFinalAttempt.SetResult();
            }

            throw new InvalidOperationException($"permanent failure #{attempt}");
        }, maxRetries: maxRetries, initialDelay: TimeSpan.FromMilliseconds(10));

        var publish = () => bus.Publish(new UserMessageReceivedEvent("s1", "u1", "hi"));

        // Publish itself must never throw, even though every retry attempt fails -
        // SubscribeAsyncWithRetry is fire-and-forget from the publisher's perspective.
        publish.Should().NotThrow();

        await reachedFinalAttempt.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Give the final (post-retry-budget) exception a moment to be caught internally.
        await Task.Delay(100);

        attempts.Should().Be(maxRetries, "the handler must not be retried more than maxRetries times");
    }

    [Fact]
    public async Task SubscribeAsyncWithRetry_HandlerSucceedsFirstTry_OnlyInvokedOnce()
    {
        using var bus = new EventBus();
        var attempts = 0;
        var succeeded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bus.SubscribeAsyncWithRetry<UserMessageReceivedEvent>(async _ =>
        {
            Interlocked.Increment(ref attempts);
            await Task.CompletedTask;
            succeeded.SetResult();
        }, maxRetries: 5, initialDelay: TimeSpan.FromMilliseconds(10));

        bus.Publish(new UserMessageReceivedEvent("s1", "u1", "hi"));

        await succeeded.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);

        attempts.Should().Be(1, "a handler that succeeds immediately should never be retried");
    }

    [Fact]
    public async Task SubscribeAsyncWithRetry_DoesNotAffectOtherSubscribersOfTheSameEvent()
    {
        using var bus = new EventBus();
        var otherHandlerInvocations = 0;
        var failingHandlerGaveUp = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bus.Subscribe<UserMessageReceivedEvent>(_ => Interlocked.Increment(ref otherHandlerInvocations));

        var attempts = 0;
        bus.SubscribeAsyncWithRetry<UserMessageReceivedEvent>(_ =>
        {
            var attempt = Interlocked.Increment(ref attempts);
            if (attempt == 2)
            {
                failingHandlerGaveUp.SetResult();
            }

            throw new InvalidOperationException("always fails");
        }, maxRetries: 2, initialDelay: TimeSpan.FromMilliseconds(10));

        bus.Publish(new UserMessageReceivedEvent("s1", "u1", "hi"));

        await failingHandlerGaveUp.Task.WaitAsync(TimeSpan.FromSeconds(10));

        otherHandlerInvocations.Should().Be(1);
    }
}
