using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.Base.Test.CircuitBreaker;

public abstract class TrippedTopicStoreEndedContractTest
{
    private const int REPETITIONS = 1_000;
    private const int READERS = 4;

    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    protected abstract IAmATrippedTopicStore Store { get; }

    [Fact]
    public async Task When_readers_race_over_an_ended_row_should_return_it_to_exactly_one()
    {
        for (var repetition = 0; repetition < REPETITIONS; repetition++)
        {
            // Arrange
            var expiredTopic = new RoutingKey($"expired-{Uuid.NewAsString()}");
            var liveTopic = new RoutingKey($"live-{Uuid.NewAsString()}");
            var expiredStamp = s_now - s_tripLength;
            var liveStamp = s_now - s_tripLength + TimeSpan.FromSeconds(1);
            await Store.WriteAsync([new TopicTrip(expiredTopic, expiredStamp), new TopicTrip(liveTopic, liveStamp)]);

            // Act
            var reads = await Task.WhenAll(Enumerable.Range(0, READERS)
                .Select(_ => Task.Run(() => Store.ReadAsync(s_now, s_tripLength))));

            // Assert
            Assert.Equal(1, reads.Count(read => read.Ended.Any(trip => trip.Topic == expiredTopic)));
            Assert.All(reads, read => Assert.DoesNotContain(read.Live, trip => trip.Topic == expiredTopic));
            Assert.All(reads, read => Assert.DoesNotContain(read.Ended, trip => trip.Topic == liveTopic));
            Assert.All(reads, read => Assert.Contains(read.Live, trip => trip.Topic == liveTopic));
        }
    }

    [Fact]
    public async Task When_a_trip_is_written_over_an_ended_row_should_revive_only_with_a_later_stamp()
    {
        // Arrange
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");
        var endedStamp = s_now - s_tripLength;
        var laterStamp = endedStamp + TimeSpan.FromSeconds(10);
        await Store.WriteAsync([new TopicTrip(topic, endedStamp)]);
        var marked = await Store.ReadAsync(s_now, s_tripLength);
        Assert.Contains(marked.Ended, trip => trip.Topic == topic);

        // Act
        var equal = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, endedStamp)]));
        var earlier = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, endedStamp - TimeSpan.FromSeconds(10))]));
        var afterEqualAndEarlier = await Store.ReadAsync(s_now, s_tripLength);
        var later = Assert.Single(await Store.WriteAsync([new TopicTrip(topic, laterStamp)]));
        var afterLater = await Store.ReadAsync(s_now, s_tripLength);

        // Assert
        Assert.True(equal.LeftEnded);
        Assert.Null(equal.PriorTripTime);
        Assert.True(earlier.LeftEnded);
        Assert.Null(earlier.PriorTripTime);
        Assert.DoesNotContain(afterEqualAndEarlier.Live, trip => trip.Topic == topic);

        Assert.False(later.LeftEnded);
        Assert.Null(later.PriorTripTime);
        var revived = Assert.Single(afterLater.Live, trip => trip.Topic == topic);
        Assert.Equal(laterStamp, revived.TripTime);
    }
}
