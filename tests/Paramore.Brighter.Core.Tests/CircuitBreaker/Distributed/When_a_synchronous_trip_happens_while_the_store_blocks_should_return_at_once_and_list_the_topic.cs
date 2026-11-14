#region License

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

# endregion

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerSynchronousTripTests
{
    private readonly RoutingKey _orders = new("orders");

    [Fact]
    public async Task When_a_synchronous_trip_happens_while_the_store_blocks_should_return_at_once_and_list_the_topic()
    {
        // Arrange
        var store = new SignallingTrippedTopicStore(hold: WriteHold.AllWrites);
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: new FakeTimeProvider());

        // Act
        var trip = Task.Run(() => breaker.TripTopic(_orders));
        var returned = await Task.WhenAny(trip, Task.Delay(TimeSpan.FromSeconds(1))) == trip;

        // Assert
        Assert.True(returned);
        Assert.Equal([_orders], breaker.TrippedTopics.ToArray());
    }

    [Fact]
    public async Task When_a_synchronous_trip_happens_should_attempt_the_write_off_the_calling_thread()
    {
        // Arrange
        var store = new SignallingTrippedTopicStore(hold: WriteHold.AllWrites);
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: new FakeTimeProvider());
        var callingThreadId = Environment.CurrentManagedThreadId;

        // Act
        breaker.TripTopic(_orders);
        var started = await Task.WhenAny(store.WriteStarted, Task.Delay(TimeSpan.FromSeconds(5))) == store.WriteStarted;

        // Assert
        Assert.True(started);
        Assert.NotEqual(callingThreadId, store.WriteThreadId);
    }

    [Fact]
    public void When_the_topics_are_listed_should_make_no_store_call()
    {
        // Arrange
        var store = new SignallingTrippedTopicStore(hold: WriteHold.AllWrites);
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: new FakeTimeProvider());

        // Act
        var topics = breaker.TrippedTopics.ToArray();

        // Assert
        Assert.Empty(topics);
        Assert.Equal(0, store.ReadCount);
        Assert.Empty(store.WrittenTrips);
    }

    [Fact]
    public async Task When_a_synchronous_trip_happens_should_stamp_with_the_breaker_clock_truncated_to_microseconds()
    {
        // Arrange
        var store = new SignallingTrippedTopicStore(hold: WriteHold.AllWrites);
        var clock = new FakeTimeProvider();
        var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero).AddTicks(1234567);
        clock.SetUtcNow(now);
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: clock);

        // Act
        breaker.TripTopic(_orders);
        await Task.WhenAny(store.WriteStarted, Task.Delay(TimeSpan.FromSeconds(5)));

        // Assert
        var expected = now.AddTicks(-(now.Ticks % 10));
        Assert.Equal([new TopicTrip(_orders, expected)], store.WrittenTrips);
    }

    [Fact]
    public void When_the_options_change_after_construction_should_keep_the_original_cooldown_duration()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var options = new OutboxCircuitBreakerOptions { CooldownDuration = TimeSpan.FromSeconds(50) };
        var breaker = new DistributedOutboxCircuitBreaker(new SignallingTrippedTopicStore(hold: WriteHold.AllWrites), options, timeProvider: clock);
        breaker.TripTopic(_orders);

        // Act
        options.CooldownDuration = TimeSpan.FromSeconds(10);
        clock.Advance(TimeSpan.FromSeconds(49.9));
        var listedBeforeEnd = breaker.TrippedTopics.ToArray();
        clock.Advance(TimeSpan.FromSeconds(0.1));
        var listedAtEnd = breaker.TrippedTopics.ToArray();

        // Assert
        Assert.Equal([_orders], listedBeforeEnd);
        Assert.Empty(listedAtEnd);
    }
}
