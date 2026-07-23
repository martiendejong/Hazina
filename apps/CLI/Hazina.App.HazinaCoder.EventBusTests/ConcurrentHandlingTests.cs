using System.Diagnostics;
using FluentAssertions;
using Hazina.App.HazinaCoder.Core.Events;

namespace Hazina.App.HazinaCoder.EventBusTests;

public class ConcurrentHandlingTests
{
    [Fact]
    public async Task SubscribeAsync_MultipleHandlers_RunConcurrently_NotSerialized()
    {
        using var bus = new EventBus();
        const int handlerCount = 5;
        var currentlyRunning = 0;
        var maxObservedConcurrency = 0;
        var maxLock = new object();
        var completions = new List<TaskCompletionSource>();

        for (var i = 0; i < handlerCount; i++)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            completions.Add(tcs);

            bus.SubscribeAsync<UserMessageReceivedEvent>(async _ =>
            {
                var running = Interlocked.Increment(ref currentlyRunning);
                lock (maxLock)
                {
                    maxObservedConcurrency = Math.Max(maxObservedConcurrency, running);
                }

                await Task.Delay(150);

                Interlocked.Decrement(ref currentlyRunning);
                tcs.SetResult();
            });
        }

        bus.Publish(new UserMessageReceivedEvent("s1", "u1", "hi"));

        await Task.WhenAll(completions.Select(c => c.Task)).WaitAsync(TimeSpan.FromSeconds(5));

        // If handlers ran sequentially, max observed concurrency would never exceed 1.
        maxObservedConcurrency.Should().BeGreaterThan(1,
            "SubscribeAsync handlers dispatch via Task.Run and must not block each other");
    }

    [Fact]
    public async Task SubscribeAsync_MultipleHandlers_TotalElapsedTimeReflectsParallelExecution()
    {
        using var bus = new EventBus();
        const int handlerCount = 4;
        var delay = TimeSpan.FromMilliseconds(150);
        var completions = new List<TaskCompletionSource>();

        for (var i = 0; i < handlerCount; i++)
        {
            var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            completions.Add(tcs);

            bus.SubscribeAsync<UserMessageReceivedEvent>(async _ =>
            {
                await Task.Delay(delay);
                tcs.SetResult();
            });
        }

        var stopwatch = Stopwatch.StartNew();
        bus.Publish(new UserMessageReceivedEvent("s1", "u1", "hi"));
        await Task.WhenAll(completions.Select(c => c.Task)).WaitAsync(TimeSpan.FromSeconds(5));
        stopwatch.Stop();

        // Serialized execution would take roughly handlerCount * delay (~600ms).
        // Concurrent execution should take roughly one delay period, with generous
        // headroom for scheduling overhead on a shared/loaded machine.
        stopwatch.Elapsed.Should().BeLessThan(delay * handlerCount);
    }

    [Fact]
    public void Publish_WithSlowAsyncHandlers_ReturnsImmediately()
    {
        using var bus = new EventBus();
        bus.SubscribeAsync<UserMessageReceivedEvent>(async _ => await Task.Delay(2000));

        var stopwatch = Stopwatch.StartNew();
        bus.Publish(new UserMessageReceivedEvent("s1", "u1", "hi"));
        stopwatch.Stop();

        // Publish is fire-and-forget for async handlers - it must not wait for them.
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromMilliseconds(500));
    }

    [Fact]
    public async Task SubscribeAsync_HandlersForDifferentEventTypes_RunIndependently()
    {
        using var bus = new EventBus();
        var messageHandlerRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var intentHandlerRan = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        bus.SubscribeAsync<UserMessageReceivedEvent>(async _ =>
        {
            await Task.Delay(50);
            messageHandlerRan.SetResult();
        });
        bus.SubscribeAsync<IntentExtractedEvent>(async _ =>
        {
            await Task.Delay(50);
            intentHandlerRan.SetResult();
        });

        bus.Publish(new UserMessageReceivedEvent("s1", "u1", "hi"));
        bus.Publish(new IntentExtractedEvent("s1", "greet", 0.9));

        await Task.WhenAll(messageHandlerRan.Task, intentHandlerRan.Task).WaitAsync(TimeSpan.FromSeconds(5));

        messageHandlerRan.Task.IsCompletedSuccessfully.Should().BeTrue();
        intentHandlerRan.Task.IsCompletedSuccessfully.Should().BeTrue();
    }
}
