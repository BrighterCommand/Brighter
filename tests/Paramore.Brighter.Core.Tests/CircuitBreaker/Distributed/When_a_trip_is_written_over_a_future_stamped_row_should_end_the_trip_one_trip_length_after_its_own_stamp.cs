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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class TripWrittenOverFutureStampedRowTests
{
    private static readonly DateTimeOffset s_start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_cooldown = TimeSpan.FromSeconds(50);
    private static readonly TimeSpan s_leeway = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan s_settle = TimeSpan.FromMilliseconds(150);
    private readonly RoutingKey _orders = new("orders");

    [Fact]
    public async Task When_a_trip_is_written_over_a_future_stamped_row_should_end_the_trip_one_trip_length_after_its_own_stamp()
    {
        // Arrange
        var inner = new InMemoryTrippedTopicStore();
        await inner.WriteAsync([new TopicTrip(_orders, s_start + TimeSpan.FromHours(1))]);
        var store = new SignallingTrippedTopicStore(inner);
        var clock = new FakeTimeProvider(s_start);
        var options = new OutboxCircuitBreakerOptions { CooldownDuration = s_cooldown, ClockSkewLeeway = s_leeway };
        var breaker = new DistributedOutboxCircuitBreaker(store, options, timeProvider: clock);

        // Act
        breaker.TripTopic(_orders);
        await store.WriteCompleted;
        await Task.Delay(s_settle);
        clock.Advance(s_cooldown + s_leeway + TimeSpan.FromSeconds(1));

        // Assert
        Assert.DoesNotContain(_orders, breaker.TrippedTopics);
    }
}
