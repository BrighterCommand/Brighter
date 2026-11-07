using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.PostgresSQL.Tests.TestDoubles;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStoreIssuesNoDeleteTests : PostgreSqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Fact]
    public async Task When_the_postgresql_trip_store_runs_should_issue_no_delete_command()
    {
        // Arrange
        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new PostgreSqlTrippedTopicStore(recordingProvider, TableName);
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");

        // Act
        await store.WriteAsync([new TopicTrip(topic, s_now - s_tripLength)]);
        var firstRead = await store.ReadAsync(s_now, s_tripLength);
        await store.WriteAsync([new TopicTrip(topic, s_now)]);
        await store.ReadAsync(s_now, s_tripLength);

        // Assert
        Assert.Contains(firstRead.Ended, trip => trip.Topic == topic);
        Assert.NotEmpty(recordingProvider.ExecutedCommands);
        Assert.DoesNotContain(
            recordingProvider.ExecutedCommands,
            command => command.CommandText.Contains("DELETE", StringComparison.OrdinalIgnoreCase));
    }
}
