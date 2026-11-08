using System;
using System.Linq;
using System.Threading.Tasks;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreCommandTimeoutTests : MySqlTrippedTopicStoreTestBase
{
    private static readonly DateTimeOffset s_now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan s_tripLength = TimeSpan.FromSeconds(50);

    [Theory]
    [InlineData(200, 1)]
    [InlineData(1000, 1)]
    [InlineData(1500, 2)]
    [InlineData(3000, 3)]
    public async Task When_the_mysql_trip_store_has_a_command_timeout_should_round_it_up_to_whole_seconds_with_a_minimum_of_one(
        int commandTimeoutMilliseconds, int expectedSeconds)
    {
        // Arrange
        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MySqlTrippedTopicStore(
            recordingProvider, TableName, commandTimeout: TimeSpan.FromMilliseconds(commandTimeoutMilliseconds));
        var topic = new RoutingKey($"orders-{Uuid.NewAsString()}");

        // Act
        await store.WriteAsync([new TopicTrip(topic, s_now)]);
        await store.ReadAsync(s_now, s_tripLength);

        // Assert
        Assert.NotEmpty(recordingProvider.ExecutedCommands);
        Assert.All(recordingProvider.ExecutedCommands, command => Assert.Equal(expectedSeconds, command.CommandTimeout));
    }
}
