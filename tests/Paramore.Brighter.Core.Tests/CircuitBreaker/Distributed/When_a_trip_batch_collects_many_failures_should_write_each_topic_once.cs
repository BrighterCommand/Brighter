using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Time.Testing;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed.TestDoubles;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class DistributedBreakerTripBatchTests
{
    private readonly RoutingKey _orders = new("orders");

    [Fact]
    public async Task When_a_trip_batch_collects_many_failures_should_write_each_topic_once()
    {
        // Arrange
        var store = new CountingTrippedTopicStore();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: clock);
        var batch = breaker.BeginTripBatch();
        DateTimeOffset latestStamp = default;

        // Act
        for (var i = 0; i < 100; i++)
        {
            clock.Advance(TimeSpan.FromMilliseconds(1));
            latestStamp = clock.GetUtcNow();
            batch.TripTopic(_orders);
        }

        var listedBeforeWrite = breaker.TrippedTopics.ToArray();
        await batch.WriteAsync();

        // Assert
        Assert.Equal([_orders], listedBeforeWrite);
        var write = Assert.Single(store.Writes);
        Assert.Equal([new TopicTrip(_orders, latestStamp)], write);
    }

    [Fact]
    public async Task When_a_trip_batch_collects_many_topics_should_write_them_in_one_store_operation()
    {
        // Arrange
        var store = new CountingTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)));
        var batch = breaker.BeginTripBatch();

        // Act
        for (var i = 0; i < 50; i++)
            batch.TripTopic(new RoutingKey($"topic-{i}"));
        await batch.WriteAsync();

        // Assert
        var write = Assert.Single(store.Writes);
        Assert.Equal(50, write.Count);
        Assert.Equal(50, write.Select(trip => trip.Topic).Distinct().Count());
    }

    [Fact]
    public async Task When_a_trip_batch_is_written_twice_should_not_write_again()
    {
        // Arrange
        var store = new CountingTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)));
        var batch = breaker.BeginTripBatch();
        batch.TripTopic(_orders);
        await batch.WriteAsync();

        // Act
        await batch.WriteAsync();

        // Assert
        Assert.Single(store.Writes);
    }

    [Fact]
    public async Task When_a_trip_batch_has_no_trips_should_not_call_the_store()
    {
        // Arrange
        var store = new CountingTrippedTopicStore();
        var breaker = new DistributedOutboxCircuitBreaker(store, timeProvider: new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)));
        var batch = breaker.BeginTripBatch();

        // Act
        await batch.WriteAsync();

        // Assert
        Assert.Empty(store.Writes);
    }
}
