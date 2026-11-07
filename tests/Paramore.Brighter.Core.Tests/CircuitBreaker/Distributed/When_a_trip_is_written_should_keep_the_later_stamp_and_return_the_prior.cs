using System;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Core.Tests.CircuitBreaker.Distributed;

public class InMemoryTrippedTopicStoreKeepsLaterStampTests
{
    private InMemoryTrippedTopicStore Store { get; } = new();

    private static readonly DateTimeOffset s_firstStamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task When_a_trip_is_written_should_keep_the_later_stamp_and_return_the_prior()
    {
        // Arrange
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");
        var firstStamp = s_firstStamp;
        var laterStamp = firstStamp.AddSeconds(10);
        var earlierStamp = firstStamp.AddSeconds(-10);

        // Act
        var first = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, firstStamp)]));
        var later = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, laterStamp)]));
        var earlier = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, earlierStamp)]));
        var read = await Store.ReadAsync(laterStamp.AddSeconds(1), TimeSpan.FromHours(1));

        // Assert
        Assert.Null(first.PriorTripTime);
        Assert.Equal(firstStamp, first.StoredTripTime);
        Assert.False(first.LeftEnded);

        Assert.Equal(firstStamp, later.PriorTripTime);
        Assert.Equal(laterStamp, later.StoredTripTime);

        Assert.Equal(laterStamp, earlier.PriorTripTime);
        Assert.Equal(laterStamp, earlier.StoredTripTime);

        var live = Assert.Single(read.Live, trip => trip.Topic == topic);
        Assert.Equal(laterStamp, live.TripTime);
    }
}
