#region License

/* The MIT License (MIT)
Copyright © 2025 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the “Software”), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Paramore.Brighter.Core.Tests.Sweeping.TestDoubles;
using Paramore.Brighter.Outbox.Hosting;
using Xunit;

namespace Paramore.Brighter.Core.Tests.Sweeping;

public class SweepTripWriteTests
{
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(10);
    private static readonly OutboxCircuitBreakerOptions s_options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };

    [Fact]
    public async Task When_a_sweep_trips_a_topic_should_write_it_before_the_lock_is_released()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var shared = new InMemoryTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(new AsynchronouslyWritingTrippedTopicStore(shared), s_options, timeProvider: clock);
        using var outbox = new DispatchFailingOutbox(breaker);
        outbox.AddUndispatched(outbox.Orders);
        var sweepLock = new ReleaseObservingDistributedLock(new InMemoryLock(), () => StoreHoldsAsync(shared, clock));
        var sweeper = NewSweeper(outbox, sweepLock, clock);

        // Act
        await sweeper.StartAsync(CancellationToken.None);
        var released = SpinWait.SpinUntil(() => sweepLock.Released, s_timeout);
        await sweeper.StopAsync(CancellationToken.None);
        sweeper.Dispose();

        // Assert
        Assert.True(released, "The sweeper never released the lock");
        Assert.True(sweepLock.ObservationHeldAtRelease, "The store did not hold orders before ReleaseLockAsync");
    }

    [Fact]
    public async Task When_dispatch_throws_after_tripping_a_topic_should_write_the_trip_and_propagate_the_dispatch_exception()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var shared = new InMemoryTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(new AsynchronouslyWritingTrippedTopicStore(shared), s_options, timeProvider: clock);
        using var outbox = new DispatchFailingOutbox(breaker);
        outbox.AddUndispatched(outbox.Orders);
        outbox.AddUndispatched(outbox.Unrouted);

        // Act
        var thrown = await Record.ExceptionAsync(() =>
            outbox.Mediator.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, new RequestContext()));

        // Assert
        Assert.IsType<ConfigurationException>(thrown);
        Assert.True(await StoreHoldsAsync(shared, clock), "The store did not hold orders when dispatch threw");
    }

    [Fact]
    public async Task When_dispatch_throws_after_tripping_a_topic_and_the_store_cannot_be_written_should_propagate_the_dispatch_exception()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var store = new ThrowingOnWriteTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(store, s_options, timeProvider: clock);
        using var outbox = new DispatchFailingOutbox(breaker);
        outbox.AddUndispatched(outbox.Orders);
        outbox.AddUndispatched(outbox.Unrouted);

        // Act
        var thrown = await Record.ExceptionAsync(() =>
            outbox.Mediator.ClearOutstandingFromOutboxAsync(10, TimeSpan.Zero, false, new RequestContext()));

        // Assert
        Assert.Equal(1, store.WriteCount);
        Assert.IsType<ConfigurationException>(thrown);
    }

    private static async Task<bool> StoreHoldsAsync(InMemoryTrippedTopicStore store, FakeTimeProvider clock)
        => (await store.ReadLiveAsync(clock.GetUtcNow(), s_options.CooldownDuration)).Count > 0;

    private static TimedOutboxSweeper NewSweeper(DispatchFailingOutbox outbox, IDistributedLock sweepLock, FakeTimeProvider clock)
        => new(
            outbox.ScopeFactory,
            sweepLock,
            new TimedOutboxSweeperOptions { TimerInterval = 5, MinimumMessageAge = TimeSpan.Zero },
            timeProvider: clock);
}
