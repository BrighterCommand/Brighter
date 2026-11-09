using System;
using System.Threading.Tasks;
using MySqlConnector;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.MySql;
using Xunit;

namespace Paramore.Brighter.MySQL.Tests.CircuitBreaker;

public class MySqlTrippedTopicStoreOtherFailureTests : MySqlTrippedTopicStoreTestBase
{
    [Fact]
    public async Task When_the_mysql_trip_store_fails_for_a_reason_other_than_a_deadlock_should_throw_without_retrying()
    {
        // Arrange
        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MySqlTrippedTopicStore(recordingProvider, $"missing_{Uuid.NewAsString()}");

        // Act
        var exception = await Assert.ThrowsAsync<MySqlException>(
            () => store.WriteAsync([new TopicTrip(new RoutingKey("orders"), DateTimeOffset.UtcNow)]));

        // Assert
        Assert.NotEqual(MySqlErrorCode.LockDeadlock, exception.ErrorCode);
        Assert.Single(recordingProvider.ExecutedCommands);
    }
}
