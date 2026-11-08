using System;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Paramore.Brighter.Base.Test.CircuitBreaker;
using Paramore.Brighter.CircuitBreaker;
using Paramore.Brighter.Outbox.MsSql;
using Xunit;

namespace Paramore.Brighter.MSSQL.Tests.CircuitBreaker;

public class MsSqlTrippedTopicStoreOtherFailureTests : MsSqlTrippedTopicStoreTestBase
{
    [Fact]
    public async Task When_the_mssql_trip_store_fails_for_a_reason_other_than_a_deadlock_should_throw_without_retrying()
    {
        // Arrange
        var recordingProvider = new RecordingConnectionProvider(ConnectionProvider);
        var store = new MsSqlTrippedTopicStore(recordingProvider, $"missing_{Uuid.NewAsString()}");

        // Act
        var exception = await Assert.ThrowsAsync<SqlException>(
            () => store.WriteAsync([new TopicTrip(new RoutingKey("orders"), DateTimeOffset.UtcNow)]));

        // Assert
        Assert.NotEqual(1205, exception.Number);
        Assert.Single(recordingProvider.ExecutedCommands);
    }
}
