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
}
