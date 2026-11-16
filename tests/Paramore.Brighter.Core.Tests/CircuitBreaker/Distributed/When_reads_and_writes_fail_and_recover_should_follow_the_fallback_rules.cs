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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class When_reads_and_writes_fail_and_recover_should_follow_the_fallback_rules
{
    private readonly RoutingKey _orders = new("orders");
    private readonly RoutingKey _payments = new("payments");
    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryTrippedTopicStore _shared = new();
    private readonly OutboxCircuitBreakerOptions _options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };

    [Fact]
    public async Task When_reads_fail_and_then_recover_should_report_fallback_until_a_read_succeeds_and_merge_the_stores_trips()
    {
        // Arrange
        var store = new ReadsFailingOnDemandTrippedTopicStore(_shared) { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, _options, timeProvider: _clock);
        var other = new DistributedOutboxCircuitBreaker(_shared, _options, timeProvider: _clock);
        other.TripTopic(_payments);
        await WaitForWritesAsync(other);
        breaker.TripTopic(_orders);
        await breaker.ReadTrippedTopicsAsync();
        Assert.True(breaker.StoreStatus.IsUsingFallback);

        // Act
        store.FailReads = false;
        var topics = await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.Equal([_orders, _payments], topics.OrderBy(topic => topic.Value).ToArray());
        Assert.False(breaker.StoreStatus.IsUsingFallback);
    }

    [Fact]
    public async Task When_reads_keep_failing_then_recover_should_keep_the_last_failure_until_fallback_ends()
    {
        // Arrange
        var store = new ReadsFailingOnDemandTrippedTopicStore(_shared) { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, _options, timeProvider: _clock);
        await breaker.ReadTrippedTopicsAsync();
        await breaker.ReadTrippedTopicsAsync();
        var failureWhileFailing = breaker.StoreStatus.LastFailure;

        // Act
        store.FailReads = false;
        await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.Equal("InvalidOperationException", failureWhileFailing);
        Assert.Null(breaker.StoreStatus.LastFailure);
        Assert.False(breaker.StoreStatus.IsUsingFallback);
    }

    [Fact]
    public async Task When_reads_recover_should_let_the_later_stamp_win_over_a_local_trip()
    {
        // Arrange
        var store = new ReadsFailingOnDemandTrippedTopicStore(_shared) { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, _options, timeProvider: _clock);
        var other = new DistributedOutboxCircuitBreaker(_shared, _options, timeProvider: _clock);
        breaker.TripTopic(_orders);
        await WaitForWritesAsync(breaker);
        _clock.Advance(TimeSpan.FromSeconds(30));
        other.TripTopic(_orders);
        await WaitForWritesAsync(other);
        _clock.Advance(TimeSpan.FromSeconds(30));
        await breaker.ReadTrippedTopicsAsync();
        Assert.Empty(breaker.TrippedTopics);

        // Act
        store.FailReads = false;
        var topics = await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.Equal([_orders], topics.ToArray());
    }

    private static async Task WaitForWritesAsync(DistributedOutboxCircuitBreaker breaker)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
            if (!breaker.StoreStatus.IsUsingFallback && breaker.TrippedTopics.Any())
                return;
        }
    }
}
