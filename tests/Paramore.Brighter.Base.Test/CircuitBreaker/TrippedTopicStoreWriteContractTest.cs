using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Base.Test.CircuitBreaker;

public abstract class TrippedTopicStoreWriteContractTest
{
    private const long TICKS_PER_MICROSECOND = 10;

    private static readonly DateTimeOffset s_firstStamp = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);

    protected abstract IAmATrippedTopicStore Store { get; }

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

    [Fact]
    public async Task When_a_microsecond_stamp_is_written_should_read_back_equal()
    {
        // Arrange
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");
        var stampWithSubMicrosecondTicks = s_firstStamp.AddTicks(1_234_567);
        var microsecondStamp = stampWithSubMicrosecondTicks.AddTicks(-(stampWithSubMicrosecondTicks.Ticks % TICKS_PER_MICROSECOND));

        // Act
        var written = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, microsecondStamp)]));
        var read = await Store.ReadAsync(microsecondStamp.AddSeconds(1), TimeSpan.FromHours(1));

        // Assert
        Assert.Equal(microsecondStamp, written.StoredTripTime);
        var live = Assert.Single(read.Live, trip => trip.Topic == topic);
        Assert.Equal(microsecondStamp, live.TripTime);
    }

    [Fact]
    public async Task When_a_write_repeats_a_topic_should_reject_it_and_change_nothing()
    {
        // Arrange
        var orders = new RoutingKey($"orders-{Uuid.NewAsString()}");
        var invoices = new RoutingKey($"invoices-{Uuid.NewAsString()}");
        var stamp = s_firstStamp;

        // Act
        var repeatsATopic = async () => await Store.WriteAsync(
            [new TopicTrip(orders, stamp), new TopicTrip(invoices, stamp), new TopicTrip(orders, stamp)]);
        await Assert.ThrowsAsync<ArgumentException>(repeatsATopic);
        var afterRejectedWrite = await Store.ReadAsync(stamp.AddSeconds(1), TimeSpan.FromHours(1));
        var results = await Store.WriteAsync([new TopicTrip(orders, stamp), new TopicTrip(invoices, stamp)]);

        // Assert
        Assert.DoesNotContain(afterRejectedWrite.Live, trip => trip.Topic == orders || trip.Topic == invoices);
        Assert.Equal(2, results.Count);
        Assert.Contains(results, result => result.Topic == orders);
        Assert.Contains(results, result => result.Topic == invoices);
    }
}
