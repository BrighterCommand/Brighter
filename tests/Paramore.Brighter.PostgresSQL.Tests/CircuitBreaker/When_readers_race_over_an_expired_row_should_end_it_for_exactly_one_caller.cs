using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStoreEndExpiredRaceTests : PostgreSqlTrippedTopicStoreTestBase
{
    private const int REPETITIONS = 1_000;
    private const int CALLERS = 4;

    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_readers_race_over_an_expired_row_should_end_it_for_exactly_one_caller()
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
            var ended = await Task.WhenAll(Enumerable.Range(0, CALLERS)
                .Select(_ => Task.Run(() => Store.EndExpiredAsync(s_now, s_tripLength))));

            // Assert
            Assert.Equal(1, ended.Count(trips => trips.Any(trip => trip.Topic == expiredTopic)));
            Assert.All(ended, trips => Assert.DoesNotContain(trips, trip => trip.Topic == liveTopic));
            Assert.Contains(ended.SelectMany(trips => trips), trip => trip.Topic == expiredTopic && trip.TripTime == expiredStamp);
        }
    }
}
