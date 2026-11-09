using System;
using System.Threading.Tasks;
using Npgsql;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.PostgreSql;
using Xunit;

namespace Paramore.Brighter.PostgresSQL.Tests.CircuitBreaker;

public class PostgreSqlTrippedTopicStoreOtherFailureTests : PostgreSqlTrippedTopicStoreTestBase
{
    [Fact]
    public async Task When_the_postgresql_trip_store_fails_for_a_reason_other_than_a_deadlock_should_throw_without_retrying()
    {
        // Arrange
        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new PostgreSqlTrippedTopicStore(recordingProvider, $"missing_{Uuid.NewAsString()}");

        // Act
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => store.WriteAsync([new TopicTrip(new RoutingKey("orders"), DateTimeOffset.UtcNow)]));

        // Assert
        Assert.NotEqual("40P01", exception.SqlState);
        Assert.Single(recordingProvider.ExecutedCommands);
    }
}
