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
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerMidBatchTripTests
{
    [Fact]
    public async Task When_a_topic_is_tripped_mid_batch_should_list_it_at_once_and_write_it_at_the_end()
    {
        // Arrange
        var orders = new RoutingKey("orders");
        var store = new CountingTrippedTopicStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: clock);
        var batch = breaker.BeginTripBatch();

        // Act
        batch.TripTopic(orders);
        var listedMidBatch = breaker.TrippedTopics.ToArray();
        var writesMidBatch = store.Writes.Count;
        await batch.WriteAsync();

        // Assert
        Assert.Equal([orders], listedMidBatch);
        Assert.Equal(0, writesMidBatch);
        var write = Assert.Single(store.Writes);
        Assert.Equal([orders], write.Select(trip => trip.Topic).ToArray());
    }
}
