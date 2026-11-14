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
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerCoalescedWriteTests
{
    private readonly RoutingKey _orders = new("orders");

    [Fact]
    public async Task When_many_callbacks_trip_a_topic_during_an_in_flight_write_should_keep_one_pending_write()
    {
        // Arrange
        var store = new SignallingTrippedTopicStore(hold: WriteHold.FirstWriteOnly);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var breaker = new DistributedOutboxCircuitBreaker(store, new OutboxCircuitBreakerOptions { StoreTimeout = TimeSpan.FromSeconds(60) }, timeProvider: clock);
        var firstStamp = clock.GetUtcNow();
        breaker.TripTopic(_orders);
        await store.WriteStarted.WaitAsync(TimeSpan.FromSeconds(5));

        // Act
        clock.Advance(TimeSpan.FromSeconds(1));
        Parallel.For(0, 10_000, _ => breaker.TripTopic(_orders));
        clock.Advance(TimeSpan.FromSeconds(1));
        var latestStamp = clock.GetUtcNow();
        breaker.TripTopic(_orders);
        var writesWhileBlocked = store.WrittenTrips.Count;
        store.ReleaseWrites();
        await store.SecondWriteStarted.WaitAsync(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(1, writesWhileBlocked);
        Assert.Equal([new TopicTrip(_orders, firstStamp), new TopicTrip(_orders, latestStamp)], store.WrittenTrips);
    }
}
