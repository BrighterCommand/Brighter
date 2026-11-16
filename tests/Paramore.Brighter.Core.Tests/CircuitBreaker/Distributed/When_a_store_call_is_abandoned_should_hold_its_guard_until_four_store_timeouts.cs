#region Licence

/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class StoreCallIsAbandonedTests
{
    private static readonly TimeSpan s_storeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_guardTimeout = TimeSpan.FromSeconds(8);
    private readonly RoutingKey _orders = new("orders");

    private static DistributedOutboxCircuitBreaker CreateBreaker(AbandonedCallCountingTrippedTopicStore store, FakeTimeProvider clock)
        => new(store, new OutboxCircuitBreakerOptions { StoreTimeout = s_storeTimeout }, timeProvider: clock);

    private static async Task<bool> CompletesAsync(Task task)
        => await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10))) == task;

    private static async Task<bool> BecomesTrueAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        return condition();
    }

    private static async Task<bool> StaysFalseAsync(Func<bool> condition)
    {
        await Task.Delay(300);
        return !condition();
    }

    private static int WarningCount()
        => TestCorrelator.GetLogEventsFromCurrentContext().Count(e => e.Level == LogEventLevel.Warning);

    [Fact]
    public async Task When_a_sweep_read_is_abandoned_should_drop_further_reads_until_four_store_timeouts()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        var unobserved = 0;
        void CountUnobserved(object? sender, UnobservedTaskExceptionEventArgs args) => Interlocked.Increment(ref unobserved);
        TaskScheduler.UnobservedTaskException += CountUnobserved;
        var store = new AbandonedCallCountingTrippedTopicStore();
        var clock = new FakeTimeProvider();
        var breaker = CreateBreaker(store, clock);
        breaker.TripTopic(_orders);
        await BecomesTrueAsync(() => store.WriteCount == 1);
        var abandoned = breaker.ReadTrippedTopicsAsync();
        clock.Advance(s_storeTimeout);
        await CompletesAsync(abandoned);
        var warningsBeforeDrop = WarningCount();

        try
        {
            // Act
            var dropping = breaker.ReadTrippedTopicsAsync();
            var returnedAtOnce = await CompletesAsync(dropping);

            // Assert
            Assert.True(returnedAtOnce);
            Assert.Equal(1, store.ReadCount);
            Assert.Equal([_orders], (await dropping).ToArray());
            Assert.Equal(warningsBeforeDrop, WarningCount());

            // Act
            clock.Advance(s_guardTimeout - s_storeTimeout);
            var released = await BecomesTrueAsync(() => WarningCount() > warningsBeforeDrop);
            var next = breaker.ReadTrippedTopicsAsync();
            clock.Advance(s_storeTimeout);
            await CompletesAsync(next);

            // Assert
            Assert.True(released);
            Assert.Equal(2, store.ReadCount);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= CountUnobserved;
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        Assert.Equal(0, unobserved);
    }

    [Fact]
    public async Task When_a_callback_write_is_abandoned_should_hold_its_guard_until_four_store_timeouts()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        var store = new AbandonedCallCountingTrippedTopicStore();
        var clock = new FakeTimeProvider();
        var breaker = CreateBreaker(store, clock);
        breaker.TripTopic(_orders);
        await BecomesTrueAsync(() => store.WriteCount == 1);
        clock.Advance(s_storeTimeout);

        // Act
        breaker.TripTopic(_orders);
        var heldBack = await StaysFalseAsync(() => store.WriteCount > 1);
        var warningsBeforeRelease = WarningCount();
        clock.Advance(s_guardTimeout - s_storeTimeout);
        var released = await BecomesTrueAsync(() => WarningCount() > warningsBeforeRelease);
        var reachedStore = await BecomesTrueAsync(() => store.WriteCount == 2);

        // Assert
        Assert.True(heldBack);
        Assert.True(released);
        Assert.True(reachedStore);
    }
}
