using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreEndsManyExpiredTripsTests : MySqlTrippedTopicStoreTestBase
{
    private const int EXPIRED_TOPICS = 200;
    private const int MOST_STATEMENTS_ALLOWED = 5;
    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_many_trips_have_expired_should_end_them_in_a_bounded_number_of_statements()
    {
        // Arrange
        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MySqlTrippedTopicStore(recordingProvider, TableName);
        var prefix = $"orders-{Uuid.NewAsString()}";
        var topics = Enumerable.Range(0, EXPIRED_TOPICS).Select(i => new RoutingKey($"{prefix}-{i:D4}")).ToList();
        await Store.WriteAsync(topics.Select(topic => new TopicTrip(topic, s_now - s_tripLength)).ToList());

        // Act
        var ended = await store.EndExpiredAsync(new TripWindow(s_now, s_tripLength));

        // Assert
        Assert.Equal(topics.Count, ended.Count);
        Assert.All(topics, topic => Assert.Contains(ended, trip => trip.Topic == topic));
        Assert.InRange(recordingProvider.ExecutedCommands.Count, 1, MOST_STATEMENTS_ALLOWED);
    }
}
