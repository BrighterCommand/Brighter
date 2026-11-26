#region Licence
/* The MIT License (MIT)
Copyright © 2026 Ian Cooper <ian_hammond_cooper@yahoo.co.uk>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE. */
#endregion

using System;

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Serilog.Events;
using Serilog.Sinks.TestCorrelator;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class FutureStampedTripTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_leeway = TimeSpan.FromSeconds(5);
    private readonly RoutingKey _orders = new("orders");

    [Fact]
    public async Task When_a_live_trip_is_stamped_later_than_now_plus_the_leeway_should_not_list_the_topic_and_log_a_warning()
    {
        // Arrange
        using var context = TestCorrelator.CreateContext();
        var store = new InMemoryTrippedTopicStore();
        var clock = new FakeTimeProvider(s_start);
        var breaker = new DistributedOutboxCircuitBreaker(
            store, new OutboxCircuitBreakerOptions { ClockSkewLeeway = s_leeway }, timeProvider: clock);
        var oneSecondPastTheLeeway = s_start + s_leeway + TimeSpan.FromSeconds(1);
        await store.WriteAsync([new TopicTrip(_orders, oneSecondPastTheLeeway)]);

        // Act
        var topics = await breaker.ReadTrippedTopicsAsync();

        // Assert
        Assert.DoesNotContain(_orders, topics);
        Assert.DoesNotContain(_orders, breaker.TrippedTopics);
        Assert.Contains(TestCorrelator.GetLogEventsFromCurrentContext(),
            e => e.Level == LogEventLevel.Warning && e.RenderMessage().Contains("orders"));
    }
}
