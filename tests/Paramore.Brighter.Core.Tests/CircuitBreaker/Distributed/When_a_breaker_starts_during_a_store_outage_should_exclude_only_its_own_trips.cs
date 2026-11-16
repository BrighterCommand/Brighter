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
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class When_a_breaker_starts_during_a_store_outage_should_exclude_only_its_own_trips
{
    private readonly RoutingKey _orders = new("orders");
    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryTrippedTopicStore _shared = new();
    private readonly OutboxCircuitBreakerOptions _options = new() { CooldownDuration = TimeSpan.FromSeconds(50), ClockSkewLeeway = TimeSpan.Zero };

    [Fact]
    public async Task When_a_breaker_starts_while_the_store_cannot_be_read_should_exclude_no_topic_and_log_a_warning()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        var store = new ReadsFailingOnDemandTrippedTopicStore(_shared) { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, _options, timeProvider: _clock);

        // Act
        var topics = await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.Empty(topics);
        Assert.True(breaker.StoreStatus.IsUsingFallback);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(),
            e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("InvalidOperationException"));
    }

    [Fact]
    public async Task When_the_store_can_be_read_again_should_use_the_stores_trips()
    {
        // Arrange
        var store = new ReadsFailingOnDemandTrippedTopicStore(_shared) { FailReads = true };
        var breaker = new DistributedOutboxCircuitBreaker(store, _options, timeProvider: _clock);
        var other = new DistributedOutboxCircuitBreaker(_shared, _options, timeProvider: _clock);
        other.TripTopic(_orders);
        await WaitForWritesAsync(other);
        await breaker.ReadTrippedTopicsAsync();

        // Act
        store.FailReads = false;
        var topics = await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.Equal([_orders], topics.ToArray());
        Assert.False(breaker.StoreStatus.IsUsingFallback);
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
