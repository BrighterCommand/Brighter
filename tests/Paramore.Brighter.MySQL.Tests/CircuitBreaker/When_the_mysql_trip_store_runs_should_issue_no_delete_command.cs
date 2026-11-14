using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreIssuesNoDeleteTests : MySqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_the_mysql_trip_store_runs_should_issue_no_delete_command()
    {
        // Arrange
        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MySqlTrippedTopicStore(recordingProvider, TableName);
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");

        // Act
        await store.WriteAsync([new TopicTrip(topic, s_now - s_tripLength)]);
        var firstEnded = await store.EndExpiredAsync(new TripWindow(s_now, s_tripLength));
        await store.WriteAsync([new TopicTrip(topic, s_now)]);
        await store.EndExpiredAsync(new TripWindow(s_now, s_tripLength));
        await store.ReadLiveAsync(new TripWindow(s_now, s_tripLength));

        // Assert
        Assert.Contains(firstEnded, trip => trip.Topic == topic);
        Assert.NotEmpty(recordingProvider.ExecutedCommands);
        Assert.DoesNotContain(
            recordingProvider.ExecutedCommands,
            command => command.CommandText.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }
}
