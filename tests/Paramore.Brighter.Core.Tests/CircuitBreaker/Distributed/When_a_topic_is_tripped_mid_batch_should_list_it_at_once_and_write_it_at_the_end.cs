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
