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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class When_every_refresh_fails_should_keep_the_fallback_view_and_drop_the_trip_at_the_cooldown
{
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan s_refreshInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan s_storeTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan s_guardTimeout = TimeSpan.FromSeconds(8);
    private readonly RoutingKey _orders = new("orders");

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
    public async Task When_every_refresh_fails_should_list_the_trip_until_the_cooldown_and_warn_once_per_refresh()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        var clock = new FakeTimeProvider();
        var store = new CountingThrowingTrippedTopicStore();
        var options = new OutboxCircuitBreakerOptions { CooldownDuration = s_cooldown, ClockSkewLeeway = TimeSpan.Zero };
        var breaker = new DistributedOutboxCircuitBreaker(store, options, timeProvider: clock);
        breaker.TripTopic(_orders);
        await BecomesTrueAsync(() => WarningCount() >= 2);
        var elapsed = TimeSpan.Zero;
        var refreshes = 0;

        // Act
        while (elapsed + s_refreshInterval < s_cooldown)
        {
            clock.Advance(s_refreshInterval);
            elapsed += s_refreshInterval;
            var warningsBefore = WarningCount();
            breaker.StartRefresh();
            refreshes++;
            await BecomesTrueAsync(() => WarningCount() > warningsBefore);
            await StaysFalseAsync(() => WarningCount() > warningsBefore + 1);
            Assert.Equal(refreshes, store.ReadCount);
            Assert.Equal(warningsBefore + 1, WarningCount());
            Assert.Equal([_orders], breaker.TrippedTopics.ToArray());
        }

        clock.Advance(s_cooldown - elapsed - TimeSpan.FromMilliseconds(100));
        var listedJustBeforeCooldown = breaker.TrippedTopics.ToArray();
        clock.Advance(TimeSpan.FromMilliseconds(100));

        // Assert
        Assert.Equal([_orders], listedJustBeforeCooldown);
        Assert.Empty(breaker.TrippedTopics);
        Assert.True(breaker.StoreStatus.IsUsingFallback);
    }

    [Fact]
    public async Task When_the_store_never_completes_should_not_block_the_caller_of_start_refresh()
    {
        // Arrange
        var store = new AbandonedCallCountingTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: new FakeTimeProvider());

        // Act
        var call = Task.Run(breaker.StartRefresh);
        var returned = await Task.WhenAny(call, Task.Delay(TimeSpan.FromSeconds(10))) == call;

        // Assert
        Assert.True(returned);
        await call;
        Assert.True(await BecomesTrueAsync(() => store.ReadCount == 1));
    }

    [Fact]
    public async Task When_a_refresh_is_abandoned_should_start_no_store_call_until_four_store_timeouts()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        var store = new AbandonedCallCountingTrippedTopicStore();
        var clock = new FakeTimeProvider();
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions { StoreTimeout = s_storeTimeout }, timeProvider: clock);
        breaker.StartRefresh();
        await BecomesTrueAsync(() => store.ReadCount == 1);
        clock.Advance(s_storeTimeout);

        // Act
        breaker.StartRefresh();
        var heldBack = await StaysFalseAsync(() => store.ReadCount > 1);
        var warningsBeforeRelease = WarningCount();
        clock.Advance(s_guardTimeout - s_storeTimeout);
        var released = await BecomesTrueAsync(() => WarningCount() > warningsBeforeRelease);
        breaker.StartRefresh();
        var reachedStore = await BecomesTrueAsync(() => store.ReadCount == 2);

        // Assert
        Assert.True(heldBack);
        Assert.True(released);
        Assert.True(reachedStore);
    }
}
